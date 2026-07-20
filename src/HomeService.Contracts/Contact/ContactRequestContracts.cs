namespace HomeService.Contracts.Contact;

public sealed record SubmitContactRequest(
    string Source,
    string FullName,
    string? CompanyName,
    string PhoneNumber,
    string? Email,
    string Subject,
    string Message);

public sealed record SubmitContactResponse(Guid Id, string Message);

public sealed record AdminContactRequestResponse(
    Guid Id,
    string Source,
    string Status,
    string FullName,
    string? CompanyName,
    string PhoneNumber,
    string? Email,
    string Subject,
    string Message,
    string? AdminNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);

public sealed record UpdateContactRequestStatusRequest(string? Note);
