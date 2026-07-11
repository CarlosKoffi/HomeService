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

    public void Approve(string? reviewNote)
    {
        if (Status != ProviderAffiliationRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending affiliation requests can be approved.");
        }

        Status = ProviderAffiliationRequestStatus.Approved;
        ReviewNote = reviewNote?.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
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
}
