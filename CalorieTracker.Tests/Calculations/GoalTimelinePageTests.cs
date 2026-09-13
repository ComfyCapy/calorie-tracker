using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DashboardModel = CalorieTracker.Pages.IndexModel;
using ProfileModel = CalorieTracker.Pages.Profile.IndexModel;

namespace CalorieTracker.Tests.Calculations;

public class GoalTimelinePageTests
{
    private static readonly DateOnly CurrentDate = TestTime.Today;

    [Fact]
    public async Task DashboardAndProfileUseSameSavedCalculationsAndTimeline()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var profile = ValidProfile("user-1", ProfileOptions.Metric);
        database.Context.UserProfiles.Add(profile);
        await database.Context.SaveChangesAsync();
        var calculator = new GoalTimelineCalculator();

        var dashboard = new DashboardModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            calculator,
            new CalorieBalanceYearService(database.Context),
            new MacroTargetCalculator(),
            new TestUserLocalTimeProvider(CurrentDate));
        PageModelTestContext.Attach(dashboard, "user-1");
        await dashboard.OnGetAsync();

        var profilePage = new ProfileModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            calculator,
            new DailyMaintenanceSnapshotService(database.Context),
            new TestUserLocalTimeProvider(CurrentDate));
        PageModelTestContext.Attach(profilePage, "user-1");
        await profilePage.OnGetAsync();

        Assert.True(dashboard.HasProfileEstimates);
        Assert.Equal(
            profile.CalculateTdee(CurrentDate),
            dashboard.UserProfile!.CalculateTdee(CurrentDate));
        Assert.Equal(
            profile.CalculateBmr(CurrentDate),
            dashboard.UserProfile.CalculateBmr(CurrentDate));
        Assert.Equal(profile.BMI, dashboard.UserProfile.BMI);
        Assert.Equal(
            profile.CalculateAge(CurrentDate),
            dashboard.UserProfile.CalculateAge(CurrentDate));
        Assert.Equal(GoalTimelineStatus.ProjectionAvailable, dashboard.GoalTimeline.Status);
        Assert.Equal(dashboard.GoalTimeline, profilePage.GoalTimeline);
    }

    [Theory]
    [InlineData(ProfileOptions.Metric, "~0.5 kg/week")]
    [InlineData(ProfileOptions.Imperial, "~1.1 lb/week")]
    public async Task DashboardAndProfileRenderSelectedUnitsAndEstimateWording(
        string measurementSystem,
        string expectedWeeklyChange)
    {
        const string userId = "timeline-user";
        using var factory = new IntegrationTestFactory();
        await SeedProfileAsync(factory, userId, measurementSystem);
        using var client = AuthenticatedClient(factory, userId);

        var dashboard = await client.GetStringAsync("/");
        var profile = await client.GetStringAsync("/Profile/Index");

        foreach (var html in new[] { dashboard, profile })
        {
            Assert.Contains("data-goal-timeline-status=\"ProjectionAvailable\"", html);
            Assert.Contains(expectedWeeklyChange, html);
            Assert.Contains("Around", html);
            Assert.Contains("At your current target", html);
            Assert.Contains("This is an estimate. Actual progress may vary.", html);
        }

        Assert.Contains("<dt>Maintenance</dt>", dashboard);
        Assert.Contains("BMR", dashboard);
        Assert.Contains("BMI", dashboard);
        Assert.Contains("Age", dashboard);
    }

    [Fact]
    public async Task ProfileRendersStructuredGoalWorkspaceWithExistingControls()
    {
        const string userId = "profile-layout-user";
        using var factory = new IntegrationTestFactory();
        await SeedProfileAsync(factory, userId, ProfileOptions.Metric);
        using var client = AuthenticatedClient(factory, userId);

        var profile = await client.GetStringAsync("/Profile/Index");

        Assert.Contains("class=\"profile-page\"", profile);
        Assert.Contains(
            "class=\"ct-scenic-header secondary-page-hero profile-hero\"",
            profile);
        Assert.Contains("profile-body-card", profile);
        Assert.Contains("profile-activity-card", profile);
        Assert.Contains("profile-goal-card", profile);
        Assert.Contains("profile-target-card", profile);
        Assert.Contains("profile-target-highlight", profile);
        Assert.Contains("profile-motivation-card", profile);
        Assert.Contains("profile-tips-card", profile);
        Assert.Contains("Small steps still move you forward.", profile);
        Assert.DoesNotContain("dashboard-hero-capy", profile);
        Assert.Contains("Save changes", profile);
        Assert.Contains("id=\"metricSystem\"", profile);
        Assert.Contains("id=\"imperialSystem\"", profile);
        Assert.Contains("id=\"goalSelect\"", profile);
        Assert.Contains("id=\"weeklyGoalSelect\"", profile);
        Assert.Contains("id=\"calculatedTarget\"", profile);
        Assert.Contains("id=\"customTarget\"", profile);
        Assert.Contains("name=\"__RequestVerificationToken\"", profile);

        Assert.True(
            profile.IndexOf("profile-target-card", StringComparison.Ordinal)
            < profile.IndexOf("profile-column--supporting", StringComparison.Ordinal));
        Assert.True(
            profile.IndexOf("profile-goal-card", StringComparison.Ordinal)
            < profile.IndexOf("profile-motivation-card", StringComparison.Ordinal));
        Assert.True(
            profile.IndexOf("profile-motivation-card", StringComparison.Ordinal)
            < profile.IndexOf("profile-tips-card", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DashboardPlacesAlwaysVisibleProfileEstimatesAfterYearMap()
    {
        const string userId = "dashboard-layout-user";
        using var factory = new IntegrationTestFactory();
        await SeedProfileAsync(factory, userId, ProfileOptions.Metric);
        using var client = AuthenticatedClient(factory, userId);

        var dashboard = await client.GetStringAsync("/");

        Assert.Contains(
            "<section class=\"card dashboard-estimates-card\"",
            dashboard);
        Assert.Contains(
            "dashboard-estimate-stats",
            dashboard);
        Assert.DoesNotContain(
            "dashboard-estimates-summary",
            dashboard);
        Assert.DoesNotContain("<details", dashboard);

        var macrosIndex = dashboard.IndexOf(
            "dashboard-macro-grid",
            StringComparison.Ordinal);
        var estimatesIndex = dashboard.IndexOf(
            "dashboard-estimates-card",
            StringComparison.Ordinal);
        var yearMapIndex = dashboard.IndexOf(
            "dashboard-year-card",
            StringComparison.Ordinal);
        var actionsIndex = dashboard.IndexOf(
            "dashboard-actions",
            StringComparison.Ordinal);

        Assert.True(macrosIndex >= 0 && macrosIndex < yearMapIndex);
        Assert.True(yearMapIndex < estimatesIndex);
        Assert.True(estimatesIndex < actionsIndex);
    }

    [Fact]
    public async Task IncompleteProfileDoesNotRenderZeroEstimateMetrics()
    {
        const string userId = "incomplete-profile-user";
        using var factory = new IntegrationTestFactory();
        await SeedIncompleteProfileAsync(factory, userId);
        using var client = AuthenticatedClient(factory, userId);

        var dashboard = await client.GetStringAsync("/");
        var profile = await client.GetStringAsync("/Profile/Index");

        Assert.Contains("Update your profile", dashboard);
        Assert.DoesNotContain("Your profile estimates", dashboard);
        Assert.DoesNotContain("~0 kcal/day", dashboard);
        Assert.DoesNotContain("profile-target-highlight", profile);
        Assert.Contains("profile-target-empty", profile);
        Assert.DoesNotContain("~0 kcal/day", profile);
    }

    private static UserProfile ValidProfile(
        string userId,
        string measurementSystem)
    {
        var profile = new UserProfile
        {
            UserId = userId,
            MeasurementSystem = measurementSystem,
            ThemePreference = ProfileOptions.SystemTheme,
            DateOfBirth = new DateTime(1990, 1, 1),
            HeightCm = 180m,
            WeightKg = 80m,
            GoalWeightKg = 75m,
            CalculationSex = ProfileOptions.Male,
            ActivityLevel = ProfileOptions.Sedentary,
            Goal = ProfileOptions.Lose,
            WeeklyGoalKg = 0.5m
        };
        profile.CustomCalorieTarget = profile.CalculateTdee(CurrentDate) - 550m;
        return profile;
    }

    private static async Task SeedProfileAsync(
        IntegrationTestFactory factory,
        string userId,
        string measurementSystem)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AddUser(context, userId);
        context.UserProfiles.Add(ValidProfile(userId, measurementSystem));
        await context.SaveChangesAsync();
    }

    private static async Task SeedIncompleteProfileAsync(
        IntegrationTestFactory factory,
        string userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AddUser(context, userId);
        context.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            MeasurementSystem = ProfileOptions.Metric,
            ThemePreference = ProfileOptions.SystemTheme,
            DateOfBirth = CurrentDate.ToDateTime(TimeOnly.MinValue),
            HeightCm = 0,
            WeightKg = 0,
            CalculationSex = string.Empty,
            ActivityLevel = string.Empty,
            Goal = string.Empty
        });
        await context.SaveChangesAsync();
    }

    private static void AddUser(ApplicationDbContext context, string userId)
    {
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = $"{userId}@EXAMPLE.TEST",
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString()
        });
    }

    private static HttpClient AuthenticatedClient(
        IntegrationTestFactory factory,
        string userId)
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
