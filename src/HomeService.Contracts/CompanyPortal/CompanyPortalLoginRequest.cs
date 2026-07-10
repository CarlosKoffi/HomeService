namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyPortalLoginRequest(
    string Email,
    string Password,
    bool RememberMe);
