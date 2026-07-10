namespace HomeService.Contracts.Companies;

public sealed record CompanyApplicationSummaryResponse(
    Guid Id,
    string CompanyName,
    string City,
    string ContactName,
    string Email,
    string PhoneNumber,
    string Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? LastReminderSentAt,
    DateTimeOffset? ActivationEmailSentAt,
    int DocumentCount,
    int PendingDocumentCount,
    IReadOnlyList<CompanyApplicationDocumentSummaryResponse> Documents);
