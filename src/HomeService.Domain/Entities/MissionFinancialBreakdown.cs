using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionFinancialBreakdown : AuditableEntity
{
    private MissionFinancialBreakdown()
    {
    }

    public MissionFinancialBreakdown(
        Guid missionId,
        MissionFinancialLineType lineType,
        string label,
        int amount,
        string currency,
        int sortOrder)
    {
        MissionId = missionId;
        LineType = lineType;
        Label = label.Trim();
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
        SortOrder = Math.Max(0, sortOrder);
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public MissionFinancialLineType LineType { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public int Amount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public int SortOrder { get; private set; }

    public void Update(string label, int amount, string currency, int sortOrder)
    {
        Label = label.Trim();
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "XOF" : currency.Trim().ToUpperInvariant();
        SortOrder = Math.Max(0, sortOrder);
        Touch();
    }
}
