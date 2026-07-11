using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class Service : AuditableEntity
{
    private Service()
    {
    }

    public Service(string name, string? description, Guid? createdByCompanyId)
    {
        Name = name.Trim();
        NormalizedName = Normalize(name);
        Description = description?.Trim();
        CreatedByCompanyId = createdByCompanyId;
        Status = createdByCompanyId.HasValue ? ServiceStatus.PendingReview : ServiceStatus.Approved;
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string IconName { get; private set; } = "sparkles";
    public int NormalPriceAmount { get; private set; } = 1500;
    public int PremiumPriceAmount { get; private set; } = 2500;
    public string Currency { get; private set; } = "XOF";
    public bool RequiresPortfolio { get; private set; }
    public int MinimumPortfolioItems { get; private set; }
    public bool RequiresCompletionPhoto { get; private set; }
    public bool RequiresBeforeAfterPhotos { get; private set; }
    public bool RequiresDiploma { get; private set; }
    public bool RequiresAdminApprovalBeforeAssignment { get; private set; }
    public Guid? CreatedByCompanyId { get; private set; }
    public ServiceStatus Status { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void UpdatePricing(int normalPriceAmount, int premiumPriceAmount, string currency)
    {
        NormalPriceAmount = Math.Max(0, normalPriceAmount);
        PremiumPriceAmount = Math.Max(NormalPriceAmount, premiumPriceAmount);
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
        Touch();
    }

    public void UpdateIcon(string? iconName)
    {
        IconName = string.IsNullOrWhiteSpace(iconName) ? "sparkles" : iconName.Trim().ToLowerInvariant();
        Touch();
    }

    public void UpdateAssignmentRequirements(
        bool requiresPortfolio,
        int minimumPortfolioItems,
        bool requiresCompletionPhoto,
        bool requiresBeforeAfterPhotos,
        bool requiresDiploma,
        bool requiresAdminApprovalBeforeAssignment)
    {
        RequiresPortfolio = requiresPortfolio;
        MinimumPortfolioItems = requiresPortfolio ? Math.Max(1, minimumPortfolioItems) : 0;
        RequiresCompletionPhoto = requiresCompletionPhoto;
        RequiresBeforeAfterPhotos = requiresBeforeAfterPhotos;
        RequiresDiploma = requiresDiploma;
        RequiresAdminApprovalBeforeAssignment = requiresAdminApprovalBeforeAssignment;
        Touch();
    }

    public void Approve()
    {
        Status = ServiceStatus.Approved;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
