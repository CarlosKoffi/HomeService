using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class PaymentProvider : AuditableEntity
{
    private PaymentProvider() { }

    public PaymentProvider(string code, string name, PaymentMethod method, string? description, string? logoUrl, int sortOrder)
    {
        Update(code, name, method, description, logoUrl, sortOrder);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PaymentMethod Method { get; private set; }
    public string? Description { get; private set; }
    public string? LogoUrl { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string code, string name, PaymentMethod method, string? description, string? logoUrl, int sortOrder)
    {
        Code = CleanRequired(code, 64).ToLowerInvariant().Replace(' ', '-');
        Name = CleanRequired(name, 120);
        Method = method;
        Description = Clean(description, 300);
        LogoUrl = Clean(logoUrl, 500);
        SortOrder = Math.Max(0, sortOrder);
        Touch();
    }

    public void SetActive(bool active) { IsActive = active; Touch(); }

    private static string CleanRequired(string value, int maxLength)
        => Clean(value, maxLength) ?? throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
