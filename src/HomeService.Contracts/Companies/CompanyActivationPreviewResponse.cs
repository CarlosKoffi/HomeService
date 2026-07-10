namespace HomeService.Contracts.Companies;

public sealed record CompanyActivationPreviewResponse(
    Guid ApplicationId,
    string CompanyName,
    string Email,
    DateTimeOffset ExpiresAt);

