using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CustomerPaymentMethod : AuditableEntity
{
    private CustomerPaymentMethod()
    {
    }

    public CustomerPaymentMethod(
        Guid customerId,
        PaymentMethod method,
        string label,
        string? maskedReference,
        bool isDefault)
    {
        CustomerId = customerId;
        Method = method;
        Label = CleanRequired(label, 120);
        MaskedReference = Clean(maskedReference, 120);
        IsDefault = isDefault;
        IsActive = true;
    }

    public Guid CustomerId { get; private set; }
    public CustomerProfile? Customer { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string? MaskedReference { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(PaymentMethod method, string label, string? maskedReference, bool isDefault)
    {
        Method = method;
        Label = CleanRequired(label, 120);
        MaskedReference = Clean(maskedReference, 120);
        IsDefault = isDefault;
        IsActive = true;
        Touch();
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        Touch();
    }

    public void Disable()
    {
        IsActive = false;
        Touch();
    }

    private static string CleanRequired(string value, int maxLength)
    {
        var cleaned = Clean(value, maxLength);
        if (cleaned is null)
        {
            throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));
        }

        return cleaned;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
