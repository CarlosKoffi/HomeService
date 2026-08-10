using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HomeService.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HomeService.Infrastructure.Payments;

public sealed class JekoClientPaymentGateway(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<JekoClientPaymentGateway> logger) : IClientPaymentGateway
{
    public bool IsEnabled => string.Equals(configuration["JEKO_PAYMENTS_ENABLED"], "true", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(configuration["JEKO_API_KEY"])
        && !string.IsNullOrWhiteSpace(configuration["JEKO_API_KEY_ID"])
        && !string.IsNullOrWhiteSpace(configuration["JEKO_STORE_ID"])
        && Uri.TryCreate(configuration["JEKO_PAYMENT_RETURN_BASE_URL"], UriKind.Absolute, out _);

    public int FeeRateBasisPoints => Math.Clamp(
        int.TryParse(configuration["JEKO_PAYMENT_FEE_RATE_BASIS_POINTS"], out var configured) ? configured : 150,
        0,
        5000);

    public async Task<ClientPaymentGatewayResult> CreateAsync(
        ClientPaymentGatewayRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return ClientPaymentGatewayResult.Disabled();
        }

        var callbackBase = configuration["JEKO_PAYMENT_RETURN_BASE_URL"]!.TrimEnd('/');
        var callbackQuery = $"paymentRequestId={request.LocalPaymentRequestId:D}&missionId={request.MissionId:D}";

        using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri("/partner_api/payment_requests"));
        AddAuthentication(message);
        message.Content = JsonContent.Create(new
        {
            storeId = configuration["JEKO_STORE_ID"],
            amountCents = request.Amount,
            currency = request.Currency,
            reference = request.Reference,
            paymentDetails = new
            {
                type = "redirect",
                data = new
                {
                    paymentMethod = request.PaymentMethod,
                    successUrl = $"{callbackBase}/api/webhooks/jeko/return?{callbackQuery}&result=success",
                    errorUrl = $"{callbackBase}/api/webhooks/jeko/return?{callbackQuery}&result=error"
                }
            }
        });

        return await SendAsync(message, cancellationToken);
    }

    public async Task<ClientPaymentGatewayResult> GetStatusAsync(
        string externalPaymentRequestId,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return ClientPaymentGatewayResult.Disabled();
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUri($"/partner_api/payment_requests/{Uri.EscapeDataString(externalPaymentRequestId)}"));
        AddAuthentication(message);
        return await SendAsync(message, cancellationToken);
    }

    private async Task<ClientPaymentGatewayResult> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return new ClientPaymentGatewayResult(
                    true,
                    false,
                    "pending",
                    null,
                    null,
                    null,
                    "Cette reference existe deja chez Jeko. Le webhook reste attendu.",
                    DateTimeOffset.UtcNow.AddMinutes(5));
            }

            var parsed = Parse(body, response.IsSuccessStatusCode);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Jeko payment API returned {StatusCode}: {Message}",
                    (int)response.StatusCode,
                    parsed.Message);
            }

            return parsed;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Jeko payment API timed out; the request remains pending for webhook reconciliation.");
            return new ClientPaymentGatewayResult(
                true,
                false,
                "pending",
                null,
                null,
                null,
                "Jeko tarde a repondre. Verification automatique en cours.",
                DateTimeOffset.UtcNow.AddMinutes(5));
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Jeko payment API is unreachable; the request remains pending.");
            return new ClientPaymentGatewayResult(
                true,
                false,
                "pending",
                null,
                null,
                null,
                "Jeko est momentanement injoignable. Verification automatique en cours.",
                DateTimeOffset.UtcNow.AddMinutes(5));
        }
    }

    private static ClientPaymentGatewayResult Parse(string body, bool httpSuccess)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new ClientPaymentGatewayResult(
                httpSuccess,
                !httpSuccess,
                httpSuccess ? "pending" : "error",
                null,
                null,
                null,
                httpSuccess ? null : "Reponse Jeko vide.",
                httpSuccess ? DateTimeOffset.UtcNow.AddMinutes(5) : null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var payload = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                ? data
                : root;
            var status = ReadString(payload, "status") ?? ReadString(root, "status") ?? (httpSuccess ? "pending" : "error");
            var normalized = NormalizeStatus(status);
            var externalId = ReadString(payload, "id") ?? ReadString(root, "id");
            var transactionId = ReadString(payload, "transactionId") ?? ReadString(root, "transactionId");
            var redirectUrl = ReadString(payload, "redirectUrl") ?? ReadString(root, "redirectUrl");
            var message = ReadString(root, "message") ?? ReadString(payload, "message");
            var amount = ReadAmount(payload) ?? ReadAmount(root);
            var currency = ReadCurrency(payload) ?? ReadCurrency(root);
            return new ClientPaymentGatewayResult(
                httpSuccess,
                normalized is "success" or "error",
                normalized,
                externalId,
                transactionId,
                redirectUrl,
                message,
                normalized == "pending" ? DateTimeOffset.UtcNow.AddMinutes(5) : null,
                amount,
                currency);
        }
        catch (JsonException)
        {
            return new ClientPaymentGatewayResult(
                httpSuccess,
                !httpSuccess,
                httpSuccess ? "pending" : "error",
                null,
                null,
                null,
                body[..Math.Min(body.Length, 500)],
                httpSuccess ? DateTimeOffset.UtcNow.AddMinutes(5) : null);
        }
    }

    private void AddAuthentication(HttpRequestMessage message)
    {
        message.Headers.TryAddWithoutValidation("X-API-KEY", configuration["JEKO_API_KEY"]);
        message.Headers.TryAddWithoutValidation("X-API-KEY-ID", configuration["JEKO_API_KEY_ID"]);
    }

    private Uri BuildUri(string path)
    {
        var baseUrl = configuration["JEKO_API_BASE_URL"] ?? "https://api.jeko.africa";
        return new Uri(new Uri(baseUrl.TrimEnd('/') + '/', UriKind.Absolute), path.TrimStart('/'));
    }

    private static string NormalizeStatus(string value) => value.Trim().ToLowerInvariant() switch
    {
        "success" or "successful" or "paid" or "completed" => "success",
        "error" or "failed" or "rejected" => "error",
        _ => "pending"
    };

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var property)
        && property.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? property.ToString()
            : null;

    private static int? ReadAmount(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty("amountCents", out var amountCents)
            && amountCents.TryGetInt32(out var cents))
        {
            return cents;
        }

        if (!element.TryGetProperty("amount", out var amount))
        {
            return null;
        }

        if (amount.ValueKind == JsonValueKind.Number && amount.TryGetInt32(out var directAmount))
        {
            return directAmount;
        }

        return amount.ValueKind == JsonValueKind.Object
            && amount.TryGetProperty("amount", out var nestedAmount)
            && nestedAmount.TryGetInt32(out var value)
                ? value
                : null;
    }

    private static string? ReadCurrency(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var directCurrency = ReadString(element, "currency");
        if (!string.IsNullOrWhiteSpace(directCurrency))
        {
            return directCurrency;
        }

        return element.TryGetProperty("amount", out var amount) && amount.ValueKind == JsonValueKind.Object
            ? ReadString(amount, "currency")
            : null;
    }
}
