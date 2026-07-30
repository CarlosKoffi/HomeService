using HomeService.Contracts.Clients;

namespace HomeService.Application.Clients;

public sealed class ClientHomeService(
    ClientProfileService profileService,
    ClientMissionListService missionListService,
    ClientNotificationInboxService notificationInboxService)
{
    public async Task<ClientHomeResponse> GetAsync(
        Domain.Entities.CustomerProfile customer,
        CancellationToken cancellationToken)
    {
        var missionList = await missionListService.ListAsync(customer.Id, null, null, cancellationToken);
        var missions = missionList.IsSuccess ? missionList.Missions : [];
        var unreadCount = await notificationInboxService.CountUnreadAsync(customer.Id, cancellationToken);

        return new ClientHomeResponse(
            profileService.ToMe(customer),
            unreadCount.UnreadCount,
            missions.FirstOrDefault(IsHighlightMission) ?? missions.FirstOrDefault(),
            missions.Take(8).ToList(),
            BuildQuickActions());
    }

    private static bool IsHighlightMission(ClientMissionListItemResponse mission)
    {
        return mission.Status is not ("Completed" or "Cancelled" or "Resolved")
            || mission.PrimaryAction is "AcceptQuote" or "ValidateCompletion";
    }

    private static IReadOnlyList<ClientHomeQuickActionResponse> BuildQuickActions()
    {
        return
        [
            new("CreateMission", "Nouvelle demande", "Trouver une entreprise disponible.", "/missions/new"),
            new("MyMissions", "Mes demandes", "Suivre vos interventions.", "/missions"),
            new("Messages", "Messages", "Echanger avec l'entreprise ou le prestataire.", "/messages"),
            new("Profile", "Profil", "Adresses, moyens de paiement et preferences.", "/profile")
        ];
    }
}
