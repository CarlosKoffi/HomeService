namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalSessionResponse(
    Guid CompanyId,
    string CompanyName,
    string UserName,
    string Email,
    DateTimeOffset AuthenticatedAt,
    string Token);
