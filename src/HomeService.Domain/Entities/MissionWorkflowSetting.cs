using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class MissionWorkflowSetting : AuditableEntity
{
    private MissionWorkflowSetting()
    {
    }

    public MissionWorkflowSetting(
        string key,
        string label,
        string description,
        string unit,
        int value,
        int minimumValue,
        int maximumValue,
        int sortOrder)
    {
        Key = key.Trim();
        Label = label.Trim();
        Description = description.Trim();
        Unit = unit.Trim();
        MinimumValue = Math.Max(0, minimumValue);
        MaximumValue = Math.Max(MinimumValue, maximumValue);
        Value = Math.Clamp(value, MinimumValue, MaximumValue);
        SortOrder = sortOrder;
    }

    public string Key { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public int Value { get; private set; }
    public int MinimumValue { get; private set; }
    public int MaximumValue { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public bool IsWithinRange(int value)
    {
        return value >= MinimumValue && value <= MaximumValue;
    }

    public void UpdateValue(int value)
    {
        if (!IsWithinRange(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Value must be between {MinimumValue} and {MaximumValue}.");
        }

        Value = value;
        Touch();
    }
}
