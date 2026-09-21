using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CreateModel = CalorieTracker.Pages.Diary.CreateModel;
using EditModel = CalorieTracker.Pages.Diary.EditModel;

namespace CalorieTracker.Tests.Diary;

public sealed class DiaryMeasurementContractTests
{
    public static TheoryData<bool, string, string, string> InvalidMeasurements => new()
    {
        { false, "zero", "DiaryEntry.Quantity", "Quantity must be greater than 0." },
        { true, "zero", "DiaryEntry.Quantity", "Quantity must be greater than 0." },
        { false, "negative", "DiaryEntry.Quantity", "Quantity must be greater than 0." },
        { true, "negative", "DiaryEntry.Quantity", "Quantity must be greater than 0." },
        { false, "unit", "DiaryEntry.Quantity", "The quantity could not be converted." },
        { true, "unit", "DiaryEntry.Quantity", "The quantity could not be converted." },
        { false, "conversion-overflow", "DiaryEntry.Quantity", "The quantity could not be converted." },
        { true, "conversion-overflow", "DiaryEntry.Quantity", "The quantity could not be converted." },
        { false, "portion-overflow", "PortionQuantity", "The resulting quantity is too large." },
        { true, "portion-overflow", "PortionQuantity", "The resulting quantity is too large." },
        { false, "estimate-overflow", "ApproximationSize", "The estimated quantity is too large." },
        { true, "estimate-overflow", "ApproximationSize", "The estimated quantity is too large." },
        { false, "portion-zero", "PortionQuantity", "Portion quantity must be greater than 0." },
        { true, "portion-zero", "PortionQuantity", "Portion quantity must be greater than 0." },
        { false, "portion-negative", "PortionQuantity", "Portion quantity must be greater than 0." },
        { true, "portion-negative", "PortionQuantity", "Portion quantity must be greater than 0." },
        { false, "portion-missing", "SelectedPortionId", "Please select a portion." },
        { true, "portion-missing", "SelectedPortionId", "Please select a portion." },
        { false, "estimate-missing", "ApproximationPortionId", "Please select the serving your estimate is based on." },
        { true, "estimate-missing", "ApproximationPortionId", "Please select the serving your estimate is based on." },
        { false, "size", "ApproximationSize", "Please select a valid estimate." },
        { true, "size", "ApproximationSize", "Please select a valid estimate." },
        { false, "mode", "MeasurementMode", "Please select a valid measurement mode." },
        { true, "mode", "MeasurementMode", "Please select a valid measurement mode." }
    };

    [Theory]
    [MemberData(nameof(InvalidMeasurements))]
    public async Task InvalidMeasurement_PreservesExactErrorAndDoesNotPersist(
        bool edit, string scenario, string key, string message)
    {
        await using var db = await TestDatabase.CreateAsync();
        var (food, portion, original) = await Seed(db, edit);
        var mode = scenario.StartsWith("portion") ? "Portion"
            : scenario.StartsWith("estimate") || scenario == "size" ? "Approximate" : "Exact";
        if (scenario == "unit") food.ServingUnit = "cup";
        if (scenario == "conversion-overflow") food.ServingUnit = "kg";
        if (scenario is "portion-overflow" or "estimate-overflow") portion.Amount = decimal.MaxValue;
        await db.Context.SaveChangesAsync();
        var quantity = scenario == "zero" ? 0 : scenario == "negative" ? -1
            : scenario == "conversion-overflow" ? decimal.MaxValue : 2;
        var count = scenario == "portion-zero" ? 0 : scenario == "portion-negative" ? -1 : 2;
        var (page, result) = await Submit(db, original, food.Id, quantity,
            scenario == "mode" ? "invalid" : mode,
            scenario.EndsWith("missing") ? null : portion.Id, count,
            scenario == "size" ? "large" : "Large");

        Assert.IsType<PageResult>(result);
        Assert.Equal(message, Assert.Single(page.ModelState[key]!.Errors).ErrorMessage);
        db.Context.ChangeTracker.Clear();
        if (edit) Assert.Equal(100, (await db.Context.DiaryEntries.SingleAsync()).Quantity);
        else Assert.Empty(await db.Context.DiaryEntries.ToListAsync());
    }

    [Theory]
    [InlineData(false, "Exact")]
    [InlineData(true, "Exact")]
    [InlineData(false, "Portion")]
    [InlineData(true, "Portion")]
    [InlineData(false, "Approximate")]
    [InlineData(true, "Approximate")]
    public async Task Modes_RemoveOnlyIrrelevantBindingErrorsAndPersistAuthoritativeQuantity(bool edit, string mode)
    {
        await using var db = await TestDatabase.CreateAsync();
        var (food, portion, original) = await Seed(db, edit);
        food.ServingUnit = "kg";
        await db.Context.SaveChangesAsync();
        string[] staleKeys = mode == "Exact" ? ["SelectedPortionId", "PortionQuantity"]
            : mode == "Portion" ? ["DiaryEntry.Quantity"]
            : ["DiaryEntry.Quantity", "SelectedPortionId", "PortionQuantity"];
        var (page, result) = await Submit(db, original, food.Id, 0.5m, mode, portion.Id, 0.5m,
            "Small", staleKeys);
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./Index", redirect.PageName);
        Assert.Equal("2026-09-05", redirect.RouteValues!["date"]);
        Assert.All(staleKeys, key => Assert.False(page.ModelState.ContainsKey(key)));
        db.Context.ChangeTracker.Clear();
        var saved = await db.Context.DiaryEntries.SingleAsync();
        Assert.Equal(mode == "Exact" ? 500 : mode == "Portion" ? 20 : 30, saved.Quantity);
        Assert.Equal(mode == "Approximate", saved.IsApproximate);
        Assert.Equal(mode == "Exact" ? null : (int?)portion.Id, saved.FoodPortionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoPortions_CreateFallsBackToExact_ButEditRequiresSelection(bool edit)
    {
        await using var db = await TestDatabase.CreateAsync();
        var (food, portion, original) = await Seed(db, edit);
        portion.IsDeleted = true;
        await db.Context.SaveChangesAsync();
        var (page, result) = await Submit(db, original, food.Id, 25, "Portion", null, null);
        if (edit)
        {
            Assert.IsType<PageResult>(result);
            Assert.Equal("Please select a portion.", Assert.Single(page.ModelState["SelectedPortionId"]!.Errors).ErrorMessage);
        }
        else
        {
            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("Exact", Assert.IsType<CreateModel>(page).MeasurementMode);
            Assert.Equal(25, (await db.Context.DiaryEntries.SingleAsync()).Quantity);
        }
    }

    [Theory]
    [InlineData(false, "Exact")]
    [InlineData(true, "Exact")]
    [InlineData(false, "Approximate")]
    [InlineData(true, "Approximate")]
    public async Task DirectPortionFoods_UseCountsWithoutUnitConversion(bool edit, string mode)
    {
        await using var db = await TestDatabase.CreateAsync();
        var (food, portion, original) = await Seed(db, edit);
        portion.IsDeleted = true;
        food.ServingBasis = FoodServingBasis.Portion;
        food.ServingUnit = "unsupported";
        food.CanonicalServingSize = 2;
        await db.Context.SaveChangesAsync();
        var (_, result) = await Submit(db, original, food.Id, 0.125m, mode, null, null, "Large");
        Assert.IsType<RedirectToPageResult>(result);
        db.Context.ChangeTracker.Clear();
        var saved = await db.Context.DiaryEntries.SingleAsync();
        Assert.Equal(mode == "Exact" ? 0.125m : 3m, saved.Quantity);
        Assert.Null(saved.PortionQuantity);
        Assert.Equal(edit ? FoodServingBasis.Measured : FoodServingBasis.Portion, saved.ServingBasisSnapshot);
    }

    [Theory]
    [InlineData(false, "Portion")]
    [InlineData(true, "Portion")]
    [InlineData(false, "Approximate")]
    [InlineData(true, "Approximate")]
    public async Task ForeignPortion_IsRejectedEvenWhenOwnedBySameUser(bool edit, string mode)
    {
        await using var db = await TestDatabase.CreateAsync();
        var (food, _, original) = await Seed(db, edit);
        var other = TestData.Food("owner");
        var foreign = new FoodPortion { Food = other, Name = "Other", Amount = 9 };
        db.Context.AddRange(other, foreign);
        await db.Context.SaveChangesAsync();
        var (page, result) = await Submit(db, original, food.Id, 10, mode, foreign.Id, 1);
        Assert.IsType<PageResult>(result);
        var key = mode == "Portion" ? "SelectedPortionId" : "ApproximationPortionId";
        Assert.Single(page.ModelState[key]!.Errors);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task FoodSelection_RejectsForeignAndNonOriginalDeletedFood(bool edit, bool deleted)
    {
        await using var db = await TestDatabase.CreateAsync();
        var (_, _, original) = await Seed(db, edit);
        await db.AddUserAsync("other", "other");
        var unavailable = TestData.Food(deleted ? "owner" : "other");
        unavailable.IsDeleted = deleted;
        db.Context.Foods.Add(unavailable);
        await db.Context.SaveChangesAsync();
        var (page, result) = await Submit(db, original, unavailable.Id, 10);
        Assert.IsType<PageResult>(result);
        Assert.Equal("Please select a valid food.", Assert.Single(page.ModelState["DiaryEntry.FoodId"]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("Portion", 80)]
    [InlineData("Approximate", 60)]
    public async Task EditOriginalDeletedFoodAndPortion_ReconstructsHistoricalAmountAndRetainsSnapshots(string mode, int expected)
    {
        await using var db = await TestDatabase.CreateAsync();
        var (food, portion, original) = await Seed(db, true);
        original!.Quantity = 30;
        original.FoodPortionId = portion.Id;
        original.PortionQuantity = 0.75m;
        original.PortionNameSnapshot = "Historical serving";
        original.IsApproximate = true;
        original.ApproximationLabel = "Small";
        food.IsDeleted = true;
        portion.IsDeleted = true;
        food.Calories = 999;
        portion.Amount = 500;
        portion.Name = "Renamed";
        await db.Context.SaveChangesAsync();
        var (_, result) = await Submit(db, original, food.Id, 999, mode, portion.Id, 2, "Large");
        Assert.IsType<RedirectToPageResult>(result);
        db.Context.ChangeTracker.Clear();
        var saved = await db.Context.DiaryEntries.SingleAsync();
        Assert.Equal(expected, saved.Quantity);
        Assert.Equal(200, saved.CaloriesSnapshot);
        Assert.Equal("Historical serving", saved.PortionNameSnapshot);
        Assert.Equal(expected * 2, saved.CaloriesConsumed);
    }

    [Fact]
    public async Task EditExact_UsesCurrentUnitWhilePreservingHistoricalNutrition()
    {
        await using var db = await TestDatabase.CreateAsync();
        var (food, _, original) = await Seed(db, true);
        food.ServingUnit = "kg";
        food.Calories = 999;
        await db.Context.SaveChangesAsync();
        var (_, result) = await Submit(db, original, food.Id, 0.5m);
        Assert.IsType<RedirectToPageResult>(result);
        db.Context.ChangeTracker.Clear();
        var saved = await db.Context.DiaryEntries.SingleAsync();
        Assert.Equal(500, saved.Quantity);
        Assert.Equal("g", saved.ServingUnitSnapshot);
        Assert.Equal(1000, saved.CaloriesConsumed);
    }

    private static async Task<(Food Food, FoodPortion Portion, DiaryEntry? Original)> Seed(TestDatabase db, bool edit)
    {
        await db.AddUserAsync("owner");
        var food = TestData.Food("owner");
        var portion = new FoodPortion { Food = food, Name = "Serving", Amount = 40 };
        db.Context.AddRange(food, portion);
        await db.Context.SaveChangesAsync();
        DiaryEntry? original = null;
        if (edit)
        {
            original = TestData.DiaryEntry("owner", food, 100);
            db.Context.DiaryEntries.Add(original);
            await db.Context.SaveChangesAsync();
        }
        return (food, portion, original);
    }

    private static async Task<(PageModel Page, IActionResult Result)> Submit(
        TestDatabase db, DiaryEntry? original, int foodId, decimal quantity,
        string mode = "Exact", int? portionId = null, decimal? count = null,
        string size = "Medium", string[]? bindingErrors = null)
    {
        var input = new DiaryEntry { Date = new DateTime(2026, 9, 5), MealType = "Dinner", FoodId = foodId, Quantity = quantity };
        PageModel page;
        if (original == null)
        {
            page = new CreateModel(db.Context, PageModelTestContext.CreateUserManager(),
                new DailyMaintenanceSnapshotService(db.Context), new TestUserLocalTimeProvider(),
                PageModelTestContext.CreateProgressionHooks(db.Context))
            {
                DiaryEntry = input, MeasurementMode = mode, SelectedPortionId = portionId,
                PortionQuantity = count, ApproximationPortionId = portionId, ApproximationSize = size
            };
        }
        else
        {
            page = new EditModel(db.Context, PageModelTestContext.CreateUserManager(), new DailyMaintenanceSnapshotService(db.Context))
            {
                DiaryEntry = input, MeasurementMode = mode, SelectedPortionId = portionId,
                PortionQuantity = count, ApproximationPortionId = portionId, ApproximationSize = size
            };
        }
        PageModelTestContext.Attach(page, "owner");
        foreach (var key in bindingErrors ?? []) page.ModelState.AddModelError(key, "Binding failure");
        var result = page is CreateModel create ? await create.OnPostAsync() : await ((EditModel)page).OnPostAsync(original!.Id);
        return (page, result);
    }
}
