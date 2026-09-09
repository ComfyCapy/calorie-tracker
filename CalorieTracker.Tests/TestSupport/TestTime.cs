using CalorieTracker.Services;

namespace CalorieTracker.Tests.TestSupport;

public static class TestTime
{
    public static readonly DateOnly Today = new(2026, 9, 9);
}

public sealed class TestUserLocalTimeProvider(
    DateOnly? today = null,
    TimeZoneInfo? timeZone = null) : IUserLocalTimeProvider
{
    public DateOnly Today { get; } = today ?? TestTime.Today;
    public TimeZoneInfo TimeZone { get; } = timeZone ?? TimeZoneInfo.Utc;
    public DateTimeOffset UtcNow =>
        new(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
    public DateTimeOffset LocalNow =>
        TimeZoneInfo.ConvertTime(UtcNow, TimeZone);
}

public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
