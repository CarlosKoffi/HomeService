namespace HomeService.Contracts.Companies;

public sealed record CompanyApplicationDetailResponse(
    Guid Id,
    string CompanyName,
    string? RegistrationNumber,
    string City,
    string? Address,
    string ContactName,
    string Email,
    string PhoneNumber,
    string? PlannedServices,
    int? EstimatedProviderCount,
    string Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? LastReminderSentAt,
    DateTimeOffset? ActivationEmailSentAt,
    string? ReviewNote,
    IReadOnlyList<CompanyApplicationDocumentResponse> Documents);
