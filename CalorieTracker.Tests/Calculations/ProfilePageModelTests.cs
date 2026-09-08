using CalorieTracker.Models;
using CalorieTracker.Pages.Profile;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Calculations;

public class ProfilePageModelTests
{
    [Fact]
    public async Task SaveImperialProfile_StoresCanonicalMetricValues()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = CreateModel(database, "user-1");
        model.UserProfile = ValidProfile(ProfileOptions.Imperial);
        model.HeightFeet = 5;
        model.HeightInches = 10;
        model.WeightLb = 154.323583526m;

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        var saved = await database.Context.UserProfiles.SingleAsync();
        Assert.Equal(177.8m, saved.HeightCm, 3);
        Assert.Equal(70m, saved.WeightKg, 3);
    }

    [Fact]
    public async Task LoadImperialProfile_ConvertsCanonicalValuesForDisplay()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        database.Context.UserProfiles.Add(new UserProfile
        {
            UserId = "user-1",
            MeasurementSystem = ProfileOptions.Imperial,
            ThemePreference = ProfileOptions.SystemTheme,
            DateOfBirth = DateTime.Today.AddYears(-30),
            HeightCm = 177.8m,
            WeightKg = 70m,
            GoalWeightKg = 65m,
            CalculationSex = ProfileOptions.Male,
            ActivityLevel = ProfileOptions.Sedentary,
            Goal = ProfileOptions.Lose,
            WeeklyGoalKg = 0.5m
        });
        await database.Context.SaveChangesAsync();
        var model = CreateModel(database, "user-1");

        await model.OnGetAsync();

        Assert.Equal(5, model.HeightFeet);
        Assert.Equal(10m, model.HeightInches!.Value, 2);
        Assert.Equal(154.3236m, model.WeightLb!.Value, 3);
        Assert.Equal(143.3004m, model.GoalWeightLb!.Value, 3);
    }

    [Fact]
    public async Task CustomTarget_RemainsValidWhenUnusedCalculatedTargetIsNonPositive()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = CreateModel(database, "user-1");
        model.UserProfile = ValidProfile(ProfileOptions.Metric);
        model.UserProfile.DateOfBirth = DateTime.Today.AddYears(-120);
        model.UserProfile.HeightCm = 50;
        model.UserProfile.WeightKg = 20;
        model.UserProfile.CustomCalorieTarget = 500;
        model.UseCustomCalorieTarget = true;

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(
            500,
            (await database.Context.UserProfiles.SingleAsync())
                .EffectiveCalorieTarget);
    }

    [Theory]
    [InlineData(ProfileOptions.Lose, 82d, 80d)]
    [InlineData(ProfileOptions.Lose, 82d, 79.5d)]
    [InlineData(ProfileOptions.Gain, 78d, 80d)]
    [InlineData(ProfileOptions.Gain, 78d, 80.5d)]
    public async Task ExistingUnchangedGoal_CanBeReachedOrOvershot(
        string goal,
        double previousWeight,
        double updatedWeight)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");

        var existing = GoalProfile(
            "user-1",
            goal,
            previousWeight,
            80m);
        database.Context.UserProfiles.Add(existing);
        await database.Context.SaveChangesAsync();

        var model = CreateModel(database, "user-1");
        model.UserProfile = GoalProfile(
            "user-1",
            goal,
            updatedWeight,
            80m);

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(
            (decimal)updatedWeight,
            (await database.Context.UserProfiles.SingleAsync()).WeightKg);
    }

    [Theory]
    [InlineData(ProfileOptions.Lose, 82d, 80.5d)]
    [InlineData(ProfileOptions.Gain, 78d, 79.5d)]
    public async Task NewlyChangedGoal_StillRejectsInvalidDirection(
        string goal,
        double previousWeight,
        double changedGoalWeight)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");

        var existing = GoalProfile(
            "user-1",
            goal,
            previousWeight,
            80m);
        database.Context.UserProfiles.Add(existing);
        await database.Context.SaveChangesAsync();

        var model = CreateModel(database, "user-1");
        model.UserProfile = GoalProfile(
            "user-1",
            goal,
            80d,
            (decimal)changedGoalWeight);

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            (decimal)previousWeight,
            (await database.Context.UserProfiles.SingleAsync()).WeightKg);
    }

    [Fact]
    public async Task ProfilePost_IgnoresSubmittedUserIdAndUsesAuthenticatedUser()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("current-user", "current");
        await database.AddUserAsync("other-user", "other");
        var model = CreateModel(database, "current-user");
        model.UserProfile = ValidProfile(ProfileOptions.Metric);
        model.UserProfile.UserId = "other-user";

        await model.OnPostAsync();

        var profile = await database.Context.UserProfiles.SingleAsync();
        Assert.Equal("current-user", profile.UserId);
    }

    private static IndexModel CreateModel(
        TestDatabase database,
        string userId)
    {
        var model = new IndexModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new GoalTimelineCalculator(),
            new DailyMaintenanceSnapshotService(database.Context));
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static UserProfile ValidProfile(string measurementSystem) => new()
    {
        MeasurementSystem = measurementSystem,
        ThemePreference = ProfileOptions.SystemTheme,
        DateOfBirth = DateTime.Today.AddYears(-30),
        HeightCm = 180,
        WeightKg = 80,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.Sedentary,
        Goal = ProfileOptions.Maintain
    };

    private static UserProfile GoalProfile(
        string userId,
        string goal,
        double weight,
        decimal goalWeight) => new()
    {
        UserId = userId,
        MeasurementSystem = ProfileOptions.Metric,
        ThemePreference = ProfileOptions.SystemTheme,
        DateOfBirth = DateTime.Today.AddYears(-30),
        HeightCm = 180,
        WeightKg = (decimal)weight,
        GoalWeightKg = goalWeight,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.Sedentary,
        Goal = goal,
        WeeklyGoalKg = 0.5m
    };
}
