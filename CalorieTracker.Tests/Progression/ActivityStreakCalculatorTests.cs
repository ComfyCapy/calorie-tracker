using CalorieTracker.Services;

namespace CalorieTracker.Tests.Progression;

public class ActivityStreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 13);
    private readonly ActivityStreakCalculator _calculator = new();

    [Fact]
    public void Calculate_WithNoDates_ReturnsEmptyResult()
    {
        var result = _calculator.Calculate([], Today);

        Assert.Equal(new ActivityStreakResult(0, null, 0, 0), result);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-2, 0)]
    public void Calculate_WithOneDate_UsesTodayYesterdayAndStaleSemantics(
        int daysFromToday,
        int expectedCurrentStreak)
    {
        var date = Today.AddDays(daysFromToday);

        var result = _calculator.Calculate([date], Today);

        Assert.Equal(1, result.DistinctActiveDayCount);
        Assert.Equal(date, result.LatestActiveLocalDate);
        Assert.Equal(expectedCurrentStreak, result.CurrentStreak);
        Assert.Equal(1, result.LongestStreak);
    }

    [Fact]
    public void Calculate_RemovesDuplicatesAndSortsInput()
    {
        DateOnly[] dates =
        [
            Today,
            Today.AddDays(-2),
            Today.AddDays(-1),
            Today,
            Today.AddDays(-2)
        ];

        var result = _calculator.Calculate(dates, Today);

        Assert.Equal(3, result.DistinctActiveDayCount);
        Assert.Equal(Today, result.LatestActiveLocalDate);
        Assert.Equal(3, result.CurrentStreak);
        Assert.Equal(3, result.LongestStreak);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Calculate_ConsecutiveDates_ReturnsWholeRun(int length)
    {
        var dates = Enumerable.Range(0, length)
            .Select(offset => Today.AddDays(-offset));

        var result = _calculator.Calculate(dates, Today);

        Assert.Equal(length, result.CurrentStreak);
        Assert.Equal(length, result.LongestStreak);
    }

    [Fact]
    public void Calculate_GapResetsCurrentRun()
    {
        DateOnly[] dates =
        [
            Today.AddDays(-4),
            Today.AddDays(-3),
            Today.AddDays(-1),
            Today
        ];

        var result = _calculator.Calculate(dates, Today);

        Assert.Equal(2, result.CurrentStreak);
        Assert.Equal(2, result.LongestStreak);
    }

    [Fact]
    public void Calculate_HistoricalLongestSurvivesShorterCurrentRun()
    {
        DateOnly[] dates =
        [
            Today.AddDays(-10),
            Today.AddDays(-9),
            Today.AddDays(-8),
            Today.AddDays(-7),
            Today.AddDays(-1),
            Today
        ];

        var result = _calculator.Calculate(dates, Today);

        Assert.Equal(2, result.CurrentStreak);
        Assert.Equal(4, result.LongestStreak);
    }

    [Theory]
    [MemberData(nameof(CalendarBoundaryDates))]
    public void Calculate_ConsecutiveDatesAcrossCalendarBoundaries(
        DateOnly first,
        DateOnly second,
        DateOnly third)
    {
        var result = _calculator.Calculate(
            [third, first, second],
            third);

        Assert.Equal(3, result.CurrentStreak);
        Assert.Equal(3, result.LongestStreak);
    }

    [Fact]
    public void Calculate_LatestDateYesterday_PreservesEndingRun()
    {
        var yesterday = Today.AddDays(-1);

        var result = _calculator.Calculate(
            [yesterday.AddDays(-2), yesterday, yesterday.AddDays(-1)],
            Today);

        Assert.Equal(yesterday, result.LatestActiveLocalDate);
        Assert.Equal(3, result.CurrentStreak);
        Assert.Equal(3, result.LongestStreak);
    }

    [Fact]
    public void Calculate_LatestDateOlderThanYesterday_ClearsCurrentRun()
    {
        var result = _calculator.Calculate(
            [Today.AddDays(-4), Today.AddDays(-3), Today.AddDays(-2)],
            Today);

        Assert.Equal(0, result.CurrentStreak);
        Assert.Equal(3, result.LongestStreak);
    }

    [Fact]
    public void Calculate_WithNullDates_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _calculator.Calculate(null!, Today));
    }

    public static TheoryData<DateOnly, DateOnly, DateOnly>
        CalendarBoundaryDates => new()
        {
            {
                new DateOnly(2026, 4, 30),
                new DateOnly(2026, 5, 1),
                new DateOnly(2026, 5, 2)
            },
            {
                new DateOnly(2025, 12, 31),
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 2)
            },
            {
                new DateOnly(2024, 2, 28),
                new DateOnly(2024, 2, 29),
                new DateOnly(2024, 3, 1)
            }
        };
}
