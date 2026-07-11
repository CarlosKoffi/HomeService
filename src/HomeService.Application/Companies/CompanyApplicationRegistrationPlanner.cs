using HomeService.Contracts.Companies;

namespace HomeService.Application.Companies;

public static class CompanyApplicationRegistrationPlanner
{
    public static CompanyApplicationRegistrationPlan Build(RegisterCompanyRequest request)
    {
        var validationErrors = CompanyApplicationValidator.Validate(request);
        var serviceNames = request.Services
            .Where(service => !string.IsNullOrWhiteSpace(service))
            .Select(service => service.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CompanyApplicationRegistrationPlan(
            request.Email.Trim().ToLowerInvariant(),
            serviceNames,
            validationErrors);
    }
}
