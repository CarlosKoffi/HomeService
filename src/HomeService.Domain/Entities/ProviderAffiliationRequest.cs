using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderAffiliationRequest : AuditableEntity
{
    private ProviderAffiliationRequest()
    {
    }

    public ProviderAffiliationRequest(Guid providerId, Guid companyId, string? message)
    {
        ProviderId = providerId;
        CompanyId = companyId;
        Message = message?.Trim();
    }

    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public ProviderAffiliationRequestStatus Status { get; private set; } = ProviderAffiliationRequestStatus.Pending;
    public string? Message { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; private set; }
    public bool CandidateMetAndTestedByCompany { get; private set; }
    public bool CompetencyValidatedByCompany { get; private set; }
    public bool SeriousnessValidatedByCompany { get; private set; }
    public bool PunctualityValidatedByCompany { get; private set; }
    public DateTimeOffset? CompanyValidationAttestedAt { get; private set; }

    public void Approve(
        string? reviewNote,
        bool candidateMetAndTestedByCompany,
        bool competencyValidatedByCompany,
        bool seriousnessValidatedByCompany,
        bool punctualityValidatedByCompany)
    {
        if (Status != ProviderAffiliationRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending affiliation requests can be approved.");
        }

        if (!candidateMetAndTestedByCompany
            || !competencyValidatedByCompany
            || !seriousnessValidatedByCompany
            || !punctualityValidatedByCompany)
        {
            throw new InvalidOperationException(
                "L'entreprise doit confirmer avoir recu et teste le candidat, puis verifie ses competences, son serieux et sa ponctualite.");
        }

        var now = DateTimeOffset.UtcNow;
        Status = ProviderAffiliationRequestStatus.Approved;
        ReviewNote = reviewNote?.Trim();
        ReviewedAt = now;
        CandidateMetAndTestedByCompany = true;
        CompetencyValidatedByCompany = true;
        SeriousnessValidatedByCompany = true;
        PunctualityValidatedByCompany = true;
        CompanyValidationAttestedAt = now;
        Touch();
    }

    public void Reject(string? reviewNote)
    {
        if (Status != ProviderAffiliationRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending affiliation requests can be rejected.");
        }

        Status = ProviderAffiliationRequestStatus.Rejected;
        ReviewNote = reviewNote?.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Cancel(string? reviewNote)
    {
        if (Status != ProviderAffiliationRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending affiliation requests can be cancelled.");
        }

        Status = ProviderAffiliationRequestStatus.Cancelled;
        ReviewNote = reviewNote?.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
        Touch();
    }
}
