namespace HomeService.Contracts.Companies;

public sealed record RegisterCompanyRequest(
    string Name,
    string PhoneNumber,
    string? Email);
