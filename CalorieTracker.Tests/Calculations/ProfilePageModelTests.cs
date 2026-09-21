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
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(ValidProfile(ProfileOptions.Imperial));
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
            DateOfBirth = TestTime.Today.AddYears(-30).ToDateTime(TimeOnly.MinValue),
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
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(ValidProfile(ProfileOptions.Metric));
        model.UserProfile.DateOfBirth = TestTime.Today
            .AddYears(-120)
            .ToDateTime(TimeOnly.MinValue);
        model.UserProfile.HeightCm = 50;
        model.UserProfile.WeightKg = 20;
        model.UserProfile.CustomCalorieTarget = 500;
        model.UseCustomCalorieTarget = true;

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(
            500,
            (await database.Context.UserProfiles.SingleAsync())
                .CalculateEffectiveCalorieTarget(TestTime.Today));
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
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(GoalProfile(
            "user-1",
            goal,
            updatedWeight,
            80m));

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
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(GoalProfile(
            "user-1",
            goal,
            80d,
            (decimal)changedGoalWeight));

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            (decimal)previousWeight,
            (await database.Context.UserProfiles.SingleAsync()).WeightKg);
    }

    [Fact]
    public async Task ProfilePost_UsesAuthenticatedUser()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("current-user", "current");
        await database.AddUserAsync("other-user", "other");
        var model = CreateModel(database, "current-user");
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(ValidProfile(ProfileOptions.Metric));
        // HTTP overposting is covered by ProfileHttpBindingTests; the input has no UserId.

        await model.OnPostAsync();

        var profile = await database.Context.UserProfiles.SingleAsync();
        Assert.Equal("current-user", profile.UserId);
    }

    [Theory]
    [InlineData("system", "UserProfile.MeasurementSystem", "Please select a valid measurement system.")]
    [InlineData("theme", "UserProfile.ThemePreference", "Please select a valid theme.")]
    [InlineData("sex", "UserProfile.CalculationSex", "Please select a valid calculation sex.")]
    [InlineData("activity", "UserProfile.ActivityLevel", "Please select a valid activity level.")]
    [InlineData("goal", "UserProfile.Goal", "Please select a valid goal.")]
    [InlineData("young", "UserProfile.DateOfBirth", "You must be between 18 and 120 years old.")]
    [InlineData("old", "UserProfile.DateOfBirth", "You must be between 18 and 120 years old.")]
    [InlineData("feet", "HeightFeet", "Please enter your height in feet.")]
    [InlineData("inches", "HeightInches", "Please enter your remaining height in inches.")]
    [InlineData("weight", "WeightLb", "Please enter your current weight.")]
    [InlineData("short", "HeightFeet", "Height must convert to between 50 cm and 300 cm.")]
    [InlineData("light", "WeightLb", "Weight must convert to between 20 kg and 500 kg.")]
    [InlineData("target", "UserProfile.CustomCalorieTarget", "Please enter a custom calorie target.")]
    [InlineData("goal-weight", "UserProfile.GoalWeightKg", "Please enter a goal weight.")]
    [InlineData("weekly", "UserProfile.WeeklyGoalKg", "Please select a weekly weight change.")]
    [InlineData("weekly-invalid", "UserProfile.WeeklyGoalKg", "Please select a valid weekly weight change.")]
    public async Task InvalidProfile_PreservesFieldMessagesAndDoesNotSave(string scenario, string key, string message)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = CreateModel(database, "user-1");
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(ValidProfile(ProfileOptions.Metric));
        if (scenario is "feet" or "inches" or "weight" or "short" or "light")
        {
            model.UserProfile.MeasurementSystem = ProfileOptions.Imperial;
            model.HeightFeet = 5; model.HeightInches = 10; model.WeightLb = 154;
        }
        switch (scenario)
        {
            case "system": model.UserProfile.MeasurementSystem = "invalid"; break;
            case "theme": model.UserProfile.ThemePreference = "invalid"; break;
            case "sex": model.UserProfile.CalculationSex = "invalid"; break;
            case "activity": model.UserProfile.ActivityLevel = "invalid"; break;
            case "goal": model.UserProfile.Goal = "invalid"; break;
            case "young": model.UserProfile.DateOfBirth = TestTime.Today.AddYears(-17).ToDateTime(TimeOnly.MinValue); break;
            case "old": model.UserProfile.DateOfBirth = TestTime.Today.AddYears(-121).ToDateTime(TimeOnly.MinValue); break;
            case "feet": model.HeightFeet = null; break;
            case "inches": model.HeightInches = null; break;
            case "weight": model.WeightLb = null; break;
            case "short": model.HeightFeet = 1; model.HeightInches = 0; break;
            case "light": model.WeightLb = 44; break;
            case "target": model.UseCustomCalorieTarget = true; break;
            default:
                model.UserProfile.Goal = ProfileOptions.Lose;
                model.UserProfile.GoalWeightKg = scenario == "goal-weight" ? null : 70;
                model.UserProfile.WeeklyGoalKg = scenario == "weekly" ? null : scenario == "weekly-invalid" ? 0.3m : 0.5m;
                break;
        }
        Assert.IsType<PageResult>(await model.OnPostAsync());
        Assert.Equal(message, Assert.Single(model.ModelState[key]!.Errors).ErrorMessage);
        Assert.True(model.IsFirstTimeSetup);
        Assert.Empty(await database.Context.UserProfiles.ToListAsync());
    }

    [Fact]
    public async Task FutureDate_AccumulatesBothDateAndAgeErrors()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = CreateModel(database, "user-1");
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(ValidProfile(ProfileOptions.Metric));
        model.UserProfile.DateOfBirth = TestTime.Today.AddDays(1).ToDateTime(TimeOnly.MinValue);
        Assert.IsType<PageResult>(await model.OnPostAsync());
        Assert.Equal(new[] { "Date of birth cannot be in the future.", "You must be between 18 and 120 years old." },
            model.ModelState["UserProfile.DateOfBirth"]!.Errors.Select(e => e.ErrorMessage));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaintainAndCalculatedMode_ClearStaleFieldsAndTheirErrors(bool imperial)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = CreateModel(database, "user-1");
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(ValidProfile(imperial ? ProfileOptions.Imperial : ProfileOptions.Metric));
        model.HeightFeet = 5; model.HeightInches = 10; model.WeightLb = 154;
        model.UserProfile.GoalWeightKg = 999;
        model.UserProfile.WeeklyGoalKg = 99;
        model.GoalWeightLb = 1;
        model.UserProfile.CustomCalorieTarget = 1;
        string[] keys = ["UserProfile.GoalWeightKg", "UserProfile.WeeklyGoalKg", "GoalWeightLb", "UserProfile.CustomCalorieTarget"];
        foreach (var key in keys) model.ModelState.AddModelError(key, "stale");
        if (imperial)
        {
            model.ModelState.AddModelError("UserProfile.HeightCm", "stale");
            model.ModelState.AddModelError("UserProfile.WeightKg", "stale");
        }
        Assert.IsType<RedirectToPageResult>(await model.OnPostAsync());
        Assert.All(keys, key => Assert.False(model.ModelState.ContainsKey(key)));
        var saved = await database.Context.UserProfiles.SingleAsync();
        Assert.Null(saved.GoalWeightKg);
        Assert.Null(saved.WeeklyGoalKg);
        Assert.Null(saved.CustomCalorieTarget);
        Assert.Null(model.GoalWeightLb);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CalculatedTarget_ErrorOnlyAppearsAfterOtherErrorsAreCleared(bool bindingError)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var model = CreateModel(database, "user-1");
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(ValidProfile(ProfileOptions.Metric));
        model.UserProfile.DateOfBirth = TestTime.Today.AddYears(-120).ToDateTime(TimeOnly.MinValue);
        model.UserProfile.HeightCm = 50;
        model.UserProfile.WeightKg = 20;
        if (bindingError) model.ModelState.AddModelError("HeightFeet", "binding error");
        Assert.IsType<PageResult>(await model.OnPostAsync());
        Assert.Equal(!bindingError, model.ModelState.ContainsKey(string.Empty));
        if (!bindingError) Assert.Equal("These profile values do not produce a valid calculated calorie target.",
            Assert.Single(model.ModelState[string.Empty]!.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData("0.009", true)]
    [InlineData("0.01", false)]
    public async Task ExistingGoalException_HasStrictTolerance(string difference, bool allowed)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        database.Context.UserProfiles.Add(GoalProfile("user-1", ProfileOptions.Lose, 82, 80));
        await database.Context.SaveChangesAsync();
        var model = CreateModel(database, "user-1");
        model.UserProfile = CalorieTracker.Pages.Profile.ProfileInput.FromEntity(GoalProfile("user-1", ProfileOptions.Lose, 79,
            80 + decimal.Parse(difference, System.Globalization.CultureInfo.InvariantCulture)));
        var result = await model.OnPostAsync();
        Assert.Equal(allowed, result is RedirectToPageResult);
    }

    private static IndexModel CreateModel(
        TestDatabase database,
        string userId)
    {
        var model = new IndexModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new GoalTimelineCalculator(),
            new DailyMaintenanceSnapshotService(database.Context),
            new TestUserLocalTimeProvider(),
            PageModelTestContext.CreateProgressionHooks(database.Context));
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static UserProfile ValidProfile(string measurementSystem) => new()
    {
        MeasurementSystem = measurementSystem,
        ThemePreference = ProfileOptions.SystemTheme,
        DateOfBirth = TestTime.Today.AddYears(-30).ToDateTime(TimeOnly.MinValue),
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
        DateOfBirth = TestTime.Today.AddYears(-30).ToDateTime(TimeOnly.MinValue),
        HeightCm = 180,
        WeightKg = (decimal)weight,
        GoalWeightKg = goalWeight,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.Sedentary,
        Goal = goal,
        WeeklyGoalKg = 0.5m
    };
}
