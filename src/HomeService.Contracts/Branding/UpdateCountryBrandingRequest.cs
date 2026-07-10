namespace HomeService.Contracts.Branding;

public sealed record UpdateCountryBrandingRequest(
    string BrandName,
    string PrimaryColor,
    string SecondaryColor,
    string AccentColor,
    string HeroTitle,
    string HeroSubtitle,
    string? HeroImageUrl,
    string MotifStyle);
