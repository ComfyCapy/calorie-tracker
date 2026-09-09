using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Http;

namespace CalorieTracker.Tests.Services;

public class UserLocalTimeProviderTests
{
    [Fact]
    public void LondonAfterLocalMidnight_IsOneCalendarDayAheadOfUtc()
    {
        var provider = Create(
            new DateTimeOffset(2026, 9, 8, 23, 35, 0, TimeSpan.Zero),
            "Europe%2FLondon");

        Assert.Equal(new DateOnly(2026, 9, 9), provider.Today);
        Assert.Equal(TimeSpan.FromHours(1), provider.LocalNow.Offset);
    }

    [Fact]
    public void NewYorkBeforeLocalMidnight_IsOneCalendarDayBehindUtc()
    {
        var provider = Create(
            new DateTimeOffset(2026, 9, 9, 2, 0, 0, TimeSpan.Zero),
            "America%2FNew_York");

        Assert.Equal(new DateOnly(2026, 9, 8), provider.Today);
        Assert.Equal(TimeSpan.FromHours(-4), provider.LocalNow.Offset);
    }

    [Theory]
    [InlineData("2026-09-08T22:59:59Z", 2026, 9, 8)]
    [InlineData("2026-09-08T23:00:00Z", 2026, 9, 9)]
    [InlineData("2026-10-24T23:00:00Z", 2026, 10, 25)]
    [InlineData("2026-10-25T00:00:00Z", 2026, 10, 25)]
    public void LondonBoundaries_UseIanaDaylightSavingRules(
        string utcValue,
        int year,
        int month,
        int day)
    {
        var provider = Create(DateTimeOffset.Parse(utcValue), "Europe/London");

        Assert.Equal(new DateOnly(year, month, day), provider.Today);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/A_Time_Zone")]
    [InlineData("%E0%A4%A")]
    public void MissingOrInvalidTimeZone_FallsBackToUtc(string? timeZoneId)
    {
        var provider = Create(
            new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero),
            timeZoneId);

        Assert.Equal(TimeZoneInfo.Utc, provider.TimeZone);
        Assert.Equal(new DateOnly(2026, 9, 9), provider.Today);
    }

    private static UserLocalTimeProvider Create(
        DateTimeOffset utcNow,
        string? timeZoneId)
    {
        var context = new DefaultHttpContext();

        if (timeZoneId != null)
        {
            context.Request.Headers.Cookie =
                $"{UserLocalTimeProvider.TimeZoneCookieName}={timeZoneId}";
        }

        return new UserLocalTimeProvider(
            new HttpContextAccessor { HttpContext = context },
            new FixedTimeProvider(utcNow));
    }
}
