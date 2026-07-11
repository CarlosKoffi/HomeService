namespace HomeService.Application.Companies;

public static class CompanyActivationTokenLifetimeResolver
{
    public static int ResolveHours(string? configuredValue)
    {
        return int.TryParse(configuredValue, out var hours) && hours is >= 1 and <= 720
            ? hours
            : 72;
    }
}
