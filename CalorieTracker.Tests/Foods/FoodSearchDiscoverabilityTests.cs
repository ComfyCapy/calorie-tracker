using System.Net;
using CalorieTracker.Tests.TestSupport;

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
        Assert.Contains("Search the wider food catalogue", html);
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
        IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "food-search-user");
        return client;
    }
}
