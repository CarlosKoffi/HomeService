using HomeService.Application.Abstractions;
using HomeService.Application.Missions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionPaymentService(
    IAppDbContext db,
    IClientPaymentGateway gateway,
    MissionCommercialPricingService pricingService,
    ClientMissionConfirmationService confirmationService)
{
    public async Task<ClientMissionPaymentPreviewResult> PreviewAsync(
        Guid customerId,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var mission = await db.Missions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return ClientMissionPaymentPreviewResult.NotFound("Mission introuvable.");
        }

        if (mission.CustomerId != customerId)
        {
            return ClientMissionPaymentPreviewResult.Forbidden("Cette mission n'appartient pas au client connecte.");
        }

        var serviceAmount = mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? mission.FinalTotalAmount ?? 0;
        if (serviceAmount <= 0 || mission.Status != MissionStatus.Accepted)
        {
            return ClientMissionPaymentPreviewResult.Invalid(
                "Le prix sera affiche lorsque le prestataire aura accepte la mission.");
        }

        var pricing = await pricingService.CalculateAsync(mission, serviceAmount, cancellationToken);
        var requestedAmount = GrossUp(pricing.CustomerTotalAmount, gateway.FeeRateBasisPoints);
        return ClientMissionPaymentPreviewResult.Ok(new ClientMissionPaymentPreviewResponse(
            mission.Id,
            serviceAmount,
            pricing.CustomerServiceFeeAmount,
            requestedAmount - pricing.CustomerTotalAmount,
            requestedAmount,
            pricing.Currency,
            gateway.FeeRateBasisPoints));
    }

    public async Task<ClientMissionPaymentResult> StartAsync(
        Guid customerId,
        Guid missionId,
        StartClientMissionPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!gateway.IsEnabled)
        {
            return ClientMissionPaymentResult.Unavailable(
                "Les paiements Jeko ne sont pas encore actives sur cet environnement.");
        }

        var mission = await db.Missions
            .FirstOrDefaultAsync(item => item.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return ClientMissionPaymentResult.NotFound("Mission introuvable.");
        }

        if (mission.CustomerId != customerId)
        {
            return ClientMissionPaymentResult.Forbidden("Cette mission n'appartient pas au client connecte.");
        }

        var successful = await db.MissionPaymentRequests
            .AsNoTracking()
            .Where(item => item.MissionId == missionId && item.Status == MissionPaymentRequestStatus.Success)
            .OrderByDescending(item => item.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (successful is not null)
        {
            return ClientMissionPaymentResult.Ok(ToResponse(successful));
        }

        var existing = await db.MissionPaymentRequests
            .Where(item => item.MissionId == missionId && item.Status == MissionPaymentRequestStatus.Pending)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                existing.MarkError("La demande de paiement Jeko a expire. Relancez le paiement.");
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await RefreshFromGatewayAsync(existing, cancellationToken);
                if (existing.Status == MissionPaymentRequestStatus.Pending
                    && string.IsNullOrWhiteSpace(existing.ExternalPaymentRequestId)
                    && string.IsNullOrWhiteSpace(existing.RedirectUrl))
                {
                    await SendToGatewayAsync(existing, cancellationToken);
                }

                return ClientMissionPaymentResult.Ok(ToResponse(existing));
            }
        }

        if (mission.CustomerConfirmedAt is not null)
        {
            return ClientMissionPaymentResult.Invalid("Cette mission est deja payee.");
        }

        if (mission.Status != MissionStatus.Accepted
            || mission.QuoteStatus != MissionQuoteStatus.Submitted
            || mission.CompanyId is null
            || mission.ProviderId is null)
        {
            return ClientMissionPaymentResult.Invalid(
                "Le paiement sera disponible lorsque le prestataire aura accepte la mission.");
        }

        var paymentMethod = await db.CustomerPaymentMethods
            .Include(item => item.PaymentProvider)
            .FirstOrDefaultAsync(item => item.Id == request.PaymentMethodId, cancellationToken);
        if (paymentMethod is null || paymentMethod.CustomerId != customerId || !paymentMethod.IsActive)
        {
            return ClientMissionPaymentResult.Invalid("Le moyen de paiement selectionne est invalide.");
        }

        var providerCode = ResolveJekoProviderCode(paymentMethod);
        if (providerCode is null)
        {
            return ClientMissionPaymentResult.Invalid(
                "Ce moyen de paiement n'est pas encore compatible avec Jeko.");
        }

        mission.SelectCustomerPaymentMethod(paymentMethod);
        var serviceAmount = mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? mission.FinalTotalAmount ?? 0;
        if (serviceAmount <= 0)
        {
            return ClientMissionPaymentResult.Invalid("Aucun montant valide n'est disponible pour cette mission.");
        }

        var pricing = await pricingService.CalculateAsync(mission, serviceAmount, cancellationToken);
        var requestedAmount = GrossUp(pricing.CustomerTotalAmount, gateway.FeeRateBasisPoints);
        var providerFeeAmount = requestedAmount - pricing.CustomerTotalAmount;
        var reference = $"WELE-{mission.MissionNumber}-{Guid.NewGuid():N}";
        var payment = new MissionPaymentRequest(
            mission.Id,
            customerId,
            paymentMethod.Id,
            reference.Length <= 120 ? reference : reference[..120],
            providerCode,
            pricing.CustomerTotalAmount,
            providerFeeAmount,
            requestedAmount,
            pricing.Currency,
            DateTimeOffset.UtcNow.AddMinutes(5));
        db.MissionPaymentRequests.Add(payment);

        // La reference est durable avant l'appel externe : un timeout ne doit jamais creer
        // une seconde reference et un risque de double debit.
        await db.SaveChangesAsync(cancellationToken);
        await SendToGatewayAsync(payment, cancellationToken);
        return ClientMissionPaymentResult.Ok(ToResponse(payment));
    }

    public async Task<ClientMissionPaymentResult> GetAsync(
        Guid customerId,
        Guid missionId,
        Guid paymentRequestId,
        CancellationToken cancellationToken)
    {
        var payment = await db.MissionPaymentRequests
            .FirstOrDefaultAsync(item => item.Id == paymentRequestId && item.MissionId == missionId, cancellationToken);
        if (payment is null)
        {
            return ClientMissionPaymentResult.NotFound("Demande de paiement introuvable.");
        }

        if (payment.CustomerId != customerId)
        {
            return ClientMissionPaymentResult.Forbidden("Ce paiement n'appartient pas au client connecte.");
        }

        if (payment.Status == MissionPaymentRequestStatus.Pending)
        {
            await RefreshFromGatewayAsync(payment, cancellationToken);
            if (payment.Status == MissionPaymentRequestStatus.Pending && payment.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                payment.MarkError("La demande de paiement Jeko a expire. Relancez le paiement.");
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return ClientMissionPaymentResult.Ok(ToResponse(payment));
    }

    public async Task<bool> ApplyExternalStatusAsync(
        string? externalPaymentRequestId,
        string? reference,
        string status,
        string? message,
        string? externalTransactionId,
        int? receivedAmount,
        string? receivedCurrency,
        CancellationToken cancellationToken)
    {
        MissionPaymentRequest? payment = null;
        if (!string.IsNullOrWhiteSpace(reference))
        {
            payment = await db.MissionPaymentRequests
                .FirstOrDefaultAsync(item => item.Reference == reference, cancellationToken);
        }

        if (payment is null && !string.IsNullOrWhiteSpace(externalPaymentRequestId))
        {
            payment = await db.MissionPaymentRequests
                .FirstOrDefaultAsync(item => item.ExternalPaymentRequestId == externalPaymentRequestId, cancellationToken);
        }

        if (payment is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(externalPaymentRequestId))
        {
            payment.AttachGatewayResponse(externalPaymentRequestId, payment.RedirectUrl);
        }

        await ApplyStatusAsync(
            payment,
            status,
            message,
            externalTransactionId,
            receivedAmount,
            receivedCurrency,
            cancellationToken);
        return true;
    }

    private async Task SendToGatewayAsync(
        MissionPaymentRequest payment,
        CancellationToken cancellationToken)
    {
        var result = await gateway.CreateAsync(
            new ClientPaymentGatewayRequest(
                payment.Id,
                payment.MissionId,
                payment.Reference,
                payment.ProviderCode,
                payment.RequestedAmount,
                payment.Currency),
            cancellationToken);

        if (!result.Accepted && result.IsDefinitive)
        {
            payment.MarkError(result.Message ?? "Jeko a refuse la demande de paiement.", result.ExternalTransactionId);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        payment.AttachGatewayResponse(result.ExternalPaymentRequestId, result.RedirectUrl, result.ExpiresAt);
        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            payment.RecordPendingIssue(result.Message);
        }

        await ApplyStatusAsync(
            payment,
            result.Status,
            result.Message,
            result.ExternalTransactionId,
            result.Amount,
            result.Currency,
            cancellationToken);
    }

    private async Task RefreshFromGatewayAsync(
        MissionPaymentRequest payment,
        CancellationToken cancellationToken)
    {
        if (!gateway.IsEnabled || string.IsNullOrWhiteSpace(payment.ExternalPaymentRequestId))
        {
            return;
        }

        var result = await gateway.GetStatusAsync(payment.ExternalPaymentRequestId, cancellationToken);
        if (!result.Accepted && !result.IsDefinitive)
        {
            return;
        }

        payment.AttachGatewayResponse(
            result.ExternalPaymentRequestId ?? payment.ExternalPaymentRequestId,
            result.RedirectUrl ?? payment.RedirectUrl,
            result.ExpiresAt);
        await ApplyStatusAsync(
            payment,
            result.Status,
            result.Message,
            result.ExternalTransactionId,
            result.Amount,
            result.Currency,
            cancellationToken);
    }

    private async Task ApplyStatusAsync(
        MissionPaymentRequest payment,
        string status,
        string? message,
        string? externalTransactionId,
        int? receivedAmount,
        string? receivedCurrency,
        CancellationToken cancellationToken)
    {
        switch (NormalizeStatus(status))
        {
            case MissionPaymentRequestStatus.Success:
                if (!receivedAmount.HasValue || string.IsNullOrWhiteSpace(receivedCurrency))
                {
                    payment.RecordPendingIssue(
                        "Jeko indique un succes sans montant complet. Confirmation securisee en attente.");
                    await db.SaveChangesAsync(cancellationToken);
                    break;
                }

                if (receivedAmount.Value != payment.RequestedAmount
                    || !string.Equals(receivedCurrency, payment.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    payment.MarkError(
                        $"Montant Jeko incoherent : {receivedAmount.Value} {receivedCurrency.Trim()} recus, "
                        + $"{payment.RequestedAmount} {payment.Currency} attendus.",
                        externalTransactionId);
                    await db.SaveChangesAsync(cancellationToken);
                    break;
                }

                payment.MarkSuccess(externalTransactionId);
                await confirmationService.ConfirmVerifiedPaymentAsync(
                    payment.MissionId,
                    externalTransactionId ?? payment.ExternalPaymentRequestId ?? payment.Reference,
                    cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                break;
            case MissionPaymentRequestStatus.Error:
                payment.MarkError(message ?? "Le paiement Jeko a echoue.", externalTransactionId);
                await db.SaveChangesAsync(cancellationToken);
                break;
            default:
                if (!string.IsNullOrWhiteSpace(message))
                {
                    payment.RecordPendingIssue(message);
                }

                await db.SaveChangesAsync(cancellationToken);
                break;
        }
    }

    private static int GrossUp(int desiredNetAmount, int feeRateBasisPoints)
    {
        if (desiredNetAmount <= 0 || feeRateBasisPoints <= 0)
        {
            return Math.Max(0, desiredNetAmount);
        }

        var denominator = 10_000 - Math.Clamp(feeRateBasisPoints, 0, 9_999);
        return checked((int)Math.Ceiling(desiredNetAmount * 10_000m / denominator));
    }

    private static string? ResolveJekoProviderCode(CustomerPaymentMethod method)
    {
        var code = method.PaymentProvider?.Code?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            return method.Method == PaymentMethod.Card ? "card" : null;
        }

        return code switch
        {
            "wave" => "wave",
            "orange" or "orange-money" => "orange",
            "mtn" or "mtn-momo" or "mtn-money" => "mtn",
            "moov" or "moov-money" => "moov",
            "djamo" => "djamo",
            "card" or "bank-card" => "card",
            _ => null
        };
    }

    private static MissionPaymentRequestStatus NormalizeStatus(string status) => status.Trim().ToLowerInvariant() switch
    {
        "success" or "successful" or "paid" or "completed" => MissionPaymentRequestStatus.Success,
        "error" or "failed" or "rejected" => MissionPaymentRequestStatus.Error,
        _ => MissionPaymentRequestStatus.Pending
    };

    private static ClientMissionPaymentResponse ToResponse(MissionPaymentRequest payment) =>
        new(
            payment.Id,
            payment.MissionId,
            payment.Reference,
            payment.Status.ToString(),
            payment.ProviderCode,
            payment.CommercialAmount,
            payment.ProviderFeeAmount,
            payment.RequestedAmount,
            payment.Currency,
            payment.RedirectUrl,
            payment.ExpiresAt,
            payment.CompletedAt,
            payment.FailureMessage);
}

public sealed record ClientMissionPaymentResult(
    ClientMissionPaymentResultStatus Status,
    ClientMissionPaymentResponse? Response,
    string? Message)
{
    public bool IsSuccess => Status == ClientMissionPaymentResultStatus.Ok;
    public static ClientMissionPaymentResult Ok(ClientMissionPaymentResponse response) => new(ClientMissionPaymentResultStatus.Ok, response, null);
    public static ClientMissionPaymentResult Invalid(string message) => new(ClientMissionPaymentResultStatus.Invalid, null, message);
    public static ClientMissionPaymentResult Forbidden(string message) => new(ClientMissionPaymentResultStatus.Forbidden, null, message);
    public static ClientMissionPaymentResult NotFound(string message) => new(ClientMissionPaymentResultStatus.NotFound, null, message);
    public static ClientMissionPaymentResult Unavailable(string message) => new(ClientMissionPaymentResultStatus.Unavailable, null, message);
}

public enum ClientMissionPaymentResultStatus
{
    Ok,
    Invalid,
    Forbidden,
    NotFound,
    Unavailable
}

public sealed record ClientMissionPaymentPreviewResult(
    ClientMissionPaymentResultStatus Status,
    ClientMissionPaymentPreviewResponse? Response,
    string? Message)
{
    public bool IsSuccess => Status == ClientMissionPaymentResultStatus.Ok;
    public static ClientMissionPaymentPreviewResult Ok(ClientMissionPaymentPreviewResponse response) => new(ClientMissionPaymentResultStatus.Ok, response, null);
    public static ClientMissionPaymentPreviewResult Invalid(string message) => new(ClientMissionPaymentResultStatus.Invalid, null, message);
    public static ClientMissionPaymentPreviewResult Forbidden(string message) => new(ClientMissionPaymentResultStatus.Forbidden, null, message);
    public static ClientMissionPaymentPreviewResult NotFound(string message) => new(ClientMissionPaymentResultStatus.NotFound, null, message);
}
