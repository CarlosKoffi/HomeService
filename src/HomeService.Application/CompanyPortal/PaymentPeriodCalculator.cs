namespace HomeService.Application.CompanyPortal;

public static class PaymentPeriodCalculator
{
    public static DateTimeOffset GetStart(string period, DateTimeOffset now)
    {
        return period.Trim().ToLowerInvariant() switch
        {
            "year" => new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "week" => now.AddDays(-7),
            _ => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)
        };
    }
}
