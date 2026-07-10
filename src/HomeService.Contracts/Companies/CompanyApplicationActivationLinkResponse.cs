namespace HomeService.Contracts.Companies;

public sealed record CompanyApplicationActivationLinkResponse(
    Guid Id,
    string Status,
    DateTimeOffset? ActivationEmailSentAt,
    DateTimeOffset? LastReminderSentAt,
    DateTimeOffset ExpiresAt,
    string ActivationLink);
