using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CompanyCommissionTier : AuditableEntity
{
    private CompanyCommissionTier()
    {
    }

    public CompanyCommissionTier(string name, int minimumMissionCount, int rateBasisPoints, int sortOrder)
    {
        Update(name, minimumMissionCount, rateBasisPoints, sortOrder, true);
    }

    public string Name { get; private set; } = string.Empty;
    public int MinimumMissionCount { get; private set; }
    public int RateBasisPoints { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(string name, int minimumMissionCount, int rateBasisPoints, int sortOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Le nom du palier est obligatoire.", nameof(name));
        }

        Name = name.Trim();
        MinimumMissionCount = Math.Max(1, minimumMissionCount);
        RateBasisPoints = Math.Clamp(rateBasisPoints, 0, 10000);
        SortOrder = Math.Max(0, sortOrder);
        IsActive = isActive;
        Touch();
    }
}
