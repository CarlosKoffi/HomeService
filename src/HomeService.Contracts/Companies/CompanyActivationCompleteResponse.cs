namespace HomeService.Contracts.Companies;

public sealed record CompanyActivationCompleteResponse(
    Guid CompanyId,
    string CompanyName,
    string Email);
