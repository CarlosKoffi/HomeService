using HomeService.Application.CompanyPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class PaymentPeriodCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void GetStart_WhenPeriodIsYear_ReturnsFirstDayOfYear()
    {
        var start = PaymentPeriodCalculator.GetStart("year", Now);

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), start);
    }

    [Fact]
    public void GetStart_WhenPeriodIsWeek_ReturnsSevenDaysBeforeNow()
    {
        var start = PaymentPeriodCalculator.GetStart("week", Now);

        Assert.Equal(Now.AddDays(-7), start);
    }

    [Theory]
    [InlineData("month")]
    [InlineData("")]
    [InlineData("unknown")]
    public void GetStart_WhenPeriodIsMonthOrUnknown_ReturnsFirstDayOfMonth(string period)
    {
        var start = PaymentPeriodCalculator.GetStart(period, Now);

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), start);
    }
}
