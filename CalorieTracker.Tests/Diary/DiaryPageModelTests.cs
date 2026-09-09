using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DiaryCreateModel = CalorieTracker.Pages.Diary.CreateModel;
using DiaryEditModel = CalorieTracker.Pages.Diary.EditModel;

namespace CalorieTracker.Tests.Diary;

public class DiaryPageModelTests
{
    [Fact]
    public async Task CreateExactOunces_StoresCanonicalQuantityAndCorrectNutrition()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food(
            "user-1",
            "oz",
            1,
            28.349523125m,
            "Freedom Cheese");
        food.Calories = 120;
        food.Protein = 7;
        food.Carbohydrates = 1;
        food.Fat = 9;
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var model = CreateCreateModel(database, "user-1", food, 2);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var entry = await database.Context.DiaryEntries.SingleAsync();
        Assert.Equal(56.69904625m, entry.Quantity, 8);
        Assert.Equal(240, entry.CaloriesConsumed);
        Assert.Equal(14, entry.ProteinConsumed);
        Assert.Equal(2, entry.CarbohydratesConsumed);
        Assert.Equal(18, entry.FatConsumed);
    }

    [Fact]
    public async Task CreatePortion_UsesCanonicalPortionAmountAndDecimalCount()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        var portion = new FoodPortion
        {
            Food = food,
            Name = "slice",
            Amount = 35
        };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var model = CreateCreateModel(database, "user-1", food, 999);
        model.MeasurementMode = "Portion";
        model.SelectedPortionId = portion.Id;
        model.PortionQuantity = 2.5m;

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var entry = await database.Context.DiaryEntries.SingleAsync();
        Assert.Equal(87.5m, entry.Quantity);
        Assert.Equal(2.5m, entry.PortionQuantity);
        Assert.Equal(portion.Id, entry.FoodPortionId);
        Assert.Equal("slice", entry.PortionNameSnapshot);
        Assert.Equal(175m, entry.CaloriesConsumed);
    }

    [Fact]
    public async Task CreatePortion_TwoCinnamonRollsUseMeasuredWeightAndSnapshotLabel()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1", name: "Cinnamon roll");
        var portion = new FoodPortion
        {
            Food = food,
            Name = "1 roll",
            Amount = 75
        };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var model = CreateCreateModel(database, "user-1", food, 999);
        model.MeasurementMode = "Portion";
        model.SelectedPortionId = portion.Id;
        model.PortionQuantity = 2;

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var entry = await database.Context.DiaryEntries.SingleAsync();
        Assert.Equal(150, entry.Quantity);
        Assert.Equal(2, entry.PortionQuantity);
        Assert.Equal("1 roll", entry.PortionNameSnapshot);
    }

    [Fact]
    public async Task CreateExact_FoodWithPortionsStillAllowsGramFallback()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1", name: "Cinnamon roll");
        var portion = new FoodPortion
        {
            Food = food,
            Name = "1 roll",
            Amount = 75
        };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var model = CreateCreateModel(database, "user-1", food, 50);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var entry = await database.Context.DiaryEntries.SingleAsync();
        Assert.Equal(50, entry.Quantity);
        Assert.Null(entry.FoodPortionId);
        Assert.Null(entry.PortionQuantity);
        Assert.Null(entry.PortionNameSnapshot);
    }

    [Fact]
    public async Task OnGet_SelectedFoodWithOnePortionPreselectsThatPortion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        var portion = new FoodPortion
        {
            Food = food,
            Name = "1 roll",
            Amount = 75
        };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var model = CreateEmptyCreateModel(database, "user-1");

        var result = await model.OnGetAsync(
            new DateTime(2026, 9, 5),
            "Lunch",
            food.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Portion", model.MeasurementMode);
        Assert.Equal(portion.Id, model.SelectedPortionId);
        Assert.Equal(1, model.PortionQuantity);
    }

    [Fact]
    public async Task OnGet_SelectedFoodWithMultiplePortionsDoesNotChooseOne()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        database.Context.AddRange(
            food,
            new FoodPortion { Food = food, Name = "1 roll", Amount = 75 },
            new FoodPortion { Food = food, Name = "1 slice", Amount = 30 });
        await database.Context.SaveChangesAsync();
        var model = CreateEmptyCreateModel(database, "user-1");

        var result = await model.OnGetAsync(
            new DateTime(2026, 9, 5),
            "Lunch",
            food.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Portion", model.MeasurementMode);
        Assert.Null(model.SelectedPortionId);
        Assert.Equal(1, model.PortionQuantity);
    }

    [Fact]
    public async Task OnGet_SelectedFoodWithoutPortionsUsesExactAmountOnly()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        database.Context.Add(food);
        await database.Context.SaveChangesAsync();
        var model = CreateEmptyCreateModel(database, "user-1");

        var result = await model.OnGetAsync(
            new DateTime(2026, 9, 5),
            "Lunch",
            food.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Exact", model.MeasurementMode);
        Assert.Null(model.SelectedPortionId);
        Assert.Null(model.PortionQuantity);
    }

    [Fact]
    public async Task EditExactToPortion_SetsPortionStateAndPreservesFoodSnapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        var portion = new FoodPortion { Food = food, Name = "bowl", Amount = 40 };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var entry = TestData.DiaryEntry("user-1", food, 50);
        entry.FoodNameSnapshot = "Original food name";
        database.Context.DiaryEntries.Add(entry);
        await database.Context.SaveChangesAsync();
        var model = CreateEditModel(database, "user-1", entry, 999);
        model.MeasurementMode = "Portion";
        model.SelectedPortionId = portion.Id;
        model.PortionQuantity = 2;

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(80, entry.Quantity);
        Assert.Equal(portion.Id, entry.FoodPortionId);
        Assert.Equal(2, entry.PortionQuantity);
        Assert.Equal("bowl", entry.PortionNameSnapshot);
        Assert.Equal("Original food name", entry.FoodNameSnapshot);
    }

    [Fact]
    public async Task EditPortionToExact_ClearsStalePortionState()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1", "kg", 1, 1000);
        var portion = new FoodPortion { Food = food, Name = "bag", Amount = 500 };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var entry = TestData.DiaryEntry("user-1", food, 500, portion, 1);
        database.Context.DiaryEntries.Add(entry);
        await database.Context.SaveChangesAsync();
        var model = CreateEditModel(database, "user-1", entry, 2);
        model.MeasurementMode = "Exact";
        model.SelectedPortionId = portion.Id;
        model.PortionQuantity = 9;

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(2000, entry.Quantity);
        Assert.Null(entry.FoodPortionId);
        Assert.Null(entry.PortionQuantity);
        Assert.Null(entry.PortionNameSnapshot);
    }

    [Fact]
    public async Task EditDeletedHistoricalFood_AllowsOriginalFoodOnly()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        food.IsDeleted = true;
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var entry = TestData.DiaryEntry("user-1", food, 100);
        database.Context.DiaryEntries.Add(entry);
        await database.Context.SaveChangesAsync();
        var model = CreateEditModel(database, "user-1", entry, 50);

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(50, entry.Quantity);
        Assert.Equal("Test food", entry.FoodNameSnapshot);
    }

    [Fact]
    public async Task EditDeletedHistoricalPortion_UsesLoggedAmountAndLabel()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        var portion = new FoodPortion
        {
            Food = food,
            Name = "Renamed slice",
            Amount = 50,
            IsDeleted = true
        };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var entry = TestData.DiaryEntry("user-1", food, 60, portion, 2);
        entry.PortionNameSnapshot = "Original slice";
        database.Context.DiaryEntries.Add(entry);
        await database.Context.SaveChangesAsync();
        var model = CreateEditModel(database, "user-1", entry, 999);
        model.MeasurementMode = "Portion";
        model.SelectedPortionId = portion.Id;
        model.PortionQuantity = 3;

        var result = await model.OnPostAsync(entry.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(90, entry.Quantity);
        Assert.Equal("Original slice", entry.PortionNameSnapshot);
    }

    [Fact]
    public async Task CreatePortion_WithPortionFromDifferentFood_IsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var selectedFood = TestData.Food("user-1", name: "Selected");
        var otherFood = TestData.Food("user-1", name: "Other");
        var selectedPortion = new FoodPortion
        {
            Food = selectedFood,
            Name = "selected portion",
            Amount = 20
        };
        var otherPortion = new FoodPortion
        {
            Food = otherFood,
            Name = "other portion",
            Amount = 25
        };
        database.Context.AddRange(
            selectedFood,
            selectedPortion,
            otherFood,
            otherPortion);
        await database.Context.SaveChangesAsync();
        var model = CreateCreateModel(database, "user-1", selectedFood, 1);
        model.MeasurementMode = "Portion";
        model.SelectedPortionId = otherPortion.Id;
        model.PortionQuantity = 1;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains(nameof(model.SelectedPortionId), model.ModelState.Keys);
        Assert.Empty(database.Context.DiaryEntries);
    }

    [Fact]
    public async Task CreatePortion_WithAnotherUsersPortion_IsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        var selectedFood = TestData.Food("user-1", name: "Selected");
        var otherFood = TestData.Food("user-2", name: "Other user's food");
        var selectedPortion = new FoodPortion
        {
            Food = selectedFood,
            Name = "1 serving",
            Amount = 20
        };
        var otherPortion = new FoodPortion
        {
            Food = otherFood,
            Name = "1 packet",
            Amount = 25
        };
        database.Context.AddRange(
            selectedFood,
            selectedPortion,
            otherFood,
            otherPortion);
        await database.Context.SaveChangesAsync();
        var model = CreateCreateModel(database, "user-1", selectedFood, 1);
        model.MeasurementMode = "Portion";
        model.SelectedPortionId = otherPortion.Id;
        model.PortionQuantity = 1;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains(nameof(model.SelectedPortionId), model.ModelState.Keys);
        Assert.Empty(database.Context.DiaryEntries);
    }

    [Fact]
    public async Task Create_WithFoodOwnedByAnotherUser_IsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        await database.AddUserAsync("user-2", "other");
        var otherFood = TestData.Food("user-2");
        database.Context.Foods.Add(otherFood);
        await database.Context.SaveChangesAsync();
        var model = CreateCreateModel(database, "user-1", otherFood, 100);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(database.Context.DiaryEntries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateExact_WithNonPositiveQuantity_IsRejected(
        int quantity)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var model = CreateCreateModel(database, "user-1", food, quantity);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(database.Context.DiaryEntries);
    }

    [Fact]
    public async Task CreatePortion_WhenQuantityOverflows_IsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        var portion = new FoodPortion
        {
            Food = food,
            Name = "huge",
            Amount = decimal.MaxValue
        };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var model = CreateCreateModel(database, "user-1", food, 1);
        model.MeasurementMode = "Portion";
        model.SelectedPortionId = portion.Id;
        model.PortionQuantity = 2;

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(database.Context.DiaryEntries);
    }

    private static DiaryCreateModel CreateCreateModel(
        TestDatabase database,
        string userId,
        Food food,
        decimal quantity)
    {
        var model = new DiaryCreateModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context),
            new TestUserLocalTimeProvider())
        {
            DiaryEntry = new DiaryEntry
            {
                Date = new DateTime(2026, 9, 5),
                MealType = "Dinner",
                FoodId = food.Id,
                Quantity = quantity
            },
            MeasurementMode = "Exact"
        };
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static DiaryCreateModel CreateEmptyCreateModel(
        TestDatabase database,
        string userId)
    {
        var model = new DiaryCreateModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context),
            new TestUserLocalTimeProvider());
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static DiaryEditModel CreateEditModel(
        TestDatabase database,
        string userId,
        DiaryEntry entry,
        decimal quantity)
    {
        var model = new DiaryEditModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context))
        {
            DiaryEntry = new DiaryEntry
            {
                Date = entry.Date,
                MealType = entry.MealType,
                FoodId = entry.FoodId,
                Quantity = quantity
            },
            MeasurementMode = "Exact"
        };
        PageModelTestContext.Attach(model, userId);
        return model;
    }
}
