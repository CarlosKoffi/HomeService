namespace HomeService.Contracts.Companies;

public sealed record RegisterCompanyRequest(
    string CompanyName,
    string? RegistrationNumber,
    string City,
    string? Address,
    string ContactName,
    string Email,
    string PhoneNumber,
    string Password,
    string ConfirmPassword,
    IReadOnlyList<string> Services,
    int? EstimatedProviderCount);
