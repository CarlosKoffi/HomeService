using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderProfile : AuditableEntity
{
    private readonly List<ProviderService> _services = [];
    private readonly List<ProviderDocument> _documents = [];

    private ProviderProfile()
    {
    }

    public ProviderProfile(
        Guid companyId,
        string firstName,
        string lastName,
        string phoneNumber,
        DateOnly dateOfBirth,
        string address,
        ProviderEmploymentType employmentType,
        int yearsOfExperience,
        decimal? missionLatitude,
        decimal? missionLongitude,
        int missionRadiusKm)
    {
        CompanyId = companyId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = phoneNumber.Trim();
        DateOfBirth = dateOfBirth;
        Address = address.Trim();
        EmploymentType = employmentType;
        YearsOfExperience = yearsOfExperience;
        MissionLatitude = missionLatitude;
        MissionLongitude = missionLongitude;
        MissionRadiusKm = missionRadiusKm;
    }

    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public DateOnly? DateOfBirth { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public ProviderEmploymentType EmploymentType { get; private set; } = ProviderEmploymentType.CompanyEmployee;
    public int YearsOfExperience { get; private set; }
    public decimal? MissionLatitude { get; private set; }
    public decimal? MissionLongitude { get; private set; }
    public int MissionRadiusKm { get; private set; } = 5;
    public ProviderStatus Status { get; private set; } = ProviderStatus.Invited;
    public bool IsAvailable { get; private set; }
    public decimal? CurrentLatitude { get; private set; }
    public decimal? CurrentLongitude { get; private set; }
    public IReadOnlyCollection<ProviderService> Services => _services;
    public IReadOnlyCollection<ProviderDocument> Documents => _documents;
    public string FullName => $"{FirstName} {LastName}".Trim();

    public void UpdateEmploymentType(ProviderEmploymentType employmentType)
    {
        EmploymentType = employmentType;
        Touch();
    }

    public void AttachDocument(ProviderDocument document)
    {
        _documents.Add(document);
        Touch();
    }

    public void AddService(Guid serviceId, ExperienceLevel experienceLevel, int hourlyRateAmount, string currency)
    {
        if (_services.Any(service => service.ServiceId == serviceId && service.IsActive))
        {
            return;
        }

        _services.Add(new ProviderService(Id, CompanyId, serviceId, experienceLevel, YearsOfExperience, hourlyRateAmount, currency));
        Touch();
    }

    public void Deactivate()
    {
        Status = ProviderStatus.Inactive;
        IsAvailable = false;
        Touch();
    }

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
