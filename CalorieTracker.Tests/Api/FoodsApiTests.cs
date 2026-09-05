using System.Net;
using CalorieTracker.Controllers;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Api;

public class FoodsApiTests
{
    [Fact]
    public async Task Search_ReturnsProviderResultsAndCurrentUsersFavouriteState()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        database.Context.Foods.AddRange(
            CachedFood("user-1", "123", true),
            CachedFood("user-2", "456", true));
        await database.Context.SaveChangesAsync();
        var service = new FakeFoodSearchService
        {
            SearchHandler = (_, page, pageSize) => Task.FromResult(
                new FoodSearchPage
                {
                    Foods =
                    [
                        TestData.UsdaResult("123", "Favourite"),
                        TestData.UsdaResult("456", "Other user's favourite")
                    ],
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalResults = 2,
                    TotalPages = 1
                })
        };
        var controller = CreateController(database, service, "user-1");

        var action = await controller.Search("apple", 1, 20);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<FoodSearchPage>(result.Value);
        Assert.True(page.Foods.Single(food => food.ExternalId == "123").IsFavourite);
        Assert.False(page.Foods.Single(food => food.ExternalId == "456").IsFavourite);
    }

    [Fact]
    public async Task Search_BlankQueryReturnsEmptyPageWithoutCallingProvider()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new FakeFoodSearchService();
        var controller = CreateController(database, service, "user-1");

        var action = await controller.Search("   ", -1, 999);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<FoodSearchPage>(result.Value);
        Assert.Empty(page.Foods);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(0, service.SearchCallCount);
    }

    [Fact]
    public async Task Search_WhenProviderFails_ReturnsServiceUnavailable()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new FakeFoodSearchService
        {
            SearchHandler = (_, _, _) =>
                throw new InvalidOperationException("Provider unavailable")
        };
        var controller = CreateController(database, service, "user-1");

        var action = await controller.Search("apple");

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    [Theory]
    [InlineData("123", true, "123")]
    [InlineData("00123", true, "123")]
    [InlineData("0", false, "")]
    [InlineData("-1", false, "")]
    [InlineData("1.5", false, "")]
    [InlineData("abc", false, "")]
    public void ExternalIdNormalization_AcceptsOnlyPositiveIntegerIds(
        string input,
        bool expectedResult,
        string expectedId)
    {
        var result = ExternalFoodIds.TryNormalizeUsdaId(
            input,
            out var normalizedId);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedId, normalizedId);
    }

    [Fact]
    public async Task Select_InvalidExternalIdReturnsBadRequestWithoutCallingProvider()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new FakeFoodSearchService();
        var controller = CreateController(database, service, "user-1");

        var result = await controller.Select("not-an-id");

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, service.GetCallCount);
        Assert.Empty(database.Context.Foods);
    }

    [Fact]
    public async Task Favourite_ResolvesServerDataAndPersistsForCurrentUser()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new FakeFoodSearchService
        {
            GetHandler = _ => Task.FromResult<FoodSearchResult?>(
                TestData.UsdaResult())
        };
        var controller = CreateController(database, service, "user-1");

        var result = await controller.Favourite("123");

        Assert.IsType<OkObjectResult>(result);
        var food = await database.Context.Foods.SingleAsync();
        Assert.Equal("user-1", food.UserId);
        Assert.Equal("USDA food", food.Name);
        Assert.True(food.IsFavourite);
    }

    [Fact]
    public async Task Unfavourite_ChangesOnlyCurrentUsersCachedFood()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        var first = CachedFood("user-1", "123", true);
        var second = CachedFood("user-2", "123", true);
        database.Context.Foods.AddRange(first, second);
        await database.Context.SaveChangesAsync();
        var controller = CreateController(
            database,
            new FakeFoodSearchService(),
            "user-1");

        var result = await controller.Unfavourite("123");

        Assert.IsType<OkObjectResult>(result);
        Assert.False(first.IsFavourite);
        Assert.True(second.IsFavourite);
    }

    [Fact]
    public async Task Resolver_DoesNotUseAnotherUsersCachedFallback()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        database.Context.Foods.Add(CachedFood("user-1", "123", true));
        await database.Context.SaveChangesAsync();
        var service = new FakeFoodSearchService
        {
            GetHandler = _ => throw new HttpRequestException("offline")
        };
        var resolver = new ExternalFoodResolver(database.Context, service);

        var resolution = await resolver.ResolveAsync("user-2", "123");

        Assert.Equal(ExternalFoodFailure.Unavailable, resolution.Failure);
        Assert.Null(resolution.Food);
        Assert.False(resolution.UsedCachedFallback);
    }

    [Fact]
    public async Task ProtectedApi_AnonymousRequestReturnsUnauthorized()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/api/foods/search?query=apple");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.FoodSearchService.SearchCallCount);
    }

    [Fact]
    public async Task ProtectedApi_AuthenticatedRequestCanSearch()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", "user-1");

        var response = await client.GetAsync("/api/foods/search?query=apple");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.FoodSearchService.SearchCallCount);
    }

    [Fact]
    public async Task ProtectedApi_MutationWithoutAntiforgeryTokenIsRejected()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", "user-1");

        var response = await client.PostAsync(
            "/api/foods/select/123",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.FoodSearchService.GetCallCount);
    }

    private static FoodsApiController CreateController(
        TestDatabase database,
        FakeFoodSearchService service,
        string userId)
    {
        var resolver = new ExternalFoodResolver(database.Context, service);
        var controller = new FoodsApiController(
            service,
            database.Context,
            PageModelTestContext.CreateUserManager(),
            resolver);
        PageModelTestContext.Attach(controller, userId);
        return controller;
    }

    private static Food CachedFood(
        string userId,
        string externalId,
        bool favourite)
    {
        var food = TestData.Food(userId);
        food.Source = FoodSources.Usda;
        food.ExternalId = externalId;
        food.IsFavourite = favourite;
        return food;
    }
}
