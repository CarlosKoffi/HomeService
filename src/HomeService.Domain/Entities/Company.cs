using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class Company : AuditableEntity
{
    private readonly List<ProviderProfile> _providers = [];

    private Company()
    {
    }

    public Company(string name, string phoneNumber, string? email)
    {
        Name = name.Trim();
        PhoneNumber = phoneNumber.Trim();
        Email = email?.Trim();
    }

    public string Name { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public CompanyStatus Status { get; private set; } = CompanyStatus.PendingReview;
    public IReadOnlyCollection<ProviderProfile> Providers => _providers;

    public void Approve()
    {
        Status = CompanyStatus.Approved;
        Touch();
    }

    public void Suspend()
    {
        Status = CompanyStatus.Suspended;
        Touch();
    }
}
