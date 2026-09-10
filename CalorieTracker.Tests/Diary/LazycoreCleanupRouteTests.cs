using System.Net;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Diary;

public sealed class LazycoreCleanupRouteTests
{
    [Theory]
    [InlineData("/Recipes")]
    [InlineData("/Recipes/Create")]
    [InlineData("/Recipes/Details/1")]
    [InlineData("/Recipes/Edit/1")]
    [InlineData("/Recipes/Delete/1")]
    public async Task RemovedRecipeRoutesReturnNotFound(string route)
    {
        using var factory = new IntegrationTestFactory();
        using var client = AuthenticatedClient(factory);

        using var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DiaryKeepsOnlyPreviousDayCopyAndContextualSavedMealAction()
    {
        using var factory = new IntegrationTestFactory();
        using var client = AuthenticatedClient(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            context.Users.Add(new ApplicationUser
            {
                Id = "cleanup-user",
                UserName = "cleanup-user",
                NormalizedUserName = "CLEANUP-USER",
                SecurityStamp = Guid.NewGuid().ToString()
            });
            var food = TestData.Food("cleanup-user");
            context.Foods.Add(food);
            await context.SaveChangesAsync();
            var entry = TestData.DiaryEntry("cleanup-user", food, 1);
            entry.Date = new DateTime(2026, 9, 9);
            context.DiaryEntries.Add(entry);
            await context.SaveChangesAsync();
        }

        var html = await client.GetStringAsync("/Diary?date=2026-09-09");

        Assert.Contains("Copy previous day", html);
        Assert.Contains("/SavedMeals/Create", html);
        Assert.DoesNotContain("CopyMeal", html);
        Assert.DoesNotContain("Make recipe", html);
        Assert.DoesNotContain("/Recipes", html);
    }

    [Fact]
    public async Task SavedMealsIndexHasIntentionalDiaryNavigationWithoutStandaloneCreate()
    {
        using var factory = new IntegrationTestFactory();
        using var client = AuthenticatedClient(factory);

        var html = await client.GetStringAsync("/SavedMeals");

        Assert.DoesNotContain("/SavedMeals/Create", html);
        Assert.Contains("btn btn-outline-secondary mt-4", html);
        Assert.Contains("Back to Diary", html);
    }

    private static HttpClient AuthenticatedClient(
        IntegrationTestFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", "cleanup-user");
        return client;
    }
}
