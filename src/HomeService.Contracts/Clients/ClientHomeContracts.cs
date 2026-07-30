namespace HomeService.Contracts.Clients;

public sealed record ClientHomeResponse(
    ClientMeResponse Customer,
    int UnreadNotificationCount,
    ClientMissionListItemResponse? HighlightMission,
    IReadOnlyList<ClientMissionListItemResponse> RecentMissions,
    IReadOnlyList<ClientHomeQuickActionResponse> QuickActions);

public sealed record ClientHomeQuickActionResponse(
    string Code,
    string Label,
    string Description,
    string Route);
