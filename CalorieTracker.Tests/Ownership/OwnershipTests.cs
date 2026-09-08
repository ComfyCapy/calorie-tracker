using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DiaryDeleteModel = CalorieTracker.Pages.Diary.DeleteModel;
using DiaryEditModel = CalorieTracker.Pages.Diary.EditModel;
using FoodDeleteModel = CalorieTracker.Pages.Foods.DeleteModel;
using FoodEditModel = CalorieTracker.Pages.Foods.EditModel;

namespace CalorieTracker.Tests.Ownership;

public class OwnershipTests
{
    [Fact]
    public async Task DiaryDelete_OwnerCanDeleteEntry()
    {
        await using var database = await TwoUserDatabaseAsync();
        var food = await AddFoodAsync(database, "user-1");
        var entry = TestData.DiaryEntry("user-1", food, 100);
        database.Context.DiaryEntries.Add(entry);
        await database.Context.SaveChangesAsync();
        var model = new DiaryDeleteModel(
            database.Context,
            PageModelTestContext.CreateUserManager());
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Empty(database.Context.DiaryEntries);
    }

    [Fact]
    public async Task DiaryDelete_AnotherUsersEntryReturnsNotFound()
    {
        await using var database = await TwoUserDatabaseAsync();
        var food = await AddFoodAsync(database, "user-2");
        var entry = TestData.DiaryEntry("user-2", food, 100);
        database.Context.DiaryEntries.Add(entry);
        await database.Context.SaveChangesAsync();
        var model = new DiaryDeleteModel(
            database.Context,
            PageModelTestContext.CreateUserManager());
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Single(database.Context.DiaryEntries);
    }

    [Fact]
    public async Task DiaryEdit_AnotherUsersEntryReturnsNotFound()
    {
        await using var database = await TwoUserDatabaseAsync();
        var food = await AddFoodAsync(database, "user-2");
        var entry = TestData.DiaryEntry("user-2", food, 100);
        database.Context.DiaryEntries.Add(entry);
        await database.Context.SaveChangesAsync();
        var model = new DiaryEditModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context))
        {
            DiaryEntry = new DiaryEntry
            {
                Date = entry.Date,
                MealType = entry.MealType,
                FoodId = food.Id,
                Quantity = 50
            }
        };
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(100, entry.Quantity);
    }

    [Fact]
    public async Task CustomFoodDelete_OwnerSoftDeletesFood()
    {
        await using var database = await TwoUserDatabaseAsync();
        var food = await AddFoodAsync(database, "user-1");
        var model = new FoodDeleteModel(
            database.Context,
            PageModelTestContext.CreateUserManager());
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync(food.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.True(food.IsDeleted);
    }

    [Fact]
    public async Task CustomFoodDelete_AnotherUsersFoodReturnsNotFound()
    {
        await using var database = await TwoUserDatabaseAsync();
        var food = await AddFoodAsync(database, "user-2");
        var model = new FoodDeleteModel(
            database.Context,
            PageModelTestContext.CreateUserManager());
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync(food.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(food.IsDeleted);
    }

    [Fact]
    public async Task CustomFoodEdit_AnotherUsersFoodReturnsNotFound()
    {
        await using var database = await TwoUserDatabaseAsync();
        var food = await AddFoodAsync(database, "user-2");
        var model = new FoodEditModel(
            database.Context,
            PageModelTestContext.CreateUserManager())
        {
            Food = TestData.Food("user-1", name: "Manipulated")
        };
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync(food.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("Test food", food.Name);
    }

    private static async Task<TestDatabase> TwoUserDatabaseAsync()
    {
        var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        return database;
    }

    private static async Task<Food> AddFoodAsync(
        TestDatabase database,
        string userId)
    {
        var food = TestData.Food(userId);
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        return food;
    }
}
