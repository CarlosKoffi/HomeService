using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CustomerAddress : AuditableEntity
{
    private CustomerAddress()
    {
    }

    public CustomerAddress(
        Guid customerId,
        string label,
        string addressLine,
        decimal? latitude,
        decimal? longitude,
        bool isDefault)
    {
        CustomerId = customerId;
        Label = CleanRequired(label, 80);
        AddressLine = CleanRequired(addressLine, 300);
        Latitude = latitude;
        Longitude = longitude;
        IsDefault = isDefault;
    }

    public Guid CustomerId { get; private set; }
    public CustomerProfile? Customer { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string AddressLine { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public bool IsDefault { get; private set; }

    public void Update(string label, string addressLine, decimal? latitude, decimal? longitude, bool isDefault)
    {
        Label = CleanRequired(label, 80);
        AddressLine = CleanRequired(addressLine, 300);
        Latitude = latitude;
        Longitude = longitude;
        IsDefault = isDefault;
        Touch();
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        Touch();
    }

    private static string CleanRequired(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));
        }

        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
