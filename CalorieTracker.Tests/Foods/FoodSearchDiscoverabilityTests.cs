using System.Net;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Foods;

public class FoodSearchDiscoverabilityTests
{
    [Fact]
    public async Task MyFoods_OffersDistinctWiderSearchAndDoesNotMountEmbeddedSearch()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync(
            "/Foods/Index?searchTerm=banana&returnToDiary=true&diaryDate=2026-09-07&diaryMeal=Lunch");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Search your foods", html);
        Assert.Contains("Search all foods", html);
        Assert.Contains("Add Custom Food", html);
        Assert.DoesNotContain("Search the wider food catalogue.", html);
        Assert.Contains("searchTerm=banana", html);
        Assert.Contains("diaryDate=2026-09-07", html);
        Assert.Contains("diaryMeal=Lunch", html);
        Assert.DoesNotContain("id=\"react-food-search\"", html);
        Assert.DoesNotContain("data-embedded=", html);
    }

    [Fact]
    public async Task WiderSearch_PreservesSearchAndDiaryContext()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync(
            "/Foods/Search?searchTerm=banana&returnToDiary=true&diaryDate=2026-09-07&diaryMeal=Lunch");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Search all foods", html);
        Assert.Contains("data-initial-search-term=\"banana\"", html);
        Assert.Contains("data-diary-date=\"2026-09-07\"", html);
        Assert.Contains("data-diary-meal=\"Lunch\"", html);
        Assert.Contains("data-initial-provider=\"cofid\"", html);
    }

    [Fact]
    public async Task WiderSearch_AcceptsKnownProviderAndRejectsUnknownProvider()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateAuthenticatedClient(factory);

        var usResponse = await client.GetAsync(
            "/Foods/Search?provider=usda");
        var usHtml = await usResponse.Content.ReadAsStringAsync();
        var invalidResponse = await client.GetAsync(
            "/Foods/Search?provider=unsupported");

        Assert.Equal(HttpStatusCode.OK, usResponse.StatusCode);
        Assert.Contains("data-initial-provider=\"usda\"", usHtml);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public void WiderSearchClient_HasTwoProviderSelectorAndProvenance()
    {
        var source = File.ReadAllText(ProjectFile(
            "CalorieTracker",
            "ClientApp",
            "src",
            "App.jsx"));

        Assert.Contains("label: 'UK'", source);
        Assert.Contains("label: 'US'", source);
        Assert.DoesNotContain("label: 'All'", source);
        Assert.Contains("aria-pressed={provider === providerId}", source);
        Assert.Contains(
            "Database provided by CoFID — UK food composition data.",
            source);
        Assert.Contains(
            "Database provided by USDA FoodData Central — US food composition data.",
            source);
        Assert.Contains("UK food database — CoFID", source);
        Assert.Contains("US food database — USDA FoodData Central", source);
        Assert.Contains("provider: nextProvider", source);
        Assert.Contains("?provider=${encodeURIComponent(foodProvider)}", source);
        Assert.Contains("foodSearchProvider', foodProvider", source);
        Assert.DoesNotContain("food-search-provider-badge", source);
        Assert.DoesNotContain("{food.source}", source);
    }

    [Fact]
    public async Task MyFoodsFavouriteControlsExplainHowFavouritesHelp()
    {
        const string userId = "food-favourite-help-user";
        using var factory = new IntegrationTestFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"{userId}@example.test",
                NormalizedUserName = $"{userId}@EXAMPLE.TEST",
                Email = $"{userId}@example.test",
                NormalizedEmail = $"{userId}@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            var favourite = TestData.Food(userId, name: "Favourite food");
            favourite.IsFavourite = true;
            var custom = TestData.Food(userId, name: "Custom food");
            context.AddRange(user, favourite, custom);
            await context.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedClient(factory, userId);
        var html = await client.GetStringAsync("/Foods/Index");

        Assert.Contains(
            "id=\"food-favourite-help\"",
            html);
        Assert.Contains(
            "Favourite this food to find it faster in My Foods.",
            html);
        Assert.Equal(
            3,
            CountOccurrences(
                html,
                "aria-describedby=\"food-favourite-help\""));
        Assert.Contains(
            "aria-label=\"Remove Favourite food from Favourites\"",
            html);
        Assert.Contains(
            "aria-label=\"Add Custom food to Favourites\"",
            html);
        Assert.Contains(
            "title=\"Favourite this food to find it faster in My Foods.\"",
            html);
    }

    [Fact]
    public async Task DiaryCreate_SearchAllFoodsLinkUsesDedicatedSearchPage()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.GetAsync(
            "/Diary/Create?date=2026-09-07&meal=Lunch&returnToFoodSearch=true&foodSearchProvider=usda&foodSearchTerm=apple");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Search all foods", html);
        Assert.Contains("/Foods/Search", html);
        Assert.Contains("provider=usda", html);
        Assert.Contains("value=\"usda\"", html);
        Assert.DoesNotContain("/Foods/Index", html[..html.IndexOf(
            "Search all foods",
            StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task DiaryCreate_UsdaNamedPortionIsPrimaryAndGramAmountIsSecondary()
    {
        const string userId = "usda-portion-user";
        using var factory = new IntegrationTestFactory();
        int foodId;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"{userId}@example.test",
                NormalizedUserName = $"{userId}@EXAMPLE.TEST",
                Email = $"{userId}@example.test",
                NormalizedEmail = $"{userId}@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            var beer = TestData.Food(userId, name: "Beer, regular");
            beer.Source = CalorieTracker.Services.FoodSources.Usda;
            beer.ExternalId = "2710616";
            beer.Portions.Add(new FoodPortion
            {
                Name = "1 can or bottle (12 fl oz)",
                Amount = 360
            });
            context.AddRange(user, beer);
            await context.SaveChangesAsync();
            foodId = beer.Id;
        }

        using var client = CreateAuthenticatedClient(factory, userId);
        var html = await client.GetStringAsync($"/Diary/Create?foodId={foodId}");
        var script = File.ReadAllText(ProjectFile(
            "CalorieTracker",
            "wwwroot",
            "js",
            "diary-food.js"));

        Assert.Contains("\"isUsda\":true", html);
        Assert.Contains("\"name\":\"1 can or bottle (12 fl oz)\"", html);
        Assert.Contains("\"amount\":360", html);
        Assert.Contains("selectedFood.isUsda", script);
        Assert.Contains("? portionName", script);
        Assert.Contains("used for nutrition", script);
    }

    private static HttpClient CreateAuthenticatedClient(
        IntegrationTestFactory factory,
        string userId = "food-search-user")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", userId);
        return client;
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static string ProjectFile(params string[] segments) =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            Path.Combine(segments)));
}
