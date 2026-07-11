using HomeService.Contracts.ProviderPortal;

namespace HomeService.Application.ProviderPortal;

public static class ProviderLocationPayloadValidator
{
    public static string? Validate(ProviderLocationVerificationRequest request)
    {
        if (request.Latitude is null || request.Longitude is null)
        {
            return "La position GPS est obligatoire.";
        }

        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return "La position GPS est invalide.";
        }

        if (request.AccuracyMeters is null or <= 0)
        {
            return "La precision GPS est obligatoire.";
        }

        if (request.AccuracyMeters > 150)
        {
            return "La precision GPS est trop faible. Reessayez avec une meilleure localisation.";
        }

        return null;
    }
}
