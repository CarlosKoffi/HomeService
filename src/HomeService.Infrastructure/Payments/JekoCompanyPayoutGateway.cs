using System.Net.Http.Json;
using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HomeService.Infrastructure.Payments;

public sealed class JekoCompanyPayoutGateway(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<JekoCompanyPayoutGateway> logger) : ICompanyPayoutGateway
{
    public bool IsEnabled => string.Equals(configuration["JEKO_PAYOUTS_ENABLED"], "true", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(configuration["JEKO_API_KEY"])
        && !string.IsNullOrWhiteSpace(configuration["JEKO_STORE_ID"]);

    public async Task<CompanyPayoutGatewayResult> CreateAsync(
        CompanyPayoutGatewayRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return CompanyPayoutGatewayResult.Disabled();
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(TransferPath()));
        AddAuthentication(message, request.Reference);
        message.Content = JsonContent.Create(new
        {
            storeId = configuration["JEKO_STORE_ID"],
            reference = request.Reference,
            amount = request.Amount,
            currency = request.Currency,
            type = request.Method == CompanyPayoutMethod.BankTransfer ? "bank" : "mobile_money",
            beneficiary = new
            {
                name = request.BeneficiaryName,
                provider = request.ProviderCode,
                account = request.Identifier
            }
        });

        return await SendAsync(message, cancellationToken);
    }

    public async Task<CompanyPayoutGatewayResult> GetStatusAsync(
        string externalTransactionId,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return CompanyPayoutGatewayResult.Disabled();
        }

        var statusPath = configuration["JEKO_TRANSFER_STATUS_PATH"] ?? "/transfers/{id}";
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUri(statusPath.Replace("{id}", Uri.EscapeDataString(externalTransactionId), StringComparison.Ordinal)));
        AddAuthentication(message, externalTransactionId);
        return await SendAsync(message, cancellationToken);
    }

    private async Task<CompanyPayoutGatewayResult> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = Parse(body, response.IsSuccessStatusCode);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Jeko payout API returned {StatusCode}: {Message}", (int)response.StatusCode, parsed.Message);
            }

            return parsed;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Jeko payout API is unreachable.");
            return new CompanyPayoutGatewayResult(false, false, false, "network_error", null, exception.Message);
        }
    }

    private static CompanyPayoutGatewayResult Parse(string body, bool httpSuccess)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new CompanyPayoutGatewayResult(httpSuccess, !httpSuccess, false, httpSuccess ? "pending" : "failed", null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var payload = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object ? data : root;
            var status = ReadString(payload, "status") ?? ReadString(root, "status") ?? (httpSuccess ? "pending" : "failed");
            var id = ReadString(payload, "id") ?? ReadString(payload, "transactionId") ?? ReadString(root, "id");
            var message = ReadString(root, "message") ?? ReadString(payload, "message");
            var normalized = status.Trim().ToLowerInvariant();
            var success = normalized is "success" or "successful" or "paid" or "completed";
            var failed = normalized is "failed" or "rejected" or "cancelled" or "canceled" or "error";
            return new CompanyPayoutGatewayResult(httpSuccess || success, success || failed, success, status, id, message);
        }
        catch (JsonException)
        {
            return new CompanyPayoutGatewayResult(httpSuccess, !httpSuccess, false, httpSuccess ? "pending" : "failed", null, body[..Math.Min(body.Length, 500)]);
        }
    }

    private void AddAuthentication(HttpRequestMessage message, string idempotencyKey)
    {
        var header = configuration["JEKO_API_KEY_HEADER"] ?? "x-api-key";
        message.Headers.TryAddWithoutValidation(header, configuration["JEKO_API_KEY"]);
        message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
    }

    private string TransferPath() => configuration["JEKO_TRANSFER_PATH"] ?? "/transfers";

    private Uri BuildUri(string path)
    {
        var baseUrl = configuration["JEKO_API_BASE_URL"] ?? "https://api.jeko.africa";
        return new Uri(new Uri(baseUrl.TrimEnd('/') + '/', UriKind.Absolute), path.TrimStart('/'));
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? property.ToString()
            : null;
}
