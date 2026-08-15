using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class BusinessClientProfile : AuditableEntity
{
    private readonly List<BusinessClientDocument> _documents = [];

    private BusinessClientProfile()
    {
    }

    public BusinessClientProfile(Guid customerProfileId)
    {
        CustomerProfileId = customerProfileId;
    }

    public Guid CustomerProfileId { get; private set; }
    public CustomerProfile? CustomerProfile { get; private set; }
    public string LegalName { get; private set; } = string.Empty;
    public string? TradeName { get; private set; }
    public string? LegalForm { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public string? TaxIdentificationNumber { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = "CI";
    public string RepresentativeName { get; private set; } = string.Empty;
    public string RepresentativeRole { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string ContactPhone { get; private set; } = string.Empty;
    public BusinessClientStatus Status { get; private set; } = BusinessClientStatus.Draft;
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewNote { get; private set; }
    public IReadOnlyCollection<BusinessClientDocument> Documents => _documents;

    public bool CanEdit => Status is BusinessClientStatus.Draft or BusinessClientStatus.MoreInformationRequested;

    public void Update(
        string legalName,
        string? tradeName,
        string? legalForm,
        string? registrationNumber,
        string? taxIdentificationNumber,
        string address,
        string city,
        string? countryCode,
        string representativeName,
        string representativeRole,
        string contactEmail,
        string contactPhone)
    {
        if (!CanEdit)
        {
            throw new InvalidOperationException("Le dossier ne peut plus etre modifie pendant son examen.");
        }

        LegalName = CleanRequired(legalName);
        TradeName = Clean(tradeName);
        LegalForm = Clean(legalForm);
        RegistrationNumber = Clean(registrationNumber);
        TaxIdentificationNumber = Clean(taxIdentificationNumber);
        Address = CleanRequired(address);
        City = CleanRequired(city);
        CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "CI" : countryCode.Trim().ToUpperInvariant();
        RepresentativeName = CleanRequired(representativeName);
        RepresentativeRole = CleanRequired(representativeRole);
        ContactEmail = CleanRequired(contactEmail);
        ContactPhone = CleanRequired(contactPhone);
        ReviewNote = null;
        Touch();
    }

    public void Submit()
    {
        if (!CanEdit)
        {
            throw new InvalidOperationException("Ce dossier a deja ete soumis.");
        }

        if (string.IsNullOrWhiteSpace(LegalName)
            || string.IsNullOrWhiteSpace(Address)
            || string.IsNullOrWhiteSpace(City)
            || string.IsNullOrWhiteSpace(RepresentativeName)
            || string.IsNullOrWhiteSpace(RepresentativeRole)
            || string.IsNullOrWhiteSpace(ContactEmail)
            || string.IsNullOrWhiteSpace(ContactPhone))
        {
            throw new InvalidOperationException("Completez les informations obligatoires avant de soumettre le dossier.");
        }

        Status = BusinessClientStatus.Submitted;
        SubmittedAt = DateTimeOffset.UtcNow;
        ReviewNote = null;
        Touch();
    }

    public void MarkUnderReview()
    {
        if (Status != BusinessClientStatus.Submitted)
        {
            throw new InvalidOperationException("Seul un dossier soumis peut passer en examen.");
        }

        Status = BusinessClientStatus.UnderReview;
        Touch();
    }

    public void RequestMoreInformation(string note)
    {
        Status = BusinessClientStatus.MoreInformationRequested;
        ReviewNote = CleanRequired(note);
        ReviewedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Approve(string? note)
    {
        Status = BusinessClientStatus.Approved;
        ReviewNote = Clean(note);
        ReviewedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Reject(string note)
    {
        Status = BusinessClientStatus.Rejected;
        ReviewNote = CleanRequired(note);
        ReviewedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    private static string CleanRequired(string value)
    {
        var cleaned = Clean(value);
        return cleaned ?? throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
