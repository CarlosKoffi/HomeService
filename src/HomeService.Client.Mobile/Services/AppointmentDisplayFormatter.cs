namespace HomeService.Client.Mobile.Services;

public static class AppointmentDisplayFormatter
{
    private static readonly TimeSpan SlotDuration = TimeSpan.FromMinutes(30);

    public static string FormatWindow(DateTimeOffset scheduledFor, string dateFormat)
    {
        var start = scheduledFor.ToUniversalTime();
        var end = start.Add(SlotDuration);
        return $"{start.ToString(dateFormat)} · {start:HH'h'mm} - {end:HH'h'mm}";
    }
}
