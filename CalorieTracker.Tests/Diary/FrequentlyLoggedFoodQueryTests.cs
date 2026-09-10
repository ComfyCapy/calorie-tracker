using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;

namespace CalorieTracker.Tests.Diary;

public sealed class FrequentlyLoggedFoodQueryTests
{
    [Fact]
    public async Task RanksByFrequencyThenMostRecentUseAndIsolatesUsers()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        await database.AddUserAsync("other", "other");
        var first = TestData.Food("owner", name: "First");
        var recentTie = TestData.Food("owner", name: "Recent tie");
        var oldTie = TestData.Food("owner", name: "Old tie");
        var other = TestData.Food("other", name: "Other user's favourite");
        database.Context.Foods.AddRange(first, recentTie, oldTie, other);
        await database.Context.SaveChangesAsync();

        AddEntries(database, "owner", first, 3, new DateTime(2026, 9, 1));
        AddEntries(database, "owner", recentTie, 2, new DateTime(2026, 9, 8));
        AddEntries(database, "owner", oldTie, 2, new DateTime(2026, 8, 1));
        AddEntries(database, "other", other, 20, new DateTime(2026, 9, 9));
        await database.Context.SaveChangesAsync();

        var foods = await FrequentlyLoggedFoodQuery.LoadAsync(
            database.Context,
            "owner");

        Assert.Equal([first.Id, recentTie.Id, oldTie.Id], foods.Select(food => food.Id));
        Assert.DoesNotContain(foods, food => food.UserId == "other");
    }

    [Fact]
    public async Task ResultCountIsBounded()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");

        for (var index = 0; index < 60; index++)
        {
            var food = TestData.Food("owner", name: $"Food {index}");
            database.Context.Foods.Add(food);
            await database.Context.SaveChangesAsync();
            AddEntries(database, "owner", food, 1, new DateTime(2026, 9, 1).AddMinutes(index));
        }
        await database.Context.SaveChangesAsync();

        var foods = await FrequentlyLoggedFoodQuery.LoadAsync(
            database.Context,
            "owner",
            limit: 500);

        Assert.Equal(50, foods.Count);
    }

    private static void AddEntries(
        TestDatabase database,
        string userId,
        Food food,
        int count,
        DateTime date)
    {
        for (var index = 0; index < count; index++)
        {
            var entry = TestData.DiaryEntry(userId, food, 100);
            entry.Date = date.AddMinutes(index);
            database.Context.DiaryEntries.Add(entry);
        }
    }
}
