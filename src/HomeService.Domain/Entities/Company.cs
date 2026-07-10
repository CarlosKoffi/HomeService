using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class Company : AuditableEntity
{
    private readonly List<ProviderProfile> _providers = [];
    private readonly List<CompanyApplication> _applications = [];

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
    public CompanyAssignmentMode AssignmentMode { get; private set; } = CompanyAssignmentMode.SelfManaged;
    public IReadOnlyCollection<ProviderProfile> Providers => _providers;
    public IReadOnlyCollection<CompanyApplication> Applications => _applications;

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

    public void ChangeAssignmentMode(CompanyAssignmentMode assignmentMode)
    {
        AssignmentMode = assignmentMode;
        Touch();
    }
}
