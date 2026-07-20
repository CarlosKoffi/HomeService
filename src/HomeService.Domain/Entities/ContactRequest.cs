using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ContactRequest : AuditableEntity
{
    private ContactRequest()
    {
    }

    public ContactRequest(
        ContactRequestSource source,
        string fullName,
        string? companyName,
        string phoneNumber,
        string? email,
        string subject,
        string message)
    {
        Source = source;
        FullName = CleanRequired(fullName);
        CompanyName = Clean(companyName);
        PhoneNumber = CleanRequired(phoneNumber);
        Email = Clean(email)?.ToLowerInvariant();
        Subject = CleanRequired(subject);
        Message = CleanRequired(message);
        Status = ContactRequestStatus.New;
    }

    public ContactRequestSource Source { get; private set; }
    public ContactRequestStatus Status { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string? CompanyName { get; private set; }
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? AdminNote { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public void MarkInProgress(string? adminNote)
    {
        Status = ContactRequestStatus.InProgress;
        AdminNote = Clean(adminNote);
        ProcessedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Close(string? adminNote)
    {
        Status = ContactRequestStatus.Closed;
        AdminNote = Clean(adminNote);
        ProcessedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    private static string CleanRequired(string value)
    {
        var cleaned = Clean(value);
        if (cleaned is null)
        {
            throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));
        }

        return cleaned;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
