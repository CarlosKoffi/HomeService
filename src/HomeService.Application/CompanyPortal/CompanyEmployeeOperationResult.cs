using HomeService.Domain.Entities;

namespace HomeService.Application.CompanyPortal;

public sealed record CompanyEmployeeOperationResult(
    CompanyEmployeeOperationStatus Status,
    ProviderProfile? Provider,
    object? Before,
    object? After,
    string? Message)
{
    public static CompanyEmployeeOperationResult Ok(ProviderProfile provider, object? before, object? after)
        => new(CompanyEmployeeOperationStatus.Ok, provider, before, after, null);

    public static CompanyEmployeeOperationResult NotFound(string message = "Employe introuvable.")
        => new(CompanyEmployeeOperationStatus.NotFound, null, null, null, message);
}
