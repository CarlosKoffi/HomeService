using System.Text.RegularExpressions;
using HomeService.Contracts.Branding;

namespace HomeService.Application.Branding;

public static class CountryBrandingValidator
{
    public static string? Validate(UpdateCountryBrandingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BrandName) || request.BrandName.Trim().Length > 120)
        {
            return "Le nom de marque est obligatoire et limite a 120 caracteres.";
        }

        if (!IsHexColor(request.PrimaryColor) || !IsHexColor(request.SecondaryColor) || !IsHexColor(request.AccentColor))
        {
            return "Les couleurs doivent etre au format hexadecimal, par exemple #f97316.";
        }

        if (string.IsNullOrWhiteSpace(request.HeroTitle) || request.HeroTitle.Trim().Length > 220)
        {
            return "Le titre hero est obligatoire et limite a 220 caracteres.";
        }

        if (string.IsNullOrWhiteSpace(request.HeroSubtitle) || request.HeroSubtitle.Trim().Length > 600)
        {
            return "Le sous-titre hero est obligatoire et limite a 600 caracteres.";
        }

        if (!string.IsNullOrWhiteSpace(request.HeroImageUrl)
            && (!Uri.TryCreate(request.HeroImageUrl, UriKind.Absolute, out var heroUri)
                || heroUri.Scheme is not ("http" or "https")))
        {
            return "L'image hero doit etre une URL http ou https valide.";
        }

        if (string.IsNullOrWhiteSpace(request.MotifStyle) || request.MotifStyle.Trim().Length > 80)
        {
            return "Le motif visuel est obligatoire et limite a 80 caracteres.";
        }

        return null;
    }

    public static bool IsHexColor(string value)
    {
        return Regex.IsMatch(value.Trim(), "^#[0-9a-fA-F]{6}$");
    }
}
