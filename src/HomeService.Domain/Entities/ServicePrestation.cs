using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class ServicePrestation : AuditableEntity
{
    private ServicePrestation()
    {
    }

    public ServicePrestation(
        Guid serviceId,
        string name,
        string? description,
        int sortOrder,
        int normalPriceAmount = 0,
        int premiumPriceAmount = 0,
        string? currency = null)
    {
        ServiceId = serviceId;
        Name = name.Trim();
        NormalizedName = Normalize(name);
        Description = description?.Trim();
        SortOrder = Math.Max(0, sortOrder);
        UpdatePricing(normalPriceAmount, premiumPriceAmount, currency);
    }

    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public int NormalPriceAmount { get; private set; }
    public int PremiumPriceAmount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public bool IsActive { get; private set; } = true;

    public void Rename(string name, string? description)
    {
        Name = name.Trim();
        NormalizedName = Normalize(name);
        Description = description?.Trim();
        Touch();
    }

    public void MoveTo(int sortOrder)
    {
        SortOrder = Math.Max(0, sortOrder);
        Touch();
    }

    public void UpdatePricing(int normalPriceAmount, int premiumPriceAmount, string? currency)
    {
        NormalPriceAmount = Math.Max(0, normalPriceAmount);
        PremiumPriceAmount = Math.Max(NormalPriceAmount, premiumPriceAmount);
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
