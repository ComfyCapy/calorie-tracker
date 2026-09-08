using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public sealed class CalorieBalanceYearService
{
    private readonly ApplicationDbContext _context;

    public CalorieBalanceYearService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CalorieBalanceYear> GetYearAsync(
        string userId,
        int year,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (year is < 1 or > 9998)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                "The requested year must have a following calendar year.");
        }

        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year + 1, 1, 1);
        var startDateTime = start.ToDateTime(TimeOnly.MinValue);
        var endDateTime = end.ToDateTime(TimeOnly.MinValue);

        var diaryEntries = await _context.DiaryEntries
            .AsNoTracking()
            .Where(entry =>
                entry.UserId == userId &&
                entry.Date >= startDateTime &&
                entry.Date < endDateTime)
            .ToListAsync(cancellationToken);

        var caloriesByDate = diaryEntries
            .GroupBy(entry => DateOnly.FromDateTime(entry.Date))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => entry.CaloriesConsumed));

        var maintenanceByDate = await _context.DailyMaintenanceSnapshots
            .AsNoTracking()
            .Where(snapshot =>
                snapshot.UserId == userId &&
                snapshot.Date >= start &&
                snapshot.Date < end)
            .ToDictionaryAsync(
                snapshot => snapshot.Date,
                snapshot => snapshot.MaintenanceCalories,
                cancellationToken);

        var daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
        var days = new List<CalorieBalanceDay>(daysInYear);
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (var offset = 0;
             offset < daysInYear;
             offset++)
        {
            var date = start.AddDays(offset);

            if (date > today)
            {
                days.Add(new CalorieBalanceDay(
                    date,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null));
                continue;
            }

            if (!caloriesByDate.TryGetValue(date, out var caloriesConsumed))
            {
                // A retained snapshot does not make a deleted diary day populated.
                days.Add(new CalorieBalanceDay(
                    date,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null));
                continue;
            }

            if (!maintenanceByDate.TryGetValue(
                    date,
                    out var maintenanceCalories) ||
                maintenanceCalories <= 0)
            {
                days.Add(new CalorieBalanceDay(
                    date,
                    true,
                    caloriesConsumed,
                    null,
                    null,
                    null,
                    null));
                continue;
            }

            var difference = caloriesConsumed - maintenanceCalories;
            var balancePercentage = difference / maintenanceCalories * 100m;

            days.Add(new CalorieBalanceDay(
                date,
                true,
                caloriesConsumed,
                maintenanceCalories,
                difference,
                balancePercentage,
                CalorieBalanceClassifier.Classify(balancePercentage)));
        }

        return new CalorieBalanceYear(year, days);
    }
}
