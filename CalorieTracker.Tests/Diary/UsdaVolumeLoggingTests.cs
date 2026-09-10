using System.Net;
using System.Text;
using System.Text.Json;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using CreateModel = CalorieTracker.Pages.Diary.CreateModel;
using EditModel = CalorieTracker.Pages.Diary.EditModel;

namespace CalorieTracker.Tests.Diary;

public class UsdaVolumeLoggingTests
{
    // Synthetic responses with USDA's documented dataset-specific field shapes.
    [Theory]
    [InlineData("Survey (FNDDS)", "Beer", "fl oz", 30, 12, 360)]
    [InlineData("Survey (FNDDS)", "Juice", "fl oz", 31, 2.5, 77.5)]
    [InlineData("Survey (FNDDS)", "Milk", "cup", 244, 0.5, 122)]
    [InlineData("SR Legacy", "Beer", "fl oz", 29.7, 12, 356.4)]
    [InlineData("Foundation", "Unidentified food", "fl oz", 30.5, 2, 61)]
    [InlineData("Foundation", "Unidentified food", "tbsp", 14, 2.5, 35)]
    [InlineData("Foundation", "Unidentified food", "tsp", 4.7, 3, 14.1)]
    public async Task DetailToCachedPortionToDiary_UsesOnlySelectedGramWeight(
        string dataType, string name, string unit, decimal weight,
        decimal count, decimal expectedGrams)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var json = JsonSerializer.Serialize(new
        {
            fdcId = 123,
            description = name,
            dataType,
            foodNutrients = new[] { new { nutrient = new { id = 1008 }, amount = 43 } },
            foodPortions = new[] { new
            {
                gramWeight = weight,
                portionDescription = dataType == "Survey (FNDDS)" ? $"1 {unit}" : null,
                modifier = dataType == "SR Legacy" ? unit : null,
                amount = 1,
                measureUnit = new { name = dataType == "Foundation" ? unit : "undetermined" }
            } }
        });
        var service = new UsdaFoodService(new HttpClient(new ResponseHandler(json)),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FoodDataCentral:ApiKey"] = "test-key"
            }).Build());
        var resolver = new ExternalFoodResolver(database.Context, TestFoodCatalogue.Create(service));
        var resolution = await resolver.ResolveAsync("user-1", "123");
        await database.Context.SaveChangesAsync();
        var food = Assert.IsType<Food>(resolution.Food);
        var portion = Assert.Single(food.Portions);
        Assert.Equal($"1 {unit}", portion.Name);
        Assert.Equal(weight, portion.Amount);
        Assert.Equal("g", food.ServingUnit);
        Assert.Equal(100, food.CanonicalServingSize);

        // Offline fallback keeps the same measure; no fresh density is inferred.
        var offline = new FakeFoodSearchService
        {
            GetHandler = _ => throw new HttpRequestException("Offline")
        };
        var cached = await new ExternalFoodResolver(database.Context, TestFoodCatalogue.Create(offline))
            .ResolveAsync("user-1", "123");
        Assert.True(cached.UsedCachedFallback);
        Assert.Same(food, cached.Food);
        var model = Create(database, food, portion, count);
        model.DiaryEntry.Quantity = 999999; // forged grams are never authoritative
        model.DiaryEntry.CaloriesSnapshot = 999999;
        model.DiaryEntry.ServingUnitSnapshot = "ml";

        Assert.IsType<RedirectToPageResult>(await model.OnPostAsync());
        var entry = await database.Context.DiaryEntries.SingleAsync();
        Assert.Equal(expectedGrams, entry.Quantity);
        Assert.Equal(expectedGrams * 43 / 100, entry.CaloriesConsumed);
        Assert.Equal("g", entry.ServingUnitSnapshot);
        Assert.Equal(portion.Name, entry.PortionNameSnapshot);
        Assert.Equal(count, entry.PortionQuantity);
    }

    [Theory]
    [InlineData(356)] // USDA rounding differences are not reconciled.
    [InlineData(300)] // Material disagreement is also kept portion-specific.
    public async Task ConflictingMeasures_AreNeverAveragedOrRebased(decimal canWeight)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        food.Source = FoodSources.Usda;
        var ounce = new FoodPortion { Food = food, Name = "1 fl oz", Amount = 29.7m };
        var can = new FoodPortion { Food = food, Name = "1 can (12 fl oz)", Amount = canWeight };
        database.Context.AddRange(food, ounce, can);
        await database.Context.SaveChangesAsync();
        Assert.IsType<RedirectToPageResult>(await Create(database, food, ounce, 12).OnPostAsync());
        Assert.IsType<RedirectToPageResult>(await Create(database, food, can, 1).OnPostAsync());
        var entries = await database.Context.DiaryEntries.OrderBy(entry => entry.Id).ToListAsync();
        Assert.Equal(356.4m, entries[0].Quantity);
        Assert.Equal(canWeight, entries[1].Quantity);
        Assert.Equal(29.7m, ounce.Amount);
    }

    [Fact]
    public async Task HistoricalMeasure_EditPreviewAndSaveUseOriginalWeightAndNutrition()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        food.Source = FoodSources.Usda;
        var portion = new FoodPortion { Food = food, Name = "1 fl oz", Amount = 30 };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        var entry = TestData.DiaryEntry("user-1", food, 360, portion, 12);
        database.Context.Add(entry);
        await database.Context.SaveChangesAsync();
        portion.Amount = 99;
        portion.Name = "Renamed measure";
        food.Calories = 999;
        await database.Context.SaveChangesAsync();
        var model = new EditModel(database.Context, PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context));
        PageModelTestContext.Attach(model, "user-1");
        await model.OnGetAsync(entry.Id);
        Assert.Equal(30, model.OriginalPortionAmount);
        Assert.Equal("1 fl oz", model.SelectedPortionName);

        // A fresh POST model has no browser-supplied snapshot authority.
        var post = new EditModel(database.Context, PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context))
        {
            DiaryEntry = new DiaryEntry { FoodId = food.Id, Date = entry.Date,
                MealType = entry.MealType, Quantity = 1 },
            MeasurementMode = "Portion", SelectedPortionId = portion.Id, PortionQuantity = 6
        };
        PageModelTestContext.Attach(post, "user-1");
        Assert.IsType<RedirectToPageResult>(await post.OnPostAsync(entry.Id));
        Assert.Equal(180, entry.Quantity);
        Assert.Equal(360, entry.CaloriesConsumed); // original 200 kcal/100g
        Assert.Equal("1 fl oz", entry.PortionNameSnapshot);
        Assert.Equal(30, post.OriginalPortionAmount);
    }

    [Fact]
    public async Task FractionalMeasure_StoresDecimalProductWithoutDisplayRounding()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var food = TestData.Food("user-1");
        food.Source = FoodSources.Usda;
        var portion = new FoodPortion { Food = food, Name = "1 fl oz", Amount = 29.72345m };
        database.Context.AddRange(food, portion);
        await database.Context.SaveChangesAsync();
        Assert.IsType<RedirectToPageResult>(await Create(database, food, portion, 0.33m).OnPostAsync());
        Assert.Equal(9.8087385m, (await database.Context.DiaryEntries.SingleAsync()).Quantity);
    }

    private static CreateModel Create(TestDatabase database, Food food, FoodPortion portion, decimal count)
    {
        var model = new CreateModel(database.Context, PageModelTestContext.CreateUserManager(),
            new DailyMaintenanceSnapshotService(database.Context), new TestUserLocalTimeProvider())
        {
            DiaryEntry = new DiaryEntry { FoodId = food.Id, Date = TestTime.Today.ToDateTime(TimeOnly.MinValue),
                MealType = "Dinner" },
            MeasurementMode = "Portion", SelectedPortionId = portion.Id, PortionQuantity = count
        };
        PageModelTestContext.Attach(model, "user-1");
        return model;
    }

    private sealed class ResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
