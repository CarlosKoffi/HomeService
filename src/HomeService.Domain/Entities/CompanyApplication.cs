using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CompanyApplication : AuditableEntity
{
    private readonly List<CompanyApplicationDocument> _documents = [];
    private readonly List<CompanyApplicationService> _requestedServices = [];

    private CompanyApplication()
    {
    }

    public CompanyApplication(
        string companyName,
        string? registrationNumber,
        string city,
        string? address,
        string contactName,
        string email,
        string phoneNumber,
        string? plannedServices,
        int? estimatedProviderCount)
    {
        CompanyName = companyName.Trim();
        RegistrationNumber = registrationNumber?.Trim();
        City = city.Trim();
        Address = address?.Trim();
        ContactName = contactName.Trim();
        Email = email.Trim();
        PhoneNumber = phoneNumber.Trim();
        PlannedServices = plannedServices?.Trim();
        EstimatedProviderCount = estimatedProviderCount;
    }

    public string CompanyName { get; private set; } = string.Empty;
    public string? RegistrationNumber { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string ContactName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? PlannedServices { get; private set; }
    public int? EstimatedProviderCount { get; private set; }
    public CompanyApplicationStatus Status { get; private set; } = CompanyApplicationStatus.Submitted;
    public DateTimeOffset? SubmittedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; private set; }
    public DateTimeOffset? LastReminderSentAt { get; private set; }
    public DateTimeOffset? ActivationEmailSentAt { get; private set; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public string? ReviewNote { get; private set; }
    public IReadOnlyCollection<CompanyApplicationDocument> Documents => _documents;
    public IReadOnlyCollection<CompanyApplicationService> RequestedServices => _requestedServices;

    public void MarkUnderReview()
    {
        Status = CompanyApplicationStatus.UnderReview;
        Touch();
    }

    public void RequestMoreInformation(string note)
    {
        Status = CompanyApplicationStatus.MoreInformationRequested;
        ReviewNote = note.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Approve()
    {
        Status = CompanyApplicationStatus.Approved;
        ReviewedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkActivationSent()
    {
        Status = CompanyApplicationStatus.ActivationSent;
        ActivationEmailSentAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkReminderSent()
    {
        LastReminderSentAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Reject(string note)
    {
        Status = CompanyApplicationStatus.Rejected;
        ReviewNote = note.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
