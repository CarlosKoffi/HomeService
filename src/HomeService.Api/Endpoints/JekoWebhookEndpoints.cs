using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        return app;
    }

    private static async Task<IResult> ReceiveTransactionAsync(
        HttpRequest request,
        IConfiguration configuration,
        CompanyWalletService walletService,
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
            if (!string.Equals(transactionType, "transfer", StringComparison.OrdinalIgnoreCase))
            {
                // Les encaissements seront traites par le service Pay-in. Ils ne doivent jamais
                // modifier le portefeuille de reversement d'une entreprise.
                return Results.Ok(new { received = true, ignored = true, transactionType });
            }

            var details = data.TryGetProperty("transactionDetails", out var transactionDetails)
                && transactionDetails.ValueKind == JsonValueKind.Object
                    ? transactionDetails
                    : default;
            var reference = ReadString(details, "reference");
            var externalId = ReadString(data, "id") ?? ReadString(data, "transactionId") ?? reference;
            var status = ReadString(data, "status") ?? ReadString(root, "status");
            var message = ReadString(data, "message") ?? ReadString(data, "description") ?? ReadString(root, "message");
            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(status))
            {
                return Results.BadRequest(new { message = "Identifiant ou statut manquant." });
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
}
