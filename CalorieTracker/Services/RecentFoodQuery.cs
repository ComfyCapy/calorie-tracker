using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public static class RecentFoodQuery
{
    public static async Task<List<Food>> LoadAsync(
        ApplicationDbContext context,
        string userId,
        bool includePortions = false)
    {
        IQueryable<DiaryEntry> query = context.DiaryEntries
            .Where(entry =>
                entry.UserId == userId &&
                entry.Food != null &&
                entry.Food.Source != null &&
                !entry.Food.IsDeleted)
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.Id)
            .Take(100);

        query = includePortions
            ? query.Include(entry => entry.Food!)
                .ThenInclude(food => food.Portions)
            : query.Include(entry => entry.Food!);

        // Sorting before deduplication keeps the newest diary use for each food.
        var entries = await query.ToListAsync();

        return entries
            .Where(entry => entry.Food != null)
            .GroupBy(entry => entry.FoodId)
            .Select(group => group.First().Food!)
            .ToList();
    }
}
