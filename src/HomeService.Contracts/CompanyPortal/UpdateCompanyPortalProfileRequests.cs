namespace HomeService.Contracts.CompanyPortal;

public sealed record UpdateCompanyPortalCompanyInfoRequest(
    string CompanyName,
    string? LegalForm,
    string? RegistrationNumber,
    string? TaxIdentificationNumber,
    string City,
    string? Address);

public sealed record UpdateCompanyPortalContactRequest(
    string ContactName,
    string Email,
    string PhoneNumber);

public sealed record UpdateCompanyPortalOperationsRequest(
    string? InterventionZones,
    string? PlannedServices);

public sealed record UpdateCompanyPortalPaymentRequest(
    string? WavePaymentNumber,
    string? OrangeMoneyPaymentNumber);
