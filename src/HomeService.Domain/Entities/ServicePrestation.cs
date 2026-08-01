using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class ServicePrestation : AuditableEntity
{
    private readonly List<ServiceOption> _options = [];
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
        string? currency = null,
        string? illustrationUrl = null)
    {
        ServiceId = serviceId;
        Name = name.Trim();
        NormalizedName = Normalize(name);
        Description = description?.Trim();
        SortOrder = Math.Max(0, sortOrder);
        UpdatePricing(normalPriceAmount, premiumPriceAmount, currency);
        IllustrationUrl = NormalizeUrl(illustrationUrl);
    }

    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public int NormalPriceAmount { get; private set; }
    public int PremiumPriceAmount { get; private set; }
    public int PriceMinAmount { get; private set; }
    public int PriceMaxAmount { get; private set; }
    public bool IsFixedPrice { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public string? IllustrationUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<ServiceOption> Options => _options;

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
        UpdatePriceRange(normalPriceAmount, premiumPriceAmount, currency);
    }

    public void UpdatePriceRange(int priceMinAmount, int priceMaxAmount, string? currency, bool isFixedPrice = false)
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

    public ServiceOption AddOption(
        string name,
        string? description,
        int sortOrder,
        int priceMinAmount,
        int priceMaxAmount,
        bool isFixedPrice,
        string? currency = null)
    {
        var normalizedName = Normalize(name);
        var existing = _options.FirstOrDefault(option => option.NormalizedName == normalizedName);
        if (existing is not null)
        {
            existing.Update(name, description, sortOrder, priceMinAmount, priceMaxAmount, isFixedPrice, currency);
            existing.Activate();
            Touch();
            return existing;
        }

        var option = new ServiceOption(Id, name, description, sortOrder, priceMinAmount, priceMaxAmount, isFixedPrice, currency);
        _options.Add(option);
        Touch();
        return option;
    }

    public void UpdateIllustration(string? illustrationUrl)
    {
        IllustrationUrl = NormalizeUrl(illustrationUrl);
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
        return CatalogNameNormalizer.Normalize(value);
    }

    private static string? NormalizeUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
