using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class Service : AuditableEntity
{
    private readonly List<ServicePrestation> _prestations = [];

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
    public string? IconUrl { get; private set; }
    public string? ImageUrl { get; private set; }
    public ServiceDisplayCategory DisplayCategory { get; private set; } = ServiceDisplayCategory.Home;
    public int NormalPriceAmount { get; private set; } = 1500;
    public int PremiumPriceAmount { get; private set; } = 2500;
    public int PriceMinAmount { get; private set; } = 1500;
    public int PriceMaxAmount { get; private set; } = 2500;
    public bool IsFixedPrice { get; private set; }
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
    public IReadOnlyCollection<ServicePrestation> Prestations => _prestations;

    public void UpdateDetails(string name, string? description, string? iconName)
    {
        Name = name.Trim();
        NormalizedName = Normalize(name);
        Description = description?.Trim();
        UpdateIcon(iconName);
        Touch();
    }

    public void UpdateMedia(string? iconUrl, string? imageUrl)
    {
        IconUrl = NormalizeUrl(iconUrl);
        ImageUrl = NormalizeUrl(imageUrl);
        Touch();
    }

    public void UpdateDisplayCategory(ServiceDisplayCategory category)
    {
        DisplayCategory = category;
        Touch();
    }

    public ServicePrestation AddPrestation(
        string name,
        string? description,
        int sortOrder,
        int normalPriceAmount = 0,
        int premiumPriceAmount = 0,
        string? currency = null,
        string? illustrationUrl = null)
    {
        var normalizedName = Normalize(name);
        var existing = _prestations.FirstOrDefault(prestation => prestation.NormalizedName == normalizedName);
        if (existing is not null)
        {
            existing.Rename(name, description);
            existing.MoveTo(sortOrder);
            existing.UpdatePricing(normalPriceAmount, premiumPriceAmount, currency);
            existing.UpdateIllustration(illustrationUrl);
            existing.Activate();
            Touch();
            return existing;
        }

        var prestation = new ServicePrestation(Id, name, description, sortOrder, normalPriceAmount, premiumPriceAmount, currency, illustrationUrl);
        _prestations.Add(prestation);
        Touch();
        return prestation;
    }

    public void UpdatePricing(int normalPriceAmount, int premiumPriceAmount, string currency)
    {
        UpdatePriceRange(normalPriceAmount, premiumPriceAmount, currency);
    }

    public void UpdatePriceRange(int priceMinAmount, int priceMaxAmount, string currency, bool isFixedPrice = false)
    {
        PriceMinAmount = Math.Max(0, priceMinAmount);
        PriceMaxAmount = Math.Max(PriceMinAmount, priceMaxAmount);
        IsFixedPrice = isFixedPrice;
        if (IsFixedPrice)
        {
            PriceMinAmount = PriceMaxAmount;
        }
        NormalPriceAmount = PriceMinAmount;
        PremiumPriceAmount = PriceMaxAmount;
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
        return CatalogNameNormalizer.Normalize(value);
    }

    private static string? NormalizeUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
