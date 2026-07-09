using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class Country : AuditableEntity
{
    private Country()
    {
    }

    public Country(string isoCode, string name, string currencyCode, bool isLaunchCountry = false)
    {
        IsoCode = isoCode.Trim().ToUpperInvariant();
        Name = name.Trim();
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        IsLaunchCountry = isLaunchCountry;
    }

    public string IsoCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = "XOF";
    public bool IsActive { get; private set; } = true;
    public bool IsLaunchCountry { get; private set; }
}
