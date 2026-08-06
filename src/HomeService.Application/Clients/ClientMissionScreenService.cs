using HomeService.Contracts.Clients;

namespace HomeService.Application.Clients;

public sealed class ClientMissionScreenService(ClientMissionStatusService missionStatusService)
{
    public async Task<ClientMissionScreenResult> GetAsync(
        Guid missionId,
        string customerPhoneNumber,
        CancellationToken cancellationToken)
    {
        var statusResult = await missionStatusService.GetAsync(missionId, customerPhoneNumber, cancellationToken);
        if (!statusResult.IsSuccess || statusResult.Response is null)
        {
            return ClientMissionScreenResult.FromStatus(statusResult);
        }

        return ClientMissionScreenResult.Ok(Map(statusResult.Response));
    }

    private static ClientMissionScreenResponse Map(ClientMissionStatusResponse mission)
    {
        var currentAmount = mission.CompanyQuotedAmount ?? mission.FinalTotalAmount ?? mission.EstimatedTotalAmount;
        var title = !string.IsNullOrWhiteSpace(mission.OptionName)
            ? mission.OptionName!
            : !string.IsNullOrWhiteSpace(mission.PrestationName)
                ? mission.PrestationName!
                : mission.ServiceName ?? "Mission";
        var subtitle = string.IsNullOrWhiteSpace(mission.ServiceAddress)
            ? mission.MissionNumber
            : $"{mission.MissionNumber} - {mission.ServiceAddress}";
        var statusLabel = BuildStatusLabel(mission);
        var primaryAction = BuildPrimaryAction(mission);

        return new ClientMissionScreenResponse(
            mission.MissionId,
            mission.MissionNumber,
            title,
            subtitle,
            mission.Status,
            statusLabel.Label,
            statusLabel.Tone,
            mission.Message,
            primaryAction,
            new ClientMissionScreenPriceResponse(
                mission.StartingPriceAmount,
                mission.MaximumPriceAmount,
                currentAmount,
                mission.PartsEstimateAmount,
                mission.PartsDescription,
                mission.PlatformCommissionAmount,
                mission.CompanyPayoutAmount,
                mission.TransportFeeAmount,
                mission.Currency,
                BuildPriceLabel(mission, currentAmount)),
            mission.AssignedProvider is null
                ? null
                : new ClientMissionScreenProviderResponse(
                    mission.AssignedProvider.ProviderId,
                    mission.AssignedProvider.FullName,
                    mission.AssignedProvider.PhoneNumber,
                    mission.AssignedProvider.PhotoStoragePath,
                    mission.AssignedProvider.AverageRating,
                    mission.AssignedProvider.CompletedMissionCount,
                    mission.AssignedProvider.EstimatedArrivalMinutes,
                    mission.AssignedProvider.CurrentLatitude,
                    mission.AssignedProvider.CurrentLongitude,
                    mission.AssignedProvider.DestinationLatitude,
                    mission.AssignedProvider.DestinationLongitude,
                    mission.AssignedProvider.DistanceKm,
                    mission.AssignedProvider.CanTrackLocation),
            mission.AssignedCompany is null
                ? null
                : new ClientMissionScreenCompanyResponse(
                    mission.AssignedCompany.CompanyId,
                    mission.AssignedCompany.Name,
                    mission.AssignedCompany.PhoneNumber,
                    mission.AssignedCompany.Email),
            BuildTimeline(mission),
            mission.AdditionalQuotes,
            mission.Photos);
    }

    private static ClientMissionScreenPrimaryActionResponse BuildPrimaryAction(ClientMissionStatusResponse mission)
    {
        var actions = mission.Actions;
        var code = actions.PrimaryAction;
        return code switch
        {
            "AcceptQuote" => new ClientMissionScreenPrimaryActionResponse(code, "Accepter et payer", actions.CanAcceptQuote, actions.AmountToPayNow, mission.CustomerPaymentExpiresAt),
            "ValidateCompletion" => new ClientMissionScreenPrimaryActionResponse(code, "Valider la fin", actions.CanValidateCompletion, null, mission.CustomerCompletionValidationExpiresAt),
            "CallProvider" => new ClientMissionScreenPrimaryActionResponse(code, "Appeler le prestataire", actions.CanCallProvider, null, null),
            "CancelMission" => new ClientMissionScreenPrimaryActionResponse(code, "Annuler la demande", actions.CanCancel, null, null),
            _ => new ClientMissionScreenPrimaryActionResponse(code, "Suivre la mission", code is not null, actions.AmountToPayNow, null)
        };
    }

    private static (string Label, string Tone) BuildStatusLabel(ClientMissionStatusResponse mission)
    {
        if (mission.Status == "Cancelled")
        {
            return ("Annulee", "danger");
        }

        if (mission.Status == "Disputed")
        {
            return ("Litige ouvert", "warning");
        }

        if (mission.CustomerCompletionValidatedAt is not null)
        {
            return ("Terminee", "success");
        }

        if (mission.Status == "Completed")
        {
            return ("A valider", "warning");
        }

        if (mission.Status is "Started" or "OnTheWay")
        {
            return ("En cours", "info");
        }

        if (mission.ContactDetailsReleased)
        {
            return ("Confirmee", "success");
        }

        if (mission.Status == "Accepted" && mission.PaymentStatus == "Pending")
        {
            return ("Paiement requis", "warning");
        }

        if (mission.AssignedProvider is not null && mission.ProviderAcceptedAt is null)
        {
            return ("Confirmation prestataire", "info");
        }

        if (mission.QuoteStatus == "Submitted")
        {
            return ("Devis disponible", "primary");
        }

        if (mission.AssignedProvider is not null)
        {
            return ("Prestataire affecte", "info");
        }

        return ("Recherche en cours", "neutral");
    }

    private static string BuildPriceLabel(ClientMissionStatusResponse mission, int? currentAmount)
    {
        if (mission.QuoteStatus == "Submitted" && currentAmount is > 0)
        {
            return $"Prix propose: {currentAmount:N0} {mission.Currency}";
        }

        if (currentAmount is > 0)
        {
            return $"Montant mission: {currentAmount:N0} {mission.Currency}";
        }

        return $"A partir de {mission.StartingPriceAmount:N0} {mission.Currency}";
    }

    private static IReadOnlyList<ClientMissionScreenTimelineStepResponse> BuildTimeline(ClientMissionStatusResponse mission)
    {
        var steps = new List<ClientMissionScreenTimelineStepResponse>
        {
            Step("request", "Demande envoyee", "Votre demande a ete transmise a la plateforme.", "done", mission.CreatedAt),
            Step(
                "quote",
                "Prix confirme",
                mission.QuoteStatus == "Submitted"
                    ? "Un prix est disponible pour votre intervention."
                    : "Une entreprise analyse la demande.",
                mission.QuoteStatus is "Submitted" or "Accepted" ? "done" : "current",
                mission.CompanyQuotedAt),
            Step(
                "provider",
                "Technicien",
                mission.AssignedProvider is null ? "Aucun technicien affecte pour le moment." : $"{mission.AssignedProvider.FullName} est affecte.",
                mission.ProviderAcceptedAt is not null ? "done" : mission.AssignedProvider is not null ? "current" : "pending",
                mission.ProviderAcceptedAt),
            Step(
                "payment",
                "Paiement",
                mission.ProviderAcceptedAt is null
                    ? "Le paiement sera disponible apres la confirmation du technicien."
                    : "Validez le prix et payez pour lancer l'intervention.",
                mission.PaymentStatus is "Authorized" or "Paid"
                    ? "done"
                    : mission.ProviderAcceptedAt is not null && mission.Status == "Accepted"
                        ? "current"
                        : "pending",
                mission.CustomerConfirmedAt),
            Step(
                "completion",
                "Fin de mission",
                "Vous pourrez valider puis noter la prestation.",
                mission.CustomerCompletionValidatedAt is not null ? "done" : mission.Status == "Completed" ? "current" : "pending",
                mission.CustomerCompletionValidatedAt)
        };

        return steps;
    }

    private static ClientMissionScreenTimelineStepResponse Step(
        string code,
        string label,
        string description,
        string status,
        DateTimeOffset? completedAt)
    {
        return new ClientMissionScreenTimelineStepResponse(code, label, description, status, completedAt);
    }
}

public sealed record ClientMissionScreenResult(
    ClientMissionStatusResultStatus Status,
    ClientMissionScreenResponse? Response,
    string Message)
{
    public bool IsSuccess => Status == ClientMissionStatusResultStatus.Success;

    public static ClientMissionScreenResult Ok(ClientMissionScreenResponse response)
        => new(ClientMissionStatusResultStatus.Success, response, string.Empty);

    public static ClientMissionScreenResult FromStatus(ClientMissionStatusResult statusResult)
        => new(statusResult.Status, null, statusResult.Message);
}
