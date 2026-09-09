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
            "/Diary/Create?date=2026-09-07&meal=Lunch");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Search all foods", html);
        Assert.Contains("/Foods/Search", html);
        Assert.DoesNotContain("/Foods/Index", html[..html.IndexOf(
            "Search all foods",
            StringComparison.Ordinal)]);
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
}
