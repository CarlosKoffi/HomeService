using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class AdminFinancialApproval : AuditableEntity
{
    private AdminFinancialApproval()
    {
    }

    public AdminFinancialApproval(
        Guid adminUserId,
        string operation,
        Guid resourceId,
        string payloadHash,
        DateTimeOffset approvedAt,
        DateTimeOffset expiresAt)
    {
        AdminUserId = adminUserId;
        Operation = operation.Trim();
        ResourceId = resourceId;
        PayloadHash = payloadHash.Trim();
        ApprovedAt = approvedAt;
        ExpiresAt = expiresAt;
    }

    public Guid AdminUserId { get; private set; }
    public AdminUser? AdminUser { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public string PayloadHash { get; private set; } = string.Empty;
    public DateTimeOffset ApprovedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void MarkCompleted(DateTimeOffset completedAt)
    {
        CompletedAt = completedAt;
        Touch();
    }
}
