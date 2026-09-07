using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Foods;

public class ExternalFoodPortionTests
{
    [Fact]
    public async Task Resolve_NewFoodPersistsMeasuredPortionsOnCanonicalFood()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var resolver = CreateResolver(
            database,
            new FoodPortionCandidate("1 roll", 75),
            new FoodPortionCandidate("1 slice", 30));

        var resolution = await resolver.ResolveAsync("user-1", "123");
        await database.Context.SaveChangesAsync();

        Assert.Equal(ExternalFoodFailure.None, resolution.Failure);
        var food = await database.Context.Foods
            .Include(candidate => candidate.Portions)
            .SingleAsync();
        Assert.Equal("user-1", food.UserId);
        Assert.Equal(FoodSources.Usda, food.Source);
        Assert.Equal(100, food.ServingSize);
        Assert.Equal(100, food.CanonicalServingSize);
        Assert.Equal("g", food.ServingUnit);
        Assert.Collection(
            food.Portions,
            portion =>
            {
                Assert.Equal("1 roll", portion.Name);
                Assert.Equal(75, portion.Amount);
            },
            portion =>
            {
                Assert.Equal("1 slice", portion.Name);
                Assert.Equal(30, portion.Amount);
            });
    }

    [Fact]
    public async Task Resolve_CachedFoodWithoutPortionsBackfillsOnlyOnce()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var cached = AddCachedFood(database, "user-1");
        await database.Context.SaveChangesAsync();
        var resolver = CreateResolver(
            database,
            new FoodPortionCandidate("1 roll", 75));

        await resolver.ResolveAsync("user-1", "123");
        await database.Context.SaveChangesAsync();
        await resolver.ResolveAsync("user-1", "123");
        await database.Context.SaveChangesAsync();

        var portion = await database.Context.FoodPortions.SingleAsync();
        Assert.Equal(cached.Id, portion.FoodId);
        Assert.Equal("1 roll", portion.Name);
        Assert.Equal(75, portion.Amount);
    }

    [Fact]
    public async Task Resolve_CachedFoodWithExistingPortionDoesNotOverwriteOrAdd()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var cached = AddCachedFood(database, "user-1");
        var existing = new FoodPortion
        {
            Food = cached,
            Name = "existing serving",
            Amount = 40,
            IsDeleted = true
        };
        database.Context.Add(existing);
        await database.Context.SaveChangesAsync();
        var resolver = CreateResolver(
            database,
            new FoodPortionCandidate("1 roll", 75));

        await resolver.ResolveAsync("user-1", "123");
        await database.Context.SaveChangesAsync();

        var portion = await database.Context.FoodPortions.SingleAsync();
        Assert.Same(existing, portion);
        Assert.Equal("existing serving", portion.Name);
        Assert.Equal(40, portion.Amount);
        Assert.True(portion.IsDeleted);
    }

    [Fact]
    public async Task Resolve_BackfillChangesOnlyCurrentUsersCachedFood()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        var first = AddCachedFood(database, "user-1");
        var second = AddCachedFood(database, "user-2");
        await database.Context.SaveChangesAsync();
        var resolver = CreateResolver(
            database,
            new FoodPortionCandidate("1 roll", 75));

        await resolver.ResolveAsync("user-2", "123");
        await database.Context.SaveChangesAsync();

        var portion = await database.Context.FoodPortions.SingleAsync();
        Assert.Equal(second.Id, portion.FoodId);
        Assert.NotEqual(first.Id, portion.FoodId);
    }

    private static ExternalFoodResolver CreateResolver(
        TestDatabase database,
        params FoodPortionCandidate[] portions)
    {
        var result = TestData.UsdaResult(name: "Cinnamon roll");
        result.Portions = [.. portions];
        var service = new FakeFoodSearchService
        {
            GetHandler = _ => Task.FromResult<FoodSearchResult?>(result)
        };
        return new ExternalFoodResolver(database.Context, service);
    }

    private static Food AddCachedFood(
        TestDatabase database,
        string userId)
    {
        var food = TestData.Food(userId, name: "Cached USDA food");
        food.Source = FoodSources.Usda;
        food.ExternalId = "123";
        database.Context.Foods.Add(food);
        return food;
    }
}
