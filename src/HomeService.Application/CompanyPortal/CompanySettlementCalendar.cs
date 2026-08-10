using HomeService.Domain.Enums;

namespace HomeService.Application.CompanyPortal;

public static class CompanySettlementCalendar
{
    public static DateTimeOffset GetEligibilityDate(
        DateTimeOffset releasedAt,
        CompanySettlementFrequency frequency)
    {
        var utc = releasedAt.ToUniversalTime();
        if (frequency == CompanySettlementFrequency.Fortnightly && utc.Day <= 14)
        {
            return new DateTimeOffset(utc.Year, utc.Month, 15, 0, 0, 0, TimeSpan.Zero);
        }

        var nextMonth = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        return nextMonth;
    }

    public static (DateTimeOffset Start, DateTimeOffset End) GetClosedPeriod(
        DateTimeOffset now,
        CompanySettlementFrequency frequency)
    {
        var utc = now.ToUniversalTime();
        if (frequency == CompanySettlementFrequency.Fortnightly)
        {
            if (utc.Day >= 15)
            {
                var start = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
                return (start, start.AddDays(14).AddTicks(-1));
            }

            var previousMonth = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1);
            return (previousMonth.AddDays(14), previousMonth.AddMonths(1).AddTicks(-1));
        }

        var currentMonth = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var prior = currentMonth.AddMonths(-1);
        return (prior, currentMonth.AddTicks(-1));
    }
}
