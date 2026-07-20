using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CountryBranding : AuditableEntity
{
    private CountryBranding()
    {
    }

    public CountryBranding(
        Guid countryId,
        string brandName,
        string primaryColor,
        string secondaryColor,
        string accentColor,
        string heroTitle,
        string heroSubtitle,
        string? heroImageUrl,
        string motifStyle)
    {
        CountryId = countryId;
        Update(brandName, primaryColor, secondaryColor, accentColor, heroTitle, heroSubtitle, heroImageUrl, motifStyle);
    }

    public Guid CountryId { get; private set; }
    public Country? Country { get; private set; }
    public string BrandName { get; private set; } = "wélé";
    public string PrimaryColor { get; private set; } = "#08bfa8";
    public string SecondaryColor { get; private set; } = "#ffffff";
    public string AccentColor { get; private set; } = "#f97316";
    public string HeroTitle { get; private set; } = string.Empty;
    public string HeroSubtitle { get; private set; } = string.Empty;
    public string? HeroImageUrl { get; private set; }
    public string MotifStyle { get; private set; } = "flag-ribbon";

    public void Update(
        string brandName,
        string primaryColor,
        string secondaryColor,
        string accentColor,
        string heroTitle,
        string heroSubtitle,
        string? heroImageUrl,
        string motifStyle)
    {
        BrandName = brandName.Trim();
        PrimaryColor = primaryColor.Trim();
        SecondaryColor = secondaryColor.Trim();
        AccentColor = accentColor.Trim();
        HeroTitle = heroTitle.Trim();
        HeroSubtitle = heroSubtitle.Trim();
        HeroImageUrl = string.IsNullOrWhiteSpace(heroImageUrl) ? null : heroImageUrl.Trim();
        MotifStyle = motifStyle.Trim();
        Touch();
    }
}
