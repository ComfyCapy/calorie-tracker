using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public sealed class ReusableMealService
{
    private readonly ApplicationDbContext _context;
    private readonly DailyMaintenanceSnapshotService _snapshotService;

    public ReusableMealService(
        ApplicationDbContext context,
        DailyMaintenanceSnapshotService snapshotService)
    {
        _context = context;
        _snapshotService = snapshotService;
    }

    public async Task<List<DiaryEntry>> LoadSourceMealAsync(
        string userId,
        DateTime sourceDate,
        string mealType) =>
        await _context.DiaryEntries
            .AsNoTracking()
            .Where(entry =>
                entry.UserId == userId &&
                entry.Date.Date == sourceDate.Date &&
                entry.MealType == mealType)
            .OrderBy(entry => entry.Id)
            .ToListAsync();

    public async Task<int?> AddSavedMealToDiaryAsync(
        string userId,
        int savedMealId,
        DateTime date,
        string mealType)
    {
        if (!ValidationRules.IsValidDiaryDate(date) ||
            !ValidationRules.MealTypes.Contains(mealType))
        {
            return null;
        }

        var savedMeal = await _context.SavedMeals
            .AsNoTracking()
            .Include(meal => meal.Items)
            .FirstOrDefaultAsync(meal =>
                meal.Id == savedMealId &&
                meal.UserId == userId);

        if (savedMeal == null || savedMeal.Items.Count == 0)
        {
            return null;
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync();

        var entries = savedMeal.Items
            .OrderBy(item => item.Id)
            .Select(item => DiarySnapshotFactory.FromSavedMealItem(
                item,
                userId,
                date,
                mealType))
            .ToList();

        _context.DiaryEntries.AddRange(entries);
        await _snapshotService.EnsureSnapshotAsync(
            userId,
            DateOnly.FromDateTime(date));
        await _snapshotService.SaveChangesAsync();
        await transaction.CommitAsync();

        return entries.Count;
    }

}
