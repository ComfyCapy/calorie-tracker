using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        Assert.All(page.Foods, food =>
        {
            Assert.Equal(FoodCatalogueProviders.Usda, food.Provider);
            Assert.Equal(FoodSources.Usda, food.Source);
        });
        Assert.Equal(1, service.SearchCallCount);
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

    [Fact]
    public async Task Search_CofidUsesSelectedProviderAndCurrentUsersFavouriteState()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var favourite = CachedFood(
            "user-1",
            "cf21-apple",
            true,
            FoodSources.Cofid);
        database.Context.Foods.Add(favourite);
        await database.Context.SaveChangesAsync();
        var usda = new FakeFoodSearchService();
        var cofid = CofidProvider(
            CofidFood("cf21-apple", "Apple, eating"));
        var controller = CreateController(
            database,
            usda,
            "user-1",
            cofid);

        var action = await controller.Search(
            "apple",
            providerId: FoodCatalogueProviders.Cofid);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<FoodSearchPage>(result.Value);
        var food = Assert.Single(page.Foods);
        Assert.True(food.IsFavourite);
        Assert.Equal(FoodSources.Cofid, food.Source);
        Assert.Equal(FoodCatalogueProviders.Cofid, food.Provider);
        Assert.Equal(0, usda.SearchCallCount);
    }

    [Fact]
    public async Task Search_InvalidProviderReturnsBadRequestWithoutSearching()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new FakeFoodSearchService();
        var controller = CreateController(database, service, "user-1");

        var action = await controller.Search(
            "apple",
            providerId: "unsupported");

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(0, service.SearchCallCount);
    }

    [Fact]
    public async Task SuggestionsRankReusableFoodsAndDoNotLeakOtherUsersFoods()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        var favourite = TestData.Food("user-1", name: "Apple favourite");
        favourite.IsFavourite = true;
        var frequent = TestData.Food("user-1", name: "Apple frequent");
        frequent.Source = FoodSources.Usda;
        frequent.ExternalId = "frequent";
        var custom = TestData.Food("user-1", name: "Apple custom");
        var other = TestData.Food("user-2", name: "Apple private");
        other.IsFavourite = true;
        database.Context.Foods.AddRange(favourite, frequent, custom, other);
        await database.Context.SaveChangesAsync();
        database.Context.DiaryEntries.Add(
            TestData.DiaryEntry("user-1", frequent, 100));
        await database.Context.SaveChangesAsync();
        var controller = CreateController(
            database,
            new FakeFoodSearchService(),
            "user-1");

        var action = await controller.Suggestions(" apple ");

        var result = Assert.IsType<OkObjectResult>(action);
        using var json = JsonSerializer.SerializeToDocument(result.Value);
        var suggestions = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(3, suggestions.Count);
        Assert.Equal("Favourite", suggestions[0].GetProperty("category").GetString());
        Assert.Equal(JsonValueKind.Null, suggestions[1].GetProperty("category").ValueKind);
        Assert.Equal("Custom", suggestions[2].GetProperty("category").GetString());
        Assert.DoesNotContain(suggestions, item =>
            item.GetProperty("name").GetString() == "Apple private");
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
    public async Task Select_CofidPersistsAuthoritativeProviderNutritionAndVolumeBasis()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new FakeFoodSearchService();
        var cofid = CofidProvider(
            CofidFood(
                "cf21-wine",
                "Wine, red",
                servingUnit: "ml",
                calories: 78));
        var controller = CreateController(
            database,
            service,
            "user-1",
            cofid);

        var result = await controller.Select(
            "cf21-wine",
            FoodCatalogueProviders.Cofid);

        Assert.IsType<OkObjectResult>(result);
        var food = await database.Context.Foods.SingleAsync();
        Assert.Equal("user-1", food.UserId);
        Assert.Equal(FoodSources.Cofid, food.Source);
        Assert.Equal("cf21-wine", food.ExternalId);
        Assert.Equal("Wine, red", food.Name);
        Assert.Equal(78, food.Calories);
        Assert.Equal(100, food.ServingSize);
        Assert.Equal(100, food.CanonicalServingSize);
        Assert.Equal("ml", food.ServingUnit);
        Assert.Equal(0, service.GetCallCount);
    }

    [Fact]
    public async Task Favourite_CofidPersistsOnlySelectedUsersProviderRecord()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new FakeFoodSearchService();
        var cofid = CofidProvider(
            CofidFood("cf21-bread", "Bread, white"));
        var controller = CreateController(
            database,
            service,
            "user-1",
            cofid);

        var result = await controller.Favourite(
            "cf21-bread",
            FoodCatalogueProviders.Cofid);

        Assert.IsType<OkObjectResult>(result);
        var food = await database.Context.Foods.SingleAsync();
        Assert.Equal(FoodSources.Cofid, food.Source);
        Assert.True(food.IsFavourite);
        Assert.Equal(0, service.GetCallCount);
    }

    [Fact]
    public async Task Select_InvalidCofidIdFailsWithoutPersistingAnything()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new FakeFoodSearchService();
        var controller = CreateController(
            database,
            service,
            "user-1",
            CofidProvider(CofidFood("cf21-known", "Known food")));

        var result = await controller.Select(
            "cf21-forged",
            FoodCatalogueProviders.Cofid);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(database.Context.Foods);
        Assert.Equal(0, service.GetCallCount);
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
    public async Task SameExternalId_RemainsIndependentAcrossProvidersAndUsers()
    {
        const string sharedId = "shared-id";
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        var catalogue = new FoodCatalogue(
        [
            new FakeFoodCatalogueProvider(
                FoodCatalogueProviders.Usda,
                FoodSources.Usda,
                sharedId,
                "USDA shared food",
                101),
            new FakeFoodCatalogueProvider(
                FoodCatalogueProviders.Cofid,
                FoodSources.Cofid,
                sharedId,
                "CoFID shared food",
                202)
        ]);
        var firstController = CreateController(database, catalogue, "user-1");
        var secondController = CreateController(database, catalogue, "user-2");

        Assert.IsType<OkObjectResult>(await firstController.Favourite(
            sharedId,
            FoodCatalogueProviders.Usda));
        Assert.IsType<OkObjectResult>(await firstController.Favourite(
            sharedId,
            FoodCatalogueProviders.Cofid));
        Assert.IsType<OkObjectResult>(await secondController.Favourite(
            sharedId,
            FoodCatalogueProviders.Usda));
        Assert.IsType<OkObjectResult>(await firstController.Unfavourite(
            sharedId,
            FoodCatalogueProviders.Usda));

        var foods = await database.Context.Foods
            .AsNoTracking()
            .OrderBy(food => food.UserId)
            .ThenBy(food => food.Source)
            .ToListAsync();
        Assert.Equal(3, foods.Count);
        var firstUsda = foods.Single(food =>
            food.UserId == "user-1" && food.Source == FoodSources.Usda);
        var firstCofid = foods.Single(food =>
            food.UserId == "user-1" && food.Source == FoodSources.Cofid);
        var secondUsda = foods.Single(food =>
            food.UserId == "user-2" && food.Source == FoodSources.Usda);
        Assert.False(firstUsda.IsFavourite);
        Assert.Equal("USDA shared food", firstUsda.Name);
        Assert.Equal(101, firstUsda.Calories);
        Assert.True(firstCofid.IsFavourite);
        Assert.Equal("CoFID shared food", firstCofid.Name);
        Assert.Equal(202, firstCofid.Calories);
        Assert.True(secondUsda.IsFavourite);
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
        var resolver = new ExternalFoodResolver(
            database.Context,
            TestFoodCatalogue.Create(service));

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
    public async Task ProtectedApi_CofidSearchUsesEmbeddedCatalogueNotUsda()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", "user-1");

        var response = await client.GetAsync(
            "/api/foods/search?query=crumpet&provider=cofid");
        var responseBody = await response.Content.ReadAsStringAsync();
        var page = await response.Content.ReadFromJsonAsync<FoodSearchPage>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.True(
            page.Foods.Any(food => food.Name.Contains(
                "Crumpet",
                StringComparison.OrdinalIgnoreCase)),
            $"USDA calls: {factory.FoodSearchService.SearchCallCount}; body: {responseBody}");
        Assert.All(page.Foods, food =>
            Assert.Equal(FoodSources.Cofid, food.Source));
        Assert.Equal(0, factory.FoodSearchService.SearchCallCount);
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

    [Theory]
    [InlineData("/api/foods/select/123")]
    [InlineData("/api/foods/favourites/123")]
    public async Task ResolverApiMutation_IsProtectedByFoodSearchRateLimit(
        string path)
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", "rate-limit-user");

        for (var attempt = 0; attempt < 60; attempt++)
        {
            using var response = await client.PostAsync(path, content: null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var limitedResponse = await client.PostAsync(path, content: null);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            limitedResponse.StatusCode);
        Assert.Equal(0, factory.FoodSearchService.GetCallCount);
    }

    [Fact]
    public async Task ApiFoodResolver_IsRateLimitedPerAuthenticatedUser()
    {
        using var factory = new IntegrationTestFactory();
        using var firstClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        firstClient.DefaultRequestHeaders.Add("X-Test-User", "first-rate-user");

        for (var attempt = 0; attempt < 60; attempt++)
        {
            using var response = await firstClient.GetAsync(
                "/Foods/ApiFood?id=123&searchTerm=apple");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var limitedResponse = await firstClient.GetAsync(
            "/Foods/ApiFood?id=123&searchTerm=apple");
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            limitedResponse.StatusCode);

        using var secondClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        secondClient.DefaultRequestHeaders.Add(
            "X-Test-User",
            "second-rate-user");
        using var independentResponse = await secondClient.GetAsync(
            "/Foods/ApiFood?id=123&searchTerm=apple");

        Assert.Equal(HttpStatusCode.NotFound, independentResponse.StatusCode);
        Assert.Equal(61, factory.FoodSearchService.GetCallCount);
    }

    private static FoodsApiController CreateController(
        TestDatabase database,
        FakeFoodSearchService service,
        string userId,
        params IFoodCatalogueProvider[] additionalProviders)
    {
        var catalogue = TestFoodCatalogue.Create(
            service,
            additionalProviders);
        return CreateController(database, catalogue, userId);
    }

    private static FoodsApiController CreateController(
        TestDatabase database,
        FoodCatalogue catalogue,
        string userId)
    {
        var resolver = new ExternalFoodResolver(database.Context, catalogue);
        var controller = new FoodsApiController(
            catalogue,
            database.Context,
            PageModelTestContext.CreateUserManager(),
            resolver);
        PageModelTestContext.Attach(controller, userId);
        return controller;
    }

    private static Food CachedFood(
        string userId,
        string externalId,
        bool favourite,
        string source = FoodSources.Usda)
    {
        var food = TestData.Food(userId);
        food.Source = source;
        food.ExternalId = externalId;
        food.IsFavourite = favourite;
        return food;
    }

    private static CofidFoodCatalogueProvider CofidProvider(
        params CofidFoodRecord[] foods) => new(foods);

    private static CofidFoodRecord CofidFood(
        string id,
        string name,
        string servingUnit = "g",
        decimal calories = 100) =>
        new()
        {
            Id = id,
            SourceCode = "13-001",
            SourceRow = 4,
            Name = name,
            Description = "Test catalogue food",
            Group = servingUnit == "ml" ? "QE" : "A",
            Calories = calories,
            Protein = 5,
            Carbohydrates = 10,
            Fat = 4,
            ServingSize = 100,
            ServingUnit = servingUnit
        };
}
