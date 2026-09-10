using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;

namespace CalorieTracker.Tests.Diary;

public sealed class ReusableMealServiceTests
{
    [Fact]
    public async Task SavedMealAddIsOwnedAndUsesStoredSnapshots()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        await database.AddUserAsync("other", "other");
        var food = TestData.Food("owner", name: "Snapshot name");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var source = TestData.DiaryEntry("owner", food, 150);
        var savedMeal = new SavedMeal
        {
            UserId = "owner",
            Name = "Usual lunch",
            Items = [DiarySnapshotFactory.ToSavedMealItem(source)]
        };
        database.Context.SavedMeals.Add(savedMeal);
        await database.Context.SaveChangesAsync();

        food.Name = "Edited food";
        food.Calories = 900;
        await database.Context.SaveChangesAsync();
        var service = CreateService(database);
        Assert.Null(await service.AddSavedMealToDiaryAsync(
            "other", savedMeal.Id, new DateTime(2026, 9, 8), "Lunch"));

        var count = await service.AddSavedMealToDiaryAsync(
            "owner", savedMeal.Id, new DateTime(2026, 9, 8), "Lunch");
        var entry = database.Context.DiaryEntries.Single();
        Assert.Equal(1, count);
        Assert.Equal("Snapshot name", entry.FoodNameSnapshot);
        Assert.Equal(200, entry.CaloriesSnapshot);

        database.Context.SavedMeals.Remove(savedMeal);
        await database.Context.SaveChangesAsync();
        Assert.Equal("Snapshot name", entry.FoodNameSnapshot);
    }

    [Fact]
    public async Task ReusableAddsRejectInvalidDiaryInputsBeforeWriting()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        var food = TestData.Food("owner");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var source = TestData.DiaryEntry("owner", food, 100);
        var savedMeal = new SavedMeal
        {
            UserId = "owner",
            Name = "Meal",
            Items = [DiarySnapshotFactory.ToSavedMealItem(source)]
        };
        database.Context.Add(savedMeal);
        await database.Context.SaveChangesAsync();
        var service = CreateService(database);

        Assert.Null(await service.AddSavedMealToDiaryAsync(
            "owner", savedMeal.Id, new DateTime(1800, 1, 1), "Dinner"));
        Assert.Null(await service.AddSavedMealToDiaryAsync(
            "owner", savedMeal.Id, new DateTime(2026, 9, 8), "Brunch"));
        Assert.Empty(database.Context.DiaryEntries);
    }

    private static ReusableMealService CreateService(TestDatabase database) =>
        new(
            database.Context,
            new DailyMaintenanceSnapshotService(database.Context));
}
