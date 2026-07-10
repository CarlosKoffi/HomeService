namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalLoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid CompanyId,
    string CompanyName,
    string UserName,
    string Email);
