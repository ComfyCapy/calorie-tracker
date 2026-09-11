using System.Net;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
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

        Assert.Contains("diary-hero", html);
        Assert.Contains("One day at a time.", html);
        Assert.Contains("dashboard-hero-capy.png", html);
        Assert.Contains("diary-layout", html);
        Assert.Contains("diary-summary-card", html);
        Assert.Contains("Daily Summary", html);
        Assert.Contains("diary-meal-card", html);
        Assert.Contains("diary-entry-row", html);
        Assert.Contains("/Diary/Edit?id=", html);
        Assert.Contains("/Diary/Delete?id=", html);
        Assert.Contains("Copy previous day", html);
        Assert.Contains("/SavedMeals/Create", html);
        Assert.DoesNotContain("CopyMeal", html);
        Assert.DoesNotContain("Make recipe", html);
        Assert.DoesNotContain("/Recipes", html);
    }

    [Fact]
    public async Task DiaryRendersAllEmptyMealsWithoutChangingActions()
    {
        using var factory = new IntegrationTestFactory();
        using var client = AuthenticatedClient(factory);

        var html = await client.GetStringAsync("/Diary?date=2026-09-09");

        Assert.Equal(
            4,
            html.Split("No foods logged yet.", StringSplitOptions.None).Length - 1);
        Assert.Equal(4, html.Split("aria-label=\"Add food to", StringSplitOptions.None).Length - 1);
        Assert.Contains("diary-summary-card", html);
    }

    [Fact]
    public async Task DiaryCalorieRingCapsItsVisualProgressAndReportsOverage()
    {
        const string userId = "diary-ring-user";
        using var factory = new IntegrationTestFactory();
        using var client = AuthenticatedClient(factory, userId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            context.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                NormalizedUserName = userId.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString()
            });
            context.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                MeasurementSystem = ProfileOptions.Metric,
                ThemePreference = ProfileOptions.SystemTheme,
                DateOfBirth = new DateTime(1990, 9, 9),
                HeightCm = 180m,
                WeightKg = 80m,
                CalculationSex = ProfileOptions.Male,
                ActivityLevel = ProfileOptions.ModeratelyActive,
                Goal = ProfileOptions.Maintain,
                CustomCalorieTarget = 500m
            });
            var food = TestData.Food(userId);
            context.Foods.Add(food);
            await context.SaveChangesAsync();

            var entry = TestData.DiaryEntry(userId, food, 650m);
            entry.Date = new DateTime(2026, 9, 9);
            context.DiaryEntries.Add(entry);
            await context.SaveChangesAsync();
        }

        var html = await client.GetStringAsync("/Diary?date=2026-09-09");

        Assert.Contains("diary-calorie-ring", html);
        Assert.Contains("--diary-calorie-progress: 100%", html);
        Assert.Contains("800 kcal over", html);
        Assert.DoesNotContain("progress-bar", html);
    }

    [Fact]
    public async Task SavedMealsIndexHasIntentionalDiaryNavigationWithoutStandaloneCreate()
    {
        using var factory = new IntegrationTestFactory();
        using var client = AuthenticatedClient(factory);

        var html = await client.GetStringAsync("/SavedMeals");

        Assert.DoesNotContain("/SavedMeals/Create", html);
        Assert.Contains("saved-meals-hero", html);
        Assert.Contains("Your favourite combinations, ready when you are.", html);
        Assert.Contains("dashboard-hero-capy.png", html);
        Assert.Contains("saved-meals-empty", html);
        Assert.Contains("Log a meal in your Diary, then save it so you can reuse the whole meal later.", html);
        Assert.Contains("Back to Diary", html);
    }

    private static HttpClient AuthenticatedClient(
        IntegrationTestFactory factory,
        string userId = "cleanup-user")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", userId);
        return client;
    }
}
