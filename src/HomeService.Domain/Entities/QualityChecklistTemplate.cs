using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class QualityChecklistTemplate : AuditableEntity
{
    private readonly List<QualityChecklistItem> _items = [];

    private QualityChecklistTemplate()
    {
    }

    public QualityChecklistTemplate(
        Guid serviceId,
        Guid? servicePrestationId,
        string name,
        string? description,
        int version = 1)
    {
        ServiceId = serviceId;
        ServicePrestationId = servicePrestationId;
        Name = CleanRequired(name, 160);
        Description = Clean(description, 800);
        Version = Math.Max(1, version);
    }

    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public Guid? ServicePrestationId { get; private set; }
    public ServicePrestation? ServicePrestation { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Version { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<QualityChecklistItem> Items => _items;

    public void Update(string name, string? description)
    {
        Name = CleanRequired(name, 160);
        Description = Clean(description, 800);
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    private static string CleanRequired(string value, int maxLength)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        if (cleaned.Length == 0) throw new ArgumentException("Une valeur est obligatoire.", nameof(value));
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
