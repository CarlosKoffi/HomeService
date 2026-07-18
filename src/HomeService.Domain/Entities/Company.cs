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
    public string? LegalForm { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public string? TaxIdentificationNumber { get; private set; }
    public string? City { get; private set; }
    public string? Address { get; private set; }
    public string? InterventionZones { get; private set; }
    public string? PlannedServices { get; private set; }
    public string? WavePaymentNumber { get; private set; }
    public string? OrangeMoneyPaymentNumber { get; private set; }
    public CompanyStatus Status { get; private set; } = CompanyStatus.PendingReview;
    public CompanyAssignmentMode AssignmentMode { get; private set; } = CompanyAssignmentMode.SelfManaged;
    public bool AcceptsInterimApplications { get; private set; }
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

    public void SetInterimApplications(bool acceptsInterimApplications)
    {
        AcceptsInterimApplications = acceptsInterimApplications;
        Touch();
    }

    public void UpdateCompanyInformation(
        string name,
        string? legalForm,
        string? registrationNumber,
        string? taxIdentificationNumber,
        string? city,
        string? address)
    {
        Name = CleanRequired(name);
        LegalForm = Clean(legalForm);
        RegistrationNumber = Clean(registrationNumber);
        TaxIdentificationNumber = Clean(taxIdentificationNumber);
        City = Clean(city);
        Address = Clean(address);
        Touch();
    }

    public void UpdateContact(string phoneNumber, string? email)
    {
        PhoneNumber = CleanRequired(phoneNumber);
        Email = Clean(email);
        Touch();
    }

    public void UpdateOperations(string? interventionZones, string? plannedServices)
    {
        InterventionZones = Clean(interventionZones);
        PlannedServices = Clean(plannedServices);
        Touch();
    }

    public void UpdatePayment(string? wavePaymentNumber, string? orangeMoneyPaymentNumber)
    {
        WavePaymentNumber = Clean(wavePaymentNumber);
        OrangeMoneyPaymentNumber = Clean(orangeMoneyPaymentNumber);
        Touch();
    }

    private static string CleanRequired(string value)
    {
        var cleaned = Clean(value);
        if (cleaned is null)
        {
            throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));
        }

        return cleaned;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
