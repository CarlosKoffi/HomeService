namespace HomeService.Contracts.Branding;

public sealed record CountryBrandingResponse(
    string CountryIsoCode,
    string CountryName,
    string BrandName,
    string PrimaryColor,
    string SecondaryColor,
    string AccentColor,
    string HeroTitle,
    string HeroSubtitle,
    string? HeroImageUrl,
    string MotifStyle);
