using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CompanyApplication : AuditableEntity
{
    private readonly List<CompanyApplicationDocument> _documents = [];
    private readonly List<CompanyApplicationService> _requestedServices = [];
    private readonly List<CompanyApplicationStatusHistory> _statusHistory = [];
    private readonly List<CompanyActivationToken> _activationTokens = [];

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
        int? estimatedProviderCount,
        Guid? id = null)
    {
        if (id.HasValue)
        {
            Id = id.Value;
        }

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

    public Guid? CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public string CompanyName { get; private set; } = string.Empty;
    public string? RegistrationNumber { get; private set; }
    public string? LegalForm { get; private set; }
    public string? TaxIdentificationNumber { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string ContactName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? PlannedServices { get; private set; }
    public string? InterventionZones { get; private set; }
    public string? WavePaymentNumber { get; private set; }
    public string? OrangeMoneyPaymentNumber { get; private set; }
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
    public IReadOnlyCollection<CompanyApplicationStatusHistory> StatusHistory => _statusHistory;
    public IReadOnlyCollection<CompanyActivationToken> ActivationTokens => _activationTokens;

    public void MarkUnderReview(string? changedBy = null)
    {
        SetStatus(CompanyApplicationStatus.UnderReview, null, changedBy);
        Touch();
    }

    public void RequestMoreInformation(string note, string? changedBy = null)
    {
        ReviewNote = note.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        SetStatus(CompanyApplicationStatus.MoreInformationRequested, ReviewNote, changedBy);
        Touch();
    }

    public void Approve(string? changedBy = null)
    {
        ReviewedAt = DateTimeOffset.UtcNow;
        SetStatus(CompanyApplicationStatus.Approved, null, changedBy);
        Touch();
    }

    public CompanyActivationToken CreateActivationToken(
        string tokenHash,
        DateTimeOffset expiresAt,
        string activationLink,
        string? changedBy = null,
        bool revokeExistingTokens = true)
    {
        if (Status != CompanyApplicationStatus.Approved && Status != CompanyApplicationStatus.ActivationSent)
        {
            throw new InvalidOperationException("Seule une demande approuvee peut recevoir un token d'activation.");
        }

        if (revokeExistingTokens)
        {
            foreach (var existingToken in _activationTokens.Where(token => token.IsActive))
            {
                existingToken.Revoke("Remplace par un nouveau token d'activation.");
            }
        }

        var activationToken = new CompanyActivationToken(Id, tokenHash, expiresAt, activationLink);
        _activationTokens.Add(activationToken);
        ActivationEmailSentAt = DateTimeOffset.UtcNow;
        SetStatus(CompanyApplicationStatus.ActivationSent, "Lien d'activation envoye.", changedBy);
        Touch();

        return activationToken;
    }

    public void MarkReminderSent()
    {
        LastReminderSentAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Reject(string note, string? changedBy = null)
    {
        ReviewNote = note.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        SetStatus(CompanyApplicationStatus.Rejected, ReviewNote, changedBy);
        Touch();
    }

    public void Reopen(string note, string? changedBy = null)
    {
        if (Status != CompanyApplicationStatus.Rejected)
        {
            throw new InvalidOperationException("Seule une demande refusee peut etre reouverte.");
        }

        ReviewNote = note.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        SetStatus(CompanyApplicationStatus.UnderReview, ReviewNote, changedBy);
        Touch();
    }

    public void LinkApprovedCompany(Guid companyId, string? changedBy = null)
    {
        if (Status is not (CompanyApplicationStatus.Approved or CompanyApplicationStatus.ActivationSent or CompanyApplicationStatus.Activated))
        {
            throw new InvalidOperationException("La demande doit etre approuvee avant de lier une entreprise.");
        }

        CompanyId = companyId;
        Touch();
    }

    public void LinkPendingCompany(Guid companyId)
    {
        if (CompanyId is not null && CompanyId != companyId)
        {
            throw new InvalidOperationException("Cette demande est deja liee a une autre entreprise.");
        }

        CompanyId = companyId;
        Touch();
    }

    public void MarkActivated(string? changedBy = null)
    {
        if (Status != CompanyApplicationStatus.Activated)
        {
            ActivatedAt = DateTimeOffset.UtcNow;
            SetStatus(CompanyApplicationStatus.Activated, "Compte entreprise active.", changedBy);
            Touch();
        }
    }

    public void UpdateCompanyInformation(
        string companyName,
        string? legalForm,
        string? registrationNumber,
        string? taxIdentificationNumber,
        string city,
        string? address)
    {
        CompanyName = CleanRequired(companyName);
        LegalForm = Clean(legalForm);
        RegistrationNumber = Clean(registrationNumber);
        TaxIdentificationNumber = Clean(taxIdentificationNumber);
        City = CleanRequired(city);
        Address = Clean(address);
        Touch();
    }

    public void UpdateContact(string contactName, string email, string phoneNumber)
    {
        ContactName = CleanRequired(contactName);
        Email = CleanRequired(email);
        PhoneNumber = CleanRequired(phoneNumber);
        Touch();
    }

    public void UpdateOperations(string? interventionZones, string? plannedServices)
    {
        InterventionZones = Clean(interventionZones);
        PlannedServices = Clean(plannedServices);
        Touch();
    }

    public void UpdatePayment(string? wavePaymentNumber, string? orangeMoneyPaymentNumber)
    {
        WavePaymentNumber = Clean(wavePaymentNumber);
        OrangeMoneyPaymentNumber = Clean(orangeMoneyPaymentNumber);
        Touch();
    }

    private void SetStatus(CompanyApplicationStatus newStatus, string? note, string? changedBy)
    {
        if (Status == newStatus)
        {
            return;
        }

        var previousStatus = Status;
        Status = newStatus;
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
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
