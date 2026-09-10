using CalorieTracker.Models;
using CalorieTracker.Pages.Foods;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalorieTracker.Tests.Foods;

public class FoodHistoryPolicyTests
{
    [Fact]
    public async Task ManualEdit_MassToMassChange_IsAllowed()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var existing = await AddFoodAsync(database, "user-1", "g", 100, 100);
        var model = CreateEditModel(database, existing, "kg", 1);

        var result = await model.OnPostAsync(existing.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("kg", existing.ServingUnit);
        Assert.Equal(1000, existing.CanonicalServingSize);
    }

    [Fact]
    public async Task ManualEdit_VolumeToVolumeChange_IsAllowed()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var existing = await AddFoodAsync(database, "user-1", "ml", 250, 250);
        var model = CreateEditModel(database, existing, "L", 0.5m);

        var result = await model.OnPostAsync(existing.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("L", existing.ServingUnit);
        Assert.Equal(500, existing.CanonicalServingSize);
    }

    [Fact]
    public async Task ManualEdit_MassToVolumeWithoutHistory_IsAllowed()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var existing = await AddFoodAsync(database, "user-1", "g", 100, 100);
        var model = CreateEditModel(database, existing, "ml", 100);

        var result = await model.OnPostAsync(existing.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("ml", existing.ServingUnit);
        Assert.Equal(100, existing.CanonicalServingSize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualEdit_CrossDimensionWithPortion_IsRejected(
        bool portionIsDeleted)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var existing = await AddFoodAsync(database, "user-1", "g", 100, 100);
        database.Context.FoodPortions.Add(new FoodPortion
        {
            FoodId = existing.Id,
            Name = "serving",
            Amount = 50,
            IsDeleted = portionIsDeleted
        });
        await database.Context.SaveChangesAsync();
        var model = CreateEditModel(database, existing, "ml", 100);

        var result = await model.OnPostAsync(existing.Id);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Equal("g", existing.ServingUnit);
    }

    [Fact]
    public async Task ManualEdit_CrossDimensionWithDiaryHistory_IsRejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var existing = await AddFoodAsync(database, "user-1", "g", 100, 100);
        database.Context.DiaryEntries.Add(
            TestData.DiaryEntry("user-1", existing, 100));
        await database.Context.SaveChangesAsync();
        var model = CreateEditModel(database, existing, "ml", 100);

        var result = await model.OnPostAsync(existing.Id);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Equal("g", existing.ServingUnit);
    }

    [Fact]
    public async Task ExternalRefresh_VolumeFoodWithoutHistory_RefreshesToMass()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var cached = await AddCachedVolumeFoodAsync(database, "user-1");
        var service = SuccessfulFoodService();
        var resolver = new ExternalFoodResolver(
            database.Context,
            TestFoodCatalogue.Create(service));

        var resolution = await resolver.ResolveAsync("user-1", "123");

        Assert.False(resolution.UsedCachedFallback);
        Assert.Same(cached, resolution.Food);
        Assert.Equal("g", cached.ServingUnit);
        Assert.Equal(100, cached.CanonicalServingSize);
        Assert.Equal("Fresh USDA food", cached.Name);
    }

    [Fact]
    public async Task ExternalRefresh_VolumeFoodWithPortion_PreservesCachedDimension()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var cached = await AddCachedVolumeFoodAsync(database, "user-1");
        database.Context.FoodPortions.Add(new FoodPortion
        {
            FoodId = cached.Id,
            Name = "glass",
            Amount = 250,
            IsDeleted = true
        });
        await database.Context.SaveChangesAsync();
        var resolver = new ExternalFoodResolver(
            database.Context,
            TestFoodCatalogue.Create(SuccessfulFoodService()));

        var resolution = await resolver.ResolveAsync("user-1", "123");

        Assert.True(resolution.UsedCachedFallback);
        Assert.Equal("ml", cached.ServingUnit);
        Assert.Equal("Cached drink", cached.Name);
    }

    [Fact]
    public async Task ExternalRefresh_VolumeFoodWithDiaryHistory_PreservesCachedDimension()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var cached = await AddCachedVolumeFoodAsync(database, "user-1");
        database.Context.DiaryEntries.Add(
            TestData.DiaryEntry("user-1", cached, 250));
        await database.Context.SaveChangesAsync();
        var resolver = new ExternalFoodResolver(
            database.Context,
            TestFoodCatalogue.Create(SuccessfulFoodService()));

        var resolution = await resolver.ResolveAsync("user-1", "123");

        Assert.True(resolution.UsedCachedFallback);
        Assert.Equal("ml", cached.ServingUnit);
        Assert.Equal("Cached drink", cached.Name);
    }

    [Fact]
    public async Task ExternalFailure_UsesAndReactivatesCurrentUsersCachedFood()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var cached = await AddCachedVolumeFoodAsync(database, "user-1");
        cached.IsDeleted = true;
        await database.Context.SaveChangesAsync();
        var service = new FakeFoodSearchService
        {
            GetHandler = _ => throw new HttpRequestException("offline")
        };
        var resolver = new ExternalFoodResolver(
            database.Context,
            TestFoodCatalogue.Create(service));

        var resolution = await resolver.ResolveAsync("user-1", "123");

        Assert.Equal(ExternalFoodFailure.None, resolution.Failure);
        Assert.True(resolution.UsedCachedFallback);
        Assert.Same(cached, resolution.Food);
        Assert.False(cached.IsDeleted);
    }

    private static EditModel CreateEditModel(
        TestDatabase database,
        Food existing,
        string newUnit,
        decimal newServingSize)
    {
        var model = new EditModel(
            database.Context,
            PageModelTestContext.CreateUserManager())
        {
            Food = new Food
            {
                Name = existing.Name,
                Calories = existing.Calories,
                Protein = existing.Protein,
                Carbohydrates = existing.Carbohydrates,
                Fat = existing.Fat,
                ServingSize = newServingSize,
                ServingUnit = newUnit
            }
        };
        PageModelTestContext.Attach(model, "user-1");
        return model;
    }

    private static async Task<Food> AddFoodAsync(
        TestDatabase database,
        string userId,
        string unit,
        decimal servingSize,
        decimal canonicalServingSize)
    {
        var food = TestData.Food(
            userId,
            unit,
            servingSize,
            canonicalServingSize);
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        return food;
    }

    private static async Task<Food> AddCachedVolumeFoodAsync(
        TestDatabase database,
        string userId)
    {
        var food = TestData.Food(
            userId,
            "ml",
            250,
            250,
            "Cached drink");
        food.Source = FoodSources.Usda;
        food.ExternalId = "123";
        database.Context.Foods.Add(food);
        await database.Context.SaveChangesAsync();
        return food;
    }

    private static FakeFoodSearchService SuccessfulFoodService() => new()
    {
        GetHandler = _ => Task.FromResult<FoodSearchResult?>(
            TestData.UsdaResult(name: "Fresh USDA food"))
    };
}
