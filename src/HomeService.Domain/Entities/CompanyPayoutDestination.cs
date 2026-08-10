using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CompanyPayoutDestination : AuditableEntity
{
    private CompanyPayoutDestination()
    {
    }

    public CompanyPayoutDestination(
        Guid companyId,
        CompanyPayoutMethod method,
        string label,
        string beneficiaryName,
        string providerCode,
        string protectedDetails,
        string maskedIdentifier,
        bool isDefault)
    {
        CompanyId = companyId;
        Method = method;
        Label = CleanRequired(label);
        BeneficiaryName = CleanRequired(beneficiaryName);
        ProviderCode = CleanRequired(providerCode).ToLowerInvariant();
        ProtectedDetails = CleanRequired(protectedDetails);
        MaskedIdentifier = CleanRequired(maskedIdentifier);
        IsDefault = isDefault;
    }

    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public CompanyPayoutMethod Method { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string BeneficiaryName { get; private set; } = string.Empty;
    public string ProviderCode { get; private set; } = string.Empty;
    public string ProtectedDetails { get; private set; } = string.Empty;
    public string MaskedIdentifier { get; private set; } = string.Empty;
    public string? ExternalContactId { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsVerified { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void MarkDefault(bool isDefault)
    {
        IsDefault = isDefault;
        Touch();
    }

    public void MarkVerified(string? externalContactId = null)
    {
        IsVerified = true;
        ExternalContactId = string.IsNullOrWhiteSpace(externalContactId) ? ExternalContactId : externalContactId.Trim();
        Touch();
    }

    public void Disable()
    {
        IsActive = false;
        IsDefault = false;
        Touch();
    }

    private static string CleanRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("La valeur est obligatoire.", nameof(value));
        }

        return value.Trim();
    }
}
