using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public static class FrequentlyLoggedFoodQuery
{
    public const int MaximumHistoryEntries = 1000;
    public const int DefaultResultLimit = 10;

    public static async Task<List<Food>> LoadAsync(
        ApplicationDbContext context,
        string userId,
        int limit = DefaultResultLimit)
    {
        limit = Math.Clamp(limit, 1, 50);

        var history = await context.DiaryEntries
            .AsNoTracking()
            .Where(entry =>
                entry.UserId == userId &&
                entry.Food != null &&
                !entry.Food.IsDeleted)
            .OrderByDescending(entry => entry.Date)
            .ThenByDescending(entry => entry.Id)
            .Take(MaximumHistoryEntries)
            .Select(entry => new
            {
                entry.FoodId,
                entry.Date,
                entry.Id
            })
            .ToListAsync();

        var rankedIds = history
            .GroupBy(entry => entry.FoodId)
            .Select(group => new
            {
                FoodId = group.Key,
                Count = group.Count(),
                LastDate = group.Max(entry => entry.Date),
                LastId = group.Max(entry => entry.Id)
            })
            .OrderByDescending(food => food.Count)
            .ThenByDescending(food => food.LastDate)
            .ThenByDescending(food => food.LastId)
            .Take(limit)
            .Select(food => food.FoodId)
            .ToList();

        if (rankedIds.Count == 0)
        {
            return [];
        }

        var foods = await context.Foods
            .Where(food =>
                food.UserId == userId &&
                !food.IsDeleted &&
                rankedIds.Contains(food.Id))
            .ToListAsync();

        var foodsById = foods
            .ToDictionary(food => food.Id);

        return rankedIds
            .Where(foodsById.ContainsKey)
            .Select(id => foodsById[id])
            .ToList();
    }
}
