namespace CalorieTracker.Models;

public sealed class UserDailyActivity
{
    public const int MaxTimeZoneIdLength = 100;

    public string UserId { get; set; } = string.Empty;
    public DateOnly LocalDate { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
}
