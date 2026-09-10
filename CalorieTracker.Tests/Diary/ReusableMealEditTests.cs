using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Diary;

public sealed class ReusableMealEditTests
{
    [Fact]
    public async Task SavedMealCanBeRenamedWithoutReplacingItsItems()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        var food = TestData.Food("owner", name: "Original item");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var savedMeal = new SavedMeal
        {
            UserId = "owner",
            Name = "Old name",
            Items =
            [
                DiarySnapshotFactory.ToSavedMealItem(
                    TestData.DiaryEntry("owner", food, 100))
            ]
        };
        database.Context.SavedMeals.Add(savedMeal);
        await database.Context.SaveChangesAsync();
        var model = new Pages.SavedMeals.EditModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            CreateService(database),
            new TestUserLocalTimeProvider());
        PageModelTestContext.Attach(model, "owner");
        model.Input = new Pages.SavedMeals.EditModel.InputModel
        {
            Id = savedMeal.Id,
            Name = "New name",
            ReplaceItems = false,
            SourceDate = DateTime.MinValue,
            SourceMeal = "Invalid"
        };

        var result = await model.OnPostAsync(savedMeal.Id);

        Assert.IsType<RedirectToPageResult>(result);
        var reloaded = await database.Context.SavedMeals
            .AsNoTracking()
            .Include(meal => meal.Items)
            .SingleAsync();
        Assert.Equal("New name", reloaded.Name);
        Assert.Single(reloaded.Items);
        Assert.Equal("Original item", reloaded.Items[0].FoodNameSnapshot);
    }

    [Fact]
    public async Task SavedMealCannotBeRenamedToWhitespace()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        var meal = new SavedMeal
        {
            UserId = "owner",
            Name = "Keep this name"
        };
        database.Context.SavedMeals.Add(meal);
        await database.Context.SaveChangesAsync();
        var model = new Pages.SavedMeals.EditModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            CreateService(database),
            new TestUserLocalTimeProvider());
        PageModelTestContext.Attach(model, "owner");
        model.Input = new Pages.SavedMeals.EditModel.InputModel
        {
            Id = meal.Id,
            Name = "   "
        };

        var result = await model.OnPostAsync(meal.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            "Keep this name",
            await database.Context.SavedMeals
                .Where(savedMeal => savedMeal.Id == meal.Id)
                .Select(savedMeal => savedMeal.Name)
                .SingleAsync());
    }

    [Fact]
    public async Task SavedMealPagesRejectCrossUserIds()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        await database.AddUserAsync("other", "other");
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

        var savedDetails = new Pages.SavedMeals.DetailsModel(
            database.Context,
            PageModelTestContext.CreateUserManager());
        PageModelTestContext.Attach(savedDetails, "other");
        var savedEdit = new Pages.SavedMeals.EditModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            CreateService(database),
            new TestUserLocalTimeProvider());
        PageModelTestContext.Attach(savedEdit, "other");
        var savedDelete = new Pages.SavedMeals.DeleteModel(
            database.Context,
            PageModelTestContext.CreateUserManager());
        PageModelTestContext.Attach(savedDelete, "other");
        Assert.IsType<NotFoundResult>(await savedDetails.OnGetAsync(savedMeal.Id));
        Assert.IsType<NotFoundResult>(await savedEdit.OnGetAsync(savedMeal.Id));
        Assert.IsType<NotFoundResult>(await savedDelete.OnPostAsync(savedMeal.Id));
        Assert.True(await database.Context.SavedMeals.AnyAsync());
    }

    private static ReusableMealService CreateService(TestDatabase database) =>
        new(
            database.Context,
            new DailyMaintenanceSnapshotService(database.Context));
}
