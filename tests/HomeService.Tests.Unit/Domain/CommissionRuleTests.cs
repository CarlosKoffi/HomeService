using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class CommissionRuleTests
{
    [Fact]
    public void CalculateAmount_AddsPercentageAndFixedAmount()
    {
        var rule = new CommissionRule(
            "Commission mise en relation",
            CommissionRuleTarget.PlatformConnection,
            rateBasisPoints: 1500,
            fixedAmount: 250,
            currency: "xof");

        var amount = rule.CalculateAmount(10000);

        Assert.Equal(1750, amount);
        Assert.Equal("XOF", rule.Currency);
    }

    [Fact]
    public void UpdatePricing_ClampsRateAndNormalizesCurrency()
    {
        var rule = new CommissionRule(
            "Surcommission affectation Kaza",
            CommissionRuleTarget.KazaAssignmentExtra,
            rateBasisPoints: 500,
            fixedAmount: 0,
            currency: "XOF",
            assignmentSource: MissionAssignmentSource.Kaza);

        rule.UpdatePricing(rateBasisPoints: 12000, fixedAmount: -100, currency: " xof ");

        Assert.Equal(10000, rule.RateBasisPoints);
        Assert.Equal(0, rule.FixedAmount);
        Assert.Equal("XOF", rule.Currency);
    }
}
