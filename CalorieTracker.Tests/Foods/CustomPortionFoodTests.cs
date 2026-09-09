using System.Globalization;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DiaryCreateModel = CalorieTracker.Pages.Diary.CreateModel;
using FoodCreateModel = CalorieTracker.Pages.Foods.CreateModel;
using FoodEditModel = CalorieTracker.Pages.Foods.EditModel;

namespace CalorieTracker.Tests.Foods;

public class CustomPortionFoodTests
{
    [Fact]
    public async Task Create_DirectSandwichPortionRequiresNoMeasuredWeight()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = CreateFoodModel(database, "user-1", PortionFood("  sandwich  "));

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var food = await database.Context.Foods.SingleAsync();
        Assert.Equal(FoodServingBasis.Portion, food.ServingBasis);
        Assert.Equal(1, food.ServingSize);
        Assert.Equal(1, food.CanonicalServingSize);
        Assert.Equal("sandwich", food.PortionLabel);
        Assert.Equal(731, food.Calories);
        Assert.Equal(35.3m, food.Protein);
        Assert.Equal(85.2m, food.Carbohydrates);
        Assert.Equal(29.4m, food.Fat);
    }

    [Fact]
    public async Task Create_AllowsAnotherNamedPortion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = PortionFood("bottle");
        food.ServingSize = 2;
        var model = CreateFoodModel(database, "user-1", food);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var saved = await database.Context.Foods.SingleAsync();
        Assert.Equal("bottle", saved.PortionLabel);
        Assert.Equal(2, saved.CanonicalServingSize);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("line\nbreak")]
    [InlineData("bad\u0001label")]
    public async Task Create_InvalidPortionLabelIsRejected(string label)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = CreateFoodModel(database, "user-1", PortionFood(label));

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Equal(FoodServingBasis.Portion, model.Food.ServingBasis);
        Assert.Equal(731, model.Food.Calories);
        Assert.Empty(database.Context.Foods);
    }

    [Fact]
    public async Task Create_OverlongPortionLabelIsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = CreateFoodModel(
            database,
            "user-1",
            PortionFood(new string('x', Food.MaxPortionLabelLength + 1)));

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains("Food.PortionLabel", model.ModelState.Keys);
        Assert.Empty(database.Context.Foods);
    }

    [Fact]
    public async Task Create_TrimsBeforeApplyingPortionLabelLengthLimit()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var label = new string('x', Food.MaxPortionLabelLength);
        var model = CreateFoodModel(
            database,
            "user-1",
            PortionFood($"  {label}  "));

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(label, (await database.Context.Foods.SingleAsync()).PortionLabel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Create_NonPositivePortionAmountIsRejected(int amount)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = PortionFood("sandwich");
        food.ServingSize = amount;
        var model = CreateFoodModel(database, "user-1", food);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains("Food.ServingSize", model.ModelState.Keys);
        Assert.Empty(database.Context.Foods);
    }

    [Fact]
    public async Task Edit_UpdatesDirectPortionNameAndNutrition()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var existing = PortionFood("sandwich");
        existing.UserId = "user-1";
        database.Context.Foods.Add(existing);
        await database.Context.SaveChangesAsync();
        var edited = PortionFood("  roll  ");
        edited.Calories = 500;
        var model = new FoodEditModel(
            database.Context,
            PageModelTestContext.CreateUserManager())
        {
            Food = edited
        };
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync(existing.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("roll", existing.PortionLabel);
        Assert.Equal(500, existing.Calories);
        Assert.Equal(FoodServingBasis.Portion, existing.ServingBasis);
    }

    [Fact]
    public async Task Edit_DoesNotReinterpretMeasuredFoodWithHistoryAsPortion()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var existing = TestData.Food("user-1");
        database.Context.Foods.Add(existing);
        await database.Context.SaveChangesAsync();
        database.Context.DiaryEntries.Add(
            TestData.DiaryEntry("user-1", existing, 100));
        await database.Context.SaveChangesAsync();
        var model = new FoodEditModel(
            database.Context,
            PageModelTestContext.CreateUserManager())
        {
            Food = PortionFood("sandwich")
        };
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync(existing.Id);

        Assert.IsType<PageResult>(result);
        Assert.Contains("Food.ServingBasis", model.ModelState.Keys);
        Assert.Equal(FoodServingBasis.Measured, existing.ServingBasis);
    }

    [Fact]
    public async Task Edit_AnotherUsersDirectPortionFoodIsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        await database.AddUserAsync("user-2", "second");
        var existing = PortionFood("sandwich");
        existing.UserId = "user-2";
        database.Context.Foods.Add(existing);
        await database.Context.SaveChangesAsync();
        var model = new FoodEditModel(
            database.Context,
            PageModelTestContext.CreateUserManager())
        {
            Food = PortionFood("renamed")
        };
        PageModelTestContext.Attach(model, "user-1");

        var result = await model.OnPostAsync(existing.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal("sandwich", existing.PortionLabel);
        Assert.Equal(731, existing.Calories);
    }

    [Theory]
    [InlineData("0.5", "365.5", "17.65", "42.6", "14.7")]
    [InlineData("1", "731", "35.3", "85.2", "29.4")]
    [InlineData("2", "1462", "70.6", "170.4", "58.8")]
    public async Task Diary_DirectPortionQuantityScalesNutritionWithoutGramConversion(
        string quantityText,
        string caloriesText,
        string proteinText,
        string carbohydratesText,
        string fatText)
    {
        var quantity = decimal.Parse(quantityText, CultureInfo.InvariantCulture);
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = PortionFood("sandwich");
        food.UserId = "user-1";
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var model = DiaryModel(database, "user-1", food, quantity);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var entry = await database.Context.DiaryEntries.SingleAsync();
        Assert.Equal(quantity, entry.Quantity);
        Assert.Equal(FoodServingBasis.Portion, entry.ServingBasisSnapshot);
        Assert.Equal("sandwich", entry.PortionLabelSnapshot);
        Assert.Equal(
            decimal.Parse(caloriesText, CultureInfo.InvariantCulture),
            entry.CaloriesConsumed);
        Assert.Equal(
            decimal.Parse(proteinText, CultureInfo.InvariantCulture),
            entry.ProteinConsumed);
        Assert.Equal(
            decimal.Parse(carbohydratesText, CultureInfo.InvariantCulture),
            entry.CarbohydratesConsumed);
        Assert.Equal(
            decimal.Parse(fatText, CultureInfo.InvariantCulture),
            entry.FatConsumed);
    }

    [Fact]
    public async Task Diary_AnotherUsersDirectPortionFoodIsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        await database.AddUserAsync("user-2", "second");
        var food = PortionFood("sandwich");
        food.UserId = "user-2";
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var model = DiaryModel(database, "user-1", food, 1);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(database.Context.DiaryEntries);
    }

    [Fact]
    public async Task DiarySnapshotAndYearMapRemainStableAfterPortionFoodEdit()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = PortionFood("sandwich");
        food.UserId = "user-1";
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        var model = DiaryModel(database, "user-1", food, 1);
        await model.OnPostAsync();
        database.Context.DailyMaintenanceSnapshots.Add(
            new DailyMaintenanceSnapshot(
                "user-1",
                new DateOnly(2026, 9, 5),
                2000));
        await database.Context.SaveChangesAsync();

        food.Calories = 100;
        food.PortionLabel = "renamed";
        await database.Context.SaveChangesAsync();

        var entry = await database.Context.DiaryEntries.SingleAsync();
        var day = (await new CalorieBalanceYearService(database.Context)
                .GetYearAsync("user-1", 2026, new DateOnly(2026, 9, 9)))
            .Days.Single(item => item.Date == new DateOnly(2026, 9, 5));

        Assert.Equal(731, entry.CaloriesConsumed);
        Assert.Equal("sandwich", entry.PortionLabelSnapshot);
        Assert.Equal(731, day.CaloriesConsumed);
    }

    private static FoodCreateModel CreateFoodModel(
        TestDatabase database,
        string userId,
        Food food)
    {
        var model = new FoodCreateModel(
            database.Context,
            PageModelTestContext.CreateUserManager())
        {
            Food = food
        };
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static DiaryCreateModel DiaryModel(
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

    private static Food PortionFood(string label) => new()
    {
        Name = "Coco di Mama spicy chicken milanese",
        Calories = 731,
        Protein = 35.3m,
        Carbohydrates = 85.2m,
        Fat = 29.4m,
        ServingBasis = FoodServingBasis.Portion,
        ServingSize = 1,
        CanonicalServingSize = 1,
        PortionLabel = label
    };
}
