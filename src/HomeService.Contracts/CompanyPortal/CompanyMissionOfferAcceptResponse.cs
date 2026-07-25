namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyMissionOfferAcceptResponse(
    Guid MissionId,
    string MissionNumber,
    string Status,
    string Message);
