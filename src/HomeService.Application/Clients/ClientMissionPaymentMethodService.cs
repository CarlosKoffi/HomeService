using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientMissionPaymentMethodService(IAppDbContext db)
{
    public async Task<ClientMissionPaymentSelectionResult> SelectAsync(
        Guid customerId,
        Guid missionId,
        Guid paymentMethodId,
        CancellationToken cancellationToken)
    {
        var mission = await db.Missions
            .FirstOrDefaultAsync(item => item.Id == missionId && item.CustomerId == customerId, cancellationToken);
        if (mission is null)
        {
            return ClientMissionPaymentSelectionResult.NotFound("Demande introuvable.");
        }

        var paymentMethod = await db.CustomerPaymentMethods
            .FirstOrDefaultAsync(item => item.Id == paymentMethodId
                && item.CustomerId == customerId
                && item.IsActive, cancellationToken);
        if (paymentMethod is null)
        {
            return ClientMissionPaymentSelectionResult.Invalid("Ce moyen de paiement n'est plus disponible.");
        }

        mission.SelectCustomerPaymentMethod(paymentMethod);
        await db.SaveChangesAsync(cancellationToken);

        return ClientMissionPaymentSelectionResult.Ok(new ClientMissionPaymentSelectionResponse(
            mission.Id,
            paymentMethod.Id,
            paymentMethod.Method.ToString(),
            paymentMethod.Label,
            paymentMethod.MaskedReference,
            IsReadyForQuoteConfirmation: true));
    }
}

public sealed record ClientMissionPaymentSelectionResult(
    bool IsSuccess,
    bool IsNotFound,
    ClientMissionPaymentSelectionResponse? Response,
    string? Message)
{
    public static ClientMissionPaymentSelectionResult Ok(ClientMissionPaymentSelectionResponse response) =>
        new(true, false, response, null);

    public static ClientMissionPaymentSelectionResult NotFound(string message) =>
        new(false, true, null, message);

    public static ClientMissionPaymentSelectionResult Invalid(string message) =>
        new(false, false, null, message);
}
