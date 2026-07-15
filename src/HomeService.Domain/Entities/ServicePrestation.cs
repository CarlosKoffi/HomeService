using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class ServicePrestation : AuditableEntity
{
    private ServicePrestation()
    {
    }

    public ServicePrestation(Guid serviceId, string name, string? description, int sortOrder)
    {
        ServiceId = serviceId;
        Name = name.Trim();
        NormalizedName = Normalize(name);
        Description = description?.Trim();
        SortOrder = Math.Max(0, sortOrder);
    }

    public Guid ServiceId { get; private set; }
    public Service? Service { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Rename(string name, string? description)
    {
        Name = name.Trim();
        NormalizedName = Normalize(name);
        Description = description?.Trim();
        Touch();
    }

    public void MoveTo(int sortOrder)
    {
        SortOrder = Math.Max(0, sortOrder);
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

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
