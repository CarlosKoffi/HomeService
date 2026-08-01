using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class ServiceOption : AuditableEntity
{
    private ServiceOption()
    {
    }

    public ServiceOption(
        Guid servicePrestationId,
        string name,
        string? description,
        int sortOrder,
        int priceMinAmount,
        int priceMaxAmount,
        bool isFixedPrice,
        string? currency = null)
    {
        ServicePrestationId = servicePrestationId;
        Update(name, description, sortOrder, priceMinAmount, priceMaxAmount, isFixedPrice, currency);
    }

    public Guid ServicePrestationId { get; private set; }
    public ServicePrestation? ServicePrestation { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public int PriceMinAmount { get; private set; }
    public int PriceMaxAmount { get; private set; }
    public bool IsFixedPrice { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public bool IsActive { get; private set; } = true;

    public void Update(
        string name,
        string? description,
        int sortOrder,
        int priceMinAmount,
        int priceMaxAmount,
        bool isFixedPrice,
        string? currency)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Le nom de l'option est obligatoire.", nameof(name));
        }

        Name = name.Trim();
        NormalizedName = CatalogNameNormalizer.Normalize(name);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SortOrder = Math.Max(0, sortOrder);
        PriceMinAmount = Math.Max(0, priceMinAmount);
        PriceMaxAmount = Math.Max(PriceMinAmount, priceMaxAmount);
        IsFixedPrice = isFixedPrice;
        if (IsFixedPrice)
        {
            PriceMinAmount = PriceMaxAmount;
        }

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
}
