using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderProfile : AuditableEntity
{
    private readonly List<ProviderService> _services = [];
    private readonly List<ProviderDocument> _documents = [];
    private readonly List<ProviderCandidateService> _candidateServices = [];

    private ProviderProfile()
    {
    }

    public ProviderProfile(
        Guid companyId,
        string firstName,
        string lastName,
        string phoneNumber,
        string? email,
        DateOnly dateOfBirth,
        string address,
        ProviderGender gender,
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
        Email = NormalizeEmail(email);
        DateOfBirth = dateOfBirth;
        Address = address.Trim();
        Gender = gender;
        EmploymentType = employmentType;
        YearsOfExperience = yearsOfExperience;
        MissionLatitude = missionLatitude;
        MissionLongitude = missionLongitude;
        MissionRadiusKm = missionRadiusKm;
    }

    public ProviderProfile(
        string firstName,
        string lastName,
        string phoneNumber,
        string? email,
        DateOnly dateOfBirth,
        string address,
        ProviderGender gender,
        int yearsOfExperience,
        decimal? missionLatitude,
        decimal? missionLongitude,
        int missionRadiusKm)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = phoneNumber.Trim();
        Email = NormalizeEmail(email);
        DateOfBirth = dateOfBirth;
        Address = address.Trim();
        Gender = gender;
        EmploymentType = ProviderEmploymentType.TemporaryWorker;
        YearsOfExperience = yearsOfExperience;
        MissionLatitude = missionLatitude;
        MissionLongitude = missionLongitude;
        MissionRadiusKm = missionRadiusKm;
        Status = ProviderStatus.InterimCandidate;
        RegistrationSource = ProviderRegistrationSource.SelfRegistration;
    }

    public Guid? CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public ProviderGender Gender { get; private set; } = ProviderGender.Unspecified;
    public ProviderEmploymentType EmploymentType { get; private set; } = ProviderEmploymentType.CompanyEmployee;
    public int YearsOfExperience { get; private set; }
    public decimal? MissionLatitude { get; private set; }
    public decimal? MissionLongitude { get; private set; }
    public int MissionRadiusKm { get; private set; } = 5;
    public ProviderStatus Status { get; private set; } = ProviderStatus.Invited;
    public ProviderRegistrationSource RegistrationSource { get; private set; } = ProviderRegistrationSource.CompanyInvitation;
    public string? PasswordHash { get; private set; }
    public bool IsAvailable { get; private set; }
    public decimal? CurrentLatitude { get; private set; }
    public decimal? CurrentLongitude { get; private set; }
    public IReadOnlyCollection<ProviderService> Services => _services;
    public IReadOnlyCollection<ProviderDocument> Documents => _documents;
    public IReadOnlyCollection<ProviderCandidateService> CandidateServices => _candidateServices;
    public string FullName => $"{FirstName} {LastName}".Trim();

    public void UpdateEmploymentType(ProviderEmploymentType employmentType)
    {
        EmploymentType = employmentType;
        Touch();
    }

    public void UpdateCompanyProfile(
        string firstName,
        string lastName,
        string phoneNumber,
        string? email,
        DateOnly dateOfBirth,
        string address,
        ProviderGender gender,
        ProviderEmploymentType employmentType,
        int yearsOfExperience,
        decimal? missionLatitude,
        decimal? missionLongitude,
        int missionRadiusKm)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = phoneNumber.Trim();
        Email = NormalizeEmail(email);
        DateOfBirth = dateOfBirth;
        Address = address.Trim();
        Gender = gender;
        EmploymentType = employmentType;
        YearsOfExperience = yearsOfExperience;
        MissionLatitude = missionLatitude;
        MissionLongitude = missionLongitude;
        MissionRadiusKm = missionRadiusKm;
        Touch();
    }

    public void AttachDocument(ProviderDocument document)
    {
        _documents.Add(document);
        Touch();
    }

    public void AddService(Guid serviceId, ExperienceLevel experienceLevel, ProviderServicePriceTier priceTier = ProviderServicePriceTier.Normal)
    {
        if (CompanyId is null)
        {
            return;
        }

        if (_services.Any(service => service.ServiceId == serviceId && service.IsActive))
        {
            return;
        }

        _services.Add(new ProviderService(Id, CompanyId.Value, serviceId, experienceLevel, YearsOfExperience, priceTier));
        Touch();
    }

    public void SyncCandidateServices(IEnumerable<(Guid ServiceId, ExperienceLevel ExperienceLevel, int YearsOfExperience)> services)
    {
        var requestedServices = services
            .GroupBy(service => service.ServiceId)
            .Select(group => group.Last())
            .ToList();
        var requestedIds = requestedServices.Select(service => service.ServiceId).ToHashSet();

        foreach (var existingService in _candidateServices.Where(service => service.IsActive && !requestedIds.Contains(service.ServiceId)))
        {
            existingService.Deactivate();
        }

        foreach (var requestedService in requestedServices)
        {
            var existingService = _candidateServices.FirstOrDefault(service => service.ServiceId == requestedService.ServiceId);
            if (existingService is null)
            {
                _candidateServices.Add(new ProviderCandidateService(Id, requestedService.ServiceId, requestedService.ExperienceLevel, requestedService.YearsOfExperience));
                continue;
            }

            existingService.Update(requestedService.ExperienceLevel, requestedService.YearsOfExperience);
        }

        Touch();
    }

    public void SyncCompanyServices(IEnumerable<(Guid ServiceId, ExperienceLevel ExperienceLevel, int YearsOfExperience, ProviderServicePriceTier PriceTier)> services)
    {
        if (CompanyId is null)
        {
            throw new InvalidOperationException("Provider must be attached to a company before company services can be validated.");
        }

        var requestedServices = services
            .GroupBy(service => service.ServiceId)
            .Select(group => group.Last())
            .ToList();
        var requestedIds = requestedServices.Select(service => service.ServiceId).ToHashSet();

        foreach (var existingService in _services.Where(service => service.IsActive && !requestedIds.Contains(service.ServiceId)))
        {
            existingService.Deactivate();
        }

        foreach (var requestedService in requestedServices)
        {
            var existingService = _services.FirstOrDefault(service => service.ServiceId == requestedService.ServiceId);
            if (existingService is null)
            {
                _services.Add(new ProviderService(
                    Id,
                    CompanyId.Value,
                    requestedService.ServiceId,
                    requestedService.ExperienceLevel,
                    requestedService.YearsOfExperience,
                    requestedService.PriceTier));
                continue;
            }

            existingService.UpdateCompanyExperience(
                requestedService.ExperienceLevel,
                requestedService.YearsOfExperience,
                requestedService.PriceTier);
        }

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

    public void SetPortalPassword(string passwordHash)
    {
        PasswordHash = passwordHash.Trim();
        Touch();
    }

    public void ActivateFromCompanyInvitation(string passwordHash)
    {
        SetPortalPassword(passwordHash);
        Status = ProviderStatus.Approved;
        IsAvailable = false;
        Touch();
    }

    public void MarkAsInterimCandidate()
    {
        Status = ProviderStatus.InterimCandidate;
        EmploymentType = ProviderEmploymentType.TemporaryWorker;
        IsAvailable = false;
        Touch();
    }

    public void AttachToCompanyAsTemporaryWorker(Guid companyId)
    {
        CompanyId = companyId;
        EmploymentType = ProviderEmploymentType.TemporaryWorker;
        Status = ProviderStatus.Approved;
        IsAvailable = false;
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

    private static string? NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
