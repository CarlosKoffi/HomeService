namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalMeResponse(
    Guid CompanyId,
    string CompanyName,
    string UserName,
    string Email);
