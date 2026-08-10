using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HomeService.Application.CompanyPortal;

namespace HomeService.Api.Endpoints;

public static class JekoWebhookEndpoints
{
    public static IEndpointRouteBuilder MapJekoWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/jeko/payouts", async (
            HttpRequest request,
            IConfiguration configuration,
            CompanyWalletService walletService,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var secret = configuration["JEKO_WEBHOOK_SECRET"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                logger.LogError("JEKO_WEBHOOK_SECRET is missing; Jeko payout webhook rejected.");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(cancellationToken);
            var signatureHeader = configuration["JEKO_WEBHOOK_SIGNATURE_HEADER"] ?? "Jeko-Signature";
            var providedSignature = request.Headers[signatureHeader].FirstOrDefault();
            if (!IsSignatureValid(body, secret, providedSignature))
            {
                logger.LogWarning("Rejected Jeko payout webhook with invalid signature.");
                return Results.Unauthorized();
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                var data = root.TryGetProperty("data", out var payload) && payload.ValueKind == JsonValueKind.Object
                    ? payload
                    : root;
                var externalId = ReadString(data, "id")
                    ?? ReadString(data, "transactionId")
                    ?? ReadString(data, "reference");
                var status = ReadString(data, "status") ?? ReadString(root, "status");
                var message = ReadString(data, "message") ?? ReadString(root, "message");
                if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(status))
                {
                    return Results.BadRequest(new { message = "Identifiant ou statut manquant." });
                }

                var applied = await walletService.ApplyExternalStatusAsync(externalId, status, message, cancellationToken);
                return applied ? Results.Ok(new { received = true }) : Results.NotFound(new { message = "Reversement inconnu." });
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { message = "Corps JSON invalide." });
            }
        })
        .WithName("ReceiveJekoPayoutWebhook");

        return app;
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
        if (normalized.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[7..];
        }

        byte[] providedBytes;
        try
        {
            providedBytes = normalized.Length == expectedBytes.Length * 2
                ? Convert.FromHexString(normalized)
                : Convert.FromBase64String(normalized);
        }
        catch (FormatException)
        {
            return false;
        }

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property)
            ? property.ToString()
            : null;
}
