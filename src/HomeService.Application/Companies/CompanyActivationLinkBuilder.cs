namespace HomeService.Application.Companies;

public static class CompanyActivationLinkBuilder
{
    public static string Build(string baseUrl, Guid applicationId, string token)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Base URL is required.", nameof(baseUrl));
        }

        return $"{baseUrl.Trim().TrimEnd('/')}/activate-company/{applicationId:D}?token={Uri.EscapeDataString(token)}";
    }
}
