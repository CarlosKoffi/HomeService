using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeService.Application.Clients;
using HomeService.Application.CompanyPortal;

namespace HomeService.Api.Endpoints;

public static class JekoWebhookEndpoints
{
    public static IEndpointRouteBuilder MapJekoWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks/jeko");

        group.MapPost("/transactions", ReceiveTransactionAsync)
            .WithName("ReceiveJekoTransactionWebhook");

        // Conserve l'ancienne URL le temps de basculer la configuration dans le Cockpit Jeko.
        group.MapPost("/payouts", ReceiveTransactionAsync)
            .WithName("ReceiveJekoPayoutWebhook")
            .ExcludeFromDescription();

        group.MapGet("/return", ReturnToApplication)
            .WithName("ReturnFromJekoPayment");

        return app;
    }

    private static async Task<IResult> ReceiveTransactionAsync(
        HttpRequest request,
        IConfiguration configuration,
        CompanyWalletService walletService,
        ClientMissionPaymentService paymentService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var secret = configuration["JEKO_WEBHOOK_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogError("JEKO_WEBHOOK_SECRET is missing; Jeko webhook rejected.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var providedSignature = request.Headers["Jeko-Signature"].FirstOrDefault();
        if (!IsSignatureValid(body, secret, providedSignature))
        {
            logger.LogWarning("Rejected Jeko webhook with invalid Jeko-Signature.");
            return Results.Unauthorized();
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var data = root.TryGetProperty("data", out var payload) && payload.ValueKind == JsonValueKind.Object
                ? payload
                : root;

            var transactionType = ReadString(data, "transactionType");
            var details = data.TryGetProperty("transactionDetails", out var transactionDetails)
                && transactionDetails.ValueKind == JsonValueKind.Object
                    ? transactionDetails
                    : default;
            var reference = ReadString(details, "reference");
            var externalId = ReadString(data, "id") ?? ReadString(data, "transactionId") ?? reference;
            var status = ReadString(data, "status") ?? ReadString(root, "status");
            var message = ReadString(data, "message") ?? ReadString(data, "description") ?? ReadString(root, "message");
            var amount = ReadAmount(data);
            var currency = ReadCurrency(data);
            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(status))
            {
                return Results.BadRequest(new { message = "Identifiant ou statut manquant." });
            }

            if (string.Equals(transactionType, "payment", StringComparison.OrdinalIgnoreCase))
            {
                var paymentRequestId = ReadString(details, "paymentRequestId")
                    ?? ReadString(data, "paymentRequestId");
                var appliedPayment = await paymentService.ApplyExternalStatusAsync(
                    paymentRequestId,
                    reference,
                    status,
                    message,
                    externalId,
                    amount,
                    currency,
                    cancellationToken);
                return appliedPayment
                    ? Results.Ok(new { received = true })
                    : Results.NotFound(new { message = "Paiement inconnu." });
            }

            if (!string.Equals(transactionType, "transfer", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Ok(new { received = true, ignored = true, transactionType });
            }

            var applied = await walletService.ApplyExternalStatusAsync(
                externalId,
                reference,
                status,
                message,
                cancellationToken);
            return applied
                ? Results.Ok(new { received = true })
                : Results.NotFound(new { message = "Reversement inconnu." });
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "Corps JSON invalide." });
        }
    }

    private static IResult ReturnToApplication(
        Guid paymentRequestId,
        Guid missionId,
        string? result)
    {
        // Ce retour navigateur n'accorde jamais le paiement. L'application interroge l'API,
        // laquelle attend le webhook signe ou le statut Jeko officiel.
        var normalizedResult = string.Equals(result, "success", StringComparison.OrdinalIgnoreCase)
            ? "success"
            : "error";
        var deepLink = $"wele://payment?missionId={missionId:D}&paymentRequestId={paymentRequestId:D}&result={normalizedResult}";
        var title = normalizedResult == "success" ? "Paiement transmis" : "Paiement non finalise";
        var message = normalizedResult == "success"
            ? "Nous verifions maintenant la confirmation securisee de Jeko."
            : "Vous pouvez revenir dans Wele et relancer le paiement.";
        var html = $$"""
            <!doctype html>
            <html lang="fr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta http-equiv="refresh" content="1;url={{deepLink}}">
              <title>{{title}} - Wele</title>
              <style>
                body{font-family:Arial,sans-serif;background:#fff;color:#111827;margin:0;display:grid;min-height:100vh;place-items:center}
                main{max-width:520px;padding:40px 24px;text-align:center}.mark{font-size:48px;color:#1768ff}
                h1{font-size:30px;margin:16px 0 10px}p{color:#64748b;line-height:1.6}
                a{display:inline-block;margin-top:20px;padding:16px 24px;border-radius:16px;background:#1768ff;color:#fff;text-decoration:none;font-weight:700}
              </style>
            </head>
            <body><main><div class="mark">w</div><h1>{{title}}</h1><p>{{message}}</p><a href="{{deepLink}}">Revenir dans l'application</a></main></body>
            </html>
            """;
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static bool IsSignatureValid(string body, string secret, string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var normalized = provided.Trim();
        if (normalized.Length != expectedBytes.Length * 2)
        {
            return false;
        }

        try
        {
            var providedBytes = Convert.FromHexString(normalized);
            return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
            ? property.ToString()
            : null;

    private static int? ReadAmount(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("amount", out var amount))
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
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("amount", out var amount)
            || amount.ValueKind != JsonValueKind.Object)
        {
            return ReadString(element, "currency");
        }

        return ReadString(amount, "currency");
    }
}
