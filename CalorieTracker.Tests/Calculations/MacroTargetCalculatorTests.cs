using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DashboardModel = CalorieTracker.Pages.IndexModel;
using DiaryModel = CalorieTracker.Pages.Diary.IndexModel;

namespace CalorieTracker.Tests.Calculations;

public class MacroTargetCalculatorTests
{
    private readonly MacroTargetCalculator _calculator = new();

    [Fact]
    public void Calculate_UsesWeightAndEffectiveCalorieTargetDeterministically()
    {
        var profile = ValidProfile();
        profile.CustomCalorieTarget = 2376m;

        var first = _calculator.Calculate(profile);
        var second = _calculator.Calculate(profile);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(128m, first.ProteinGrams);
        Assert.Equal(322m, first.CarbohydratesGrams);
        Assert.Equal(64m, first.FatGrams);
        Assert.Equal(2376m, first.DailyCalorieTarget);
    }

    [Fact]
    public void Calculate_MacroCaloriesReconcileWithDailyTargetAfterRounding()
    {
        var profile = ValidProfile();
        profile.WeightKg = 80.06m;
        profile.CustomCalorieTarget = 2377m;

        var targets = _calculator.Calculate(profile);

        Assert.NotNull(targets);
        Assert.InRange(
            Math.Abs(targets.MacroCalories - targets.DailyCalorieTarget),
            0,
            0.2m);
        Assert.Equal(128.1m, targets.ProteinGrams);
        Assert.Equal(64m, targets.FatGrams);
    }

    [Fact]
    public void Calculate_UsesGoalAdjustedTargetWhenNoCustomTargetExists()
    {
        var profile = ValidProfile();
        profile.Goal = ProfileOptions.Lose;
        profile.WeeklyGoalKg = 0.5m;

        var targets = _calculator.Calculate(profile);

        Assert.NotNull(targets);
        Assert.Equal(profile.DailyCalorieTarget, targets.DailyCalorieTarget);
    }

    [Fact]
    public void Calculate_CustomTargetChangesCarbohydratesButKeepsWeightBasedTargets()
    {
        var profile = ValidProfile();
        profile.CustomCalorieTarget = 2400m;
        var first = _calculator.Calculate(profile)!;
        profile.CustomCalorieTarget = 2800m;

        var second = _calculator.Calculate(profile);

        Assert.NotNull(second);
        Assert.Equal(first.ProteinGrams, second.ProteinGrams);
        Assert.Equal(first.FatGrams, second.FatGrams);
        Assert.Equal(first.CarbohydratesGrams + 100m, second.CarbohydratesGrams);
    }

    [Fact]
    public void Calculate_MissingOrInvalidProfileReturnsNoTargets()
    {
        Assert.Null(_calculator.Calculate(null));
        Assert.Null(_calculator.Calculate(new UserProfile()));

        var invalid = ValidProfile();
        invalid.CustomCalorieTarget = 100m;
        Assert.Null(_calculator.Calculate(invalid));

        var impossible = ValidProfile();
        impossible.WeightKg = 500m;
        impossible.CustomCalorieTarget = 500m;
        Assert.Null(_calculator.Calculate(impossible));
    }

    [Fact]
    public void CreateProgress_CapsVisualPercentageButPreservesConsumedAmount()
    {
        var profile = ValidProfile();
        profile.CustomCalorieTarget = 2376m;
        var targets = _calculator.Calculate(profile)!;

        var progress = targets.CreateProgress(256m, 0m, 64m);

        Assert.Equal(256m, progress.Protein.ConsumedGrams);
        Assert.Equal(100m, progress.Protein.Percentage);
        Assert.Equal(0m, progress.Carbohydrates.Percentage);
        Assert.Equal(100m, progress.Fat.Percentage);
    }

    [Fact]
    public void CreateProgress_ReportsOrdinaryPercentage()
    {
        var profile = ValidProfile();
        profile.CustomCalorieTarget = 2376m;
        var targets = _calculator.Calculate(profile)!;

        var progress = targets.CreateProgress(64m, 161m, 16m);

        Assert.Equal(50m, progress.Protein.Percentage);
        Assert.Equal(50m, progress.Carbohydrates.Percentage);
        Assert.Equal(25m, progress.Fat.Percentage);
    }

    [Fact]
    public async Task DiaryAndDashboardConsumeTheSameCalculatedTargets()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        database.Context.UserProfiles.Add(ValidProfile("user-1"));
        await database.Context.SaveChangesAsync();

        var dashboard = new DashboardModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new GoalTimelineCalculator(),
            new CalorieBalanceYearService(database.Context),
            _calculator);
        PageModelTestContext.Attach(dashboard, "user-1");

        var diary = new DiaryModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            _calculator);
        PageModelTestContext.Attach(diary, "user-1");

        await dashboard.OnGetAsync();
        await diary.OnGetAsync(DateTime.Today);

        Assert.NotNull(dashboard.MacroGoalProgress);
        Assert.NotNull(diary.MacroGoalProgress);
        Assert.Equal(
            dashboard.MacroGoalProgress.Protein.TargetGrams,
            diary.MacroGoalProgress.Protein.TargetGrams);
        Assert.Equal(
            dashboard.MacroGoalProgress.Carbohydrates.TargetGrams,
            diary.MacroGoalProgress.Carbohydrates.TargetGrams);
        Assert.Equal(
            dashboard.MacroGoalProgress.Fat.TargetGrams,
            diary.MacroGoalProgress.Fat.TargetGrams);
    }

    [Fact]
    public async Task DiaryAndDashboardRenderSharedAccessibleGoalMarkupHiddenByDefault()
    {
        const string userId = "macro-markup-user";
        using var factory = new IntegrationTestFactory();
        await SeedProfileAsync(factory, userId);
        using var client = AuthenticatedClient(factory, userId);

        var diary = await client.GetStringAsync("/Diary");
        var dashboard = await client.GetStringAsync("/");

        Assert.Contains("data-macro-goals-toggle", diary);
        Assert.Contains("Show macro goals", diary);
        Assert.Contains("id=\"diaryMacroGoals\"", diary);
        Assert.Contains("data-macro-goals-content", diary);
        Assert.Contains("hidden", diary);

        foreach (var macro in new[] { "Protein", "Carbohydrates", "Fat" })
        {
            Assert.Contains($"aria-label=\"{macro} macro goal progress\"", diary);
            Assert.Contains($"aria-label=\"{macro} macro goal progress\"", dashboard);
        }

        Assert.Contains("data-macro-goals-compact", dashboard);
        Assert.Contains("macro-goals-preference", diary);
        Assert.Contains("macro-goals-preference", dashboard);
    }

    [Fact]
    public async Task PagesWithoutUsableProfileDoNotRenderMacroGoalControlsOrBars()
    {
        const string userId = "missing-macro-profile-user";
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
            var food = TestData.Food(userId);
            context.AddRange(user, food);
            await context.SaveChangesAsync();
            var entry = TestData.DiaryEntry(userId, food, 100m);
            entry.Date = DateTime.Today;
            context.DiaryEntries.Add(entry);
            await context.SaveChangesAsync();
        }

        using var client = AuthenticatedClient(factory, userId);
        var diary = await client.GetStringAsync("/Diary");
        var dashboard = await client.GetStringAsync("/");

        Assert.DoesNotContain("data-macro-goals-toggle", diary);
        Assert.DoesNotContain("macro goal progress", diary);
        Assert.DoesNotContain("macro goal progress", dashboard);
        Assert.Contains("10.0 g", diary);
        Assert.Contains("10.0 g", dashboard);
    }

    private static UserProfile ValidProfile(string userId = "user-1") => new()
    {
        UserId = userId,
        MeasurementSystem = ProfileOptions.Metric,
        ThemePreference = ProfileOptions.SystemTheme,
        DateOfBirth = DateTime.Today.AddYears(-35),
        HeightCm = 180m,
        WeightKg = 80m,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.ModeratelyActive,
        Goal = ProfileOptions.Maintain
    };

    private static async Task SeedProfileAsync(
        IntegrationTestFactory factory,
        string userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = $"{userId}@EXAMPLE.TEST",
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        var profile = ValidProfile(userId);
        profile.CustomCalorieTarget = 2376m;
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();
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
