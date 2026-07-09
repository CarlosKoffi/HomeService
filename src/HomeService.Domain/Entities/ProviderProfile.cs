using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderProfile : AuditableEntity
{
    private readonly List<ProviderService> _services = [];

    private ProviderProfile()
    {
    }

    public ProviderProfile(Guid companyId, string firstName, string lastName, string phoneNumber)
    {
        CompanyId = companyId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = phoneNumber.Trim();
    }

    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public ProviderStatus Status { get; private set; } = ProviderStatus.Invited;
    public bool IsAvailable { get; private set; }
    public decimal? CurrentLatitude { get; private set; }
    public decimal? CurrentLongitude { get; private set; }
    public IReadOnlyCollection<ProviderService> Services => _services;

    public void SubmitForReview()
    {
        Status = ProviderStatus.PendingPlatformReview;
        Touch();
    }

    public void Approve()
    {
        Status = ProviderStatus.Approved;
        Touch();
    }

    public void SuspendByCompany()
    {
        Status = ProviderStatus.SuspendedByCompany;
        IsAvailable = false;
        Touch();
    }

    public void SetAvailability(bool isAvailable, decimal? latitude, decimal? longitude)
    {
        if (Status != ProviderStatus.Approved)
        {
            throw new InvalidOperationException("Only approved providers can update availability.");
        }

        IsAvailable = isAvailable;
        CurrentLatitude = latitude;
        CurrentLongitude = longitude;
        Touch();
    }
}
