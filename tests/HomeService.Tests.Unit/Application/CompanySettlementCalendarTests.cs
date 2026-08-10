using HomeService.Application.CompanyPortal;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanySettlementCalendarTests
{
    [Theory]
    [InlineData(2026, 8, 1, 2026, 8, 15)]
    [InlineData(2026, 8, 14, 2026, 8, 15)]
    [InlineData(2026, 8, 15, 2026, 9, 1)]
    [InlineData(2026, 8, 31, 2026, 9, 1)]
    public void FortnightlyEligibility_UsesTwoClosedCycles(
        int year,
        int month,
        int day,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var releasedAt = new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero);

        var result = CompanySettlementCalendar.GetEligibilityDate(releasedAt, CompanySettlementFrequency.Fortnightly);

        Assert.Equal(new DateTimeOffset(expectedYear, expectedMonth, expectedDay, 0, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void MonthlyEligibility_IsAlwaysTheFirstDayOfNextMonth()
    {
        var releasedAt = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

        var result = CompanySettlementCalendar.GetEligibilityDate(releasedAt, CompanySettlementFrequency.Monthly);

        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), result);
    }
}
