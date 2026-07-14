using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CmsMenuItem : AuditableEntity
{
    private CmsMenuItem()
    {
    }

    public CmsMenuItem(Guid menuId, string label, CmsMenuItemTargetType targetType, int position)
    {
        MenuId = menuId;
        Label = NormalizeLabel(label);
        TargetType = targetType;
        Position = position < 0 ? throw new ArgumentOutOfRangeException(nameof(position)) : position;
    }

    public Guid MenuId { get; private set; }
    public CmsMenu? Menu { get; private set; }
    public Guid? ParentMenuItemId { get; private set; }
    public CmsMenuItem? ParentMenuItem { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public Guid? PageId { get; private set; }
    public CmsPage? Page { get; private set; }
    public string? TargetValue { get; private set; }
    public CmsMenuItemTargetType TargetType { get; private set; }
    public int Position { get; private set; }
    public string? IconName { get; private set; }
    public bool OpenInNewTab { get; private set; }
    public bool IsActive { get; private set; } = true;

    private static string NormalizeLabel(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A CMS menu item label is required.", nameof(value))
            : value.Trim();
    }
}
