namespace CalorieTracker.Services;

public sealed record ActivityStreakResult(
    int DistinctActiveDayCount,
    DateOnly? LatestActiveLocalDate,
    int CurrentStreak,
    int LongestStreak);

public sealed class ActivityStreakCalculator
{
    public ActivityStreakResult Calculate(
        IEnumerable<DateOnly> activityDates,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(activityDates);

        var dates = activityDates
            .Distinct()
            .Order()
            .ToArray();

        if (dates.Length == 0)
        {
            return new ActivityStreakResult(0, null, 0, 0);
        }

        var longestStreak = 1;
        var endingStreak = 1;

        for (var index = 1; index < dates.Length; index++)
        {
            endingStreak = dates[index].DayNumber ==
                dates[index - 1].DayNumber + 1
                ? endingStreak + 1
                : 1;

            longestStreak = Math.Max(longestStreak, endingStreak);
        }

        var latestDate = dates[^1];
        var latestDayOffset = today.DayNumber - latestDate.DayNumber;
        var currentStreak = latestDayOffset is 0 or 1
            ? endingStreak
            : 0;

        return new ActivityStreakResult(
            dates.Length,
            latestDate,
            currentStreak,
            longestStreak);
    }
}
