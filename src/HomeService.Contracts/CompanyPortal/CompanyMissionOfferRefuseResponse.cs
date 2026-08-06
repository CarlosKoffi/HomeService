namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyMissionOfferRefuseResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    string Message);
