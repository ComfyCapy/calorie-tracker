using CalorieTracker.Models;
using CalorieTracker.Services;

namespace CalorieTracker.Tests.Calculations;

public class UserProfileCalculationTests
{
    private static readonly DateOnly CurrentDate = new(2026, 9, 9);

    [Fact]
    public void Bmi_UsesMetricHeightAndWeight()
    {
        var profile = CreateProfile();

        Assert.Equal(24.69m, profile.BMI, 2);
    }

    [Fact]
    public void Bmi_WithNonPositiveHeight_ReturnsZero()
    {
        var profile = CreateProfile();
        profile.HeightCm = 0;

        Assert.Equal(0, profile.BMI);
    }

    [Theory]
    [InlineData(ProfileOptions.Male, 1780)]
    [InlineData(ProfileOptions.Female, 1614)]
    public void Bmr_UsesSupportedCalculationSexFormula(
        string calculationSex,
        int expected)
    {
        var profile = CreateProfile();
        profile.CalculationSex = calculationSex;

        Assert.Equal(expected, profile.CalculateBmr(CurrentDate));
    }

    [Theory]
    [InlineData(ProfileOptions.Sedentary, 2136)]
    [InlineData(ProfileOptions.LightlyActive, 2447.5)]
    [InlineData(ProfileOptions.ModeratelyActive, 2759)]
    [InlineData(ProfileOptions.VeryActive, 3070.5)]
    [InlineData(ProfileOptions.ExtraActive, 3382)]
    public void Tdee_MultipliesBmrBySupportedActivityLevel(
        string activityLevel,
        double expected)
    {
        var profile = CreateProfile();
        profile.ActivityLevel = activityLevel;

        Assert.Equal((decimal)expected, profile.CalculateTdee(CurrentDate), 2);
    }

    [Fact]
    public void DailyTarget_ForMaintenance_EqualsTdee()
    {
        var profile = CreateProfile();
        profile.Goal = ProfileOptions.Maintain;

        Assert.Equal(
            profile.CalculateTdee(CurrentDate),
            profile.CalculateDailyCalorieTarget(CurrentDate));
    }

    [Theory]
    [InlineData(ProfileOptions.Lose, 1586)]
    [InlineData(ProfileOptions.Gain, 2686)]
    public void DailyTarget_AppliesSupportedWeeklyGoal(
        string goal,
        int expected)
    {
        var profile = CreateProfile();
        profile.Goal = goal;
        profile.WeeklyGoalKg = 0.5m;

        Assert.Equal(expected, profile.CalculateDailyCalorieTarget(CurrentDate));
    }

    [Fact]
    public void DailyTarget_WithoutWeeklyGoal_FallsBackToMaintenance()
    {
        var profile = CreateProfile();
        profile.Goal = ProfileOptions.Lose;
        profile.WeeklyGoalKg = null;

        Assert.Equal(
            profile.CalculateTdee(CurrentDate),
            profile.CalculateDailyCalorieTarget(CurrentDate));
    }

    [Fact]
    public void EffectiveTarget_CustomValueOverridesCalculatedValue()
    {
        var profile = CreateProfile();
        profile.CustomCalorieTarget = 1950;

        Assert.Equal(1950, profile.CalculateEffectiveCalorieTarget(CurrentDate));
    }

    [Fact]
    public void Age_UsesCompletedYears()
    {
        var profile = CreateProfile();
        profile.DateOfBirth = CurrentDate.AddYears(-30).ToDateTime(TimeOnly.MinValue);

        Assert.Equal(30, profile.CalculateAge(CurrentDate));
    }

    [Fact]
    public void DateAwareCalculations_AreDeterministicForExplicitDate()
    {
        var profile = CreateProfile();

        Assert.Equal(30, profile.CalculateAge(CurrentDate));
        Assert.Equal(1780m, profile.CalculateBmr(CurrentDate));
        Assert.Equal(2136m, profile.CalculateTdee(CurrentDate));
    }

    [Fact]
    public void CalculateAge_HandlesDatesAroundBirthday()
    {
        var profile = CreateProfile();
        profile.DateOfBirth = new DateTime(1990, 6, 15);

        Assert.Equal(29, profile.CalculateAge(new DateOnly(2020, 6, 14)));
        Assert.Equal(30, profile.CalculateAge(new DateOnly(2020, 6, 15)));
        Assert.Equal(30, profile.CalculateAge(new DateOnly(2020, 6, 16)));
    }

    [Fact]
    public void CalculateAge_HandlesLeapDayBirthdaysUsingExistingDateSemantics()
    {
        var profile = CreateProfile();
        profile.DateOfBirth = new DateTime(2000, 2, 29);

        Assert.Equal(23, profile.CalculateAge(new DateOnly(2024, 2, 28)));
        Assert.Equal(24, profile.CalculateAge(new DateOnly(2024, 2, 29)));
        Assert.Equal(24, profile.CalculateAge(new DateOnly(2024, 3, 1)));
        Assert.Equal(24, profile.CalculateAge(new DateOnly(2025, 2, 28)));
        Assert.Equal(25, profile.CalculateAge(new DateOnly(2025, 3, 1)));
    }

    [Fact]
    public void TryCalculateMaintenance_UsesHistoricalAgeAndExistingFormula()
    {
        var profile = CreateProfile();
        profile.DateOfBirth = new DateTime(1980, 6, 15);
        var historicalDate = new DateOnly(2010, 6, 14);

        Assert.True(profile.TryCalculateMaintenance(historicalDate, out var maintenance));
        Assert.Equal(29, profile.CalculateAge(historicalDate));
        Assert.Equal(2142m, maintenance);
        Assert.Equal(profile.CalculateBmr(historicalDate) * 1.2m, maintenance);
    }

    [Fact]
    public void TryCalculateMaintenance_RejectsMissingOrUnsupportedProfileInputs()
    {
        var profile = CreateProfile();
        profile.DateOfBirth = null;

        Assert.False(profile.TryCalculateMaintenance(new DateOnly(2026, 1, 1), out var missingDateOfBirth));
        Assert.Equal(0, missingDateOfBirth);

        profile.DateOfBirth = new DateTime(2010, 1, 1);
        Assert.False(profile.TryCalculateMaintenance(new DateOnly(2026, 1, 1), out var unsupportedAge));
        Assert.Equal(0, unsupportedAge);

        profile.DateOfBirth = new DateTime(1990, 1, 1);
        profile.HeightCm = 0;
        Assert.False(profile.TryCalculateMaintenance(new DateOnly(2026, 1, 1), out var invalidHeight));
        Assert.Equal(0, invalidHeight);
    }

    [Fact]
    public void TryCalculateMaintenance_DoesNotUseGoalOrCustomTarget()
    {
        var profile = CreateProfile();
        profile.Goal = string.Empty;
        profile.WeeklyGoalKg = null;
        profile.CustomCalorieTarget = 100;

        Assert.True(profile.TryCalculateMaintenance(
            CurrentDate,
            out var maintenance));
        Assert.Equal(profile.CalculateTdee(CurrentDate), maintenance);
    }

    private static UserProfile CreateProfile() => new()
    {
        DateOfBirth = CurrentDate.AddYears(-30).ToDateTime(TimeOnly.MinValue),
        HeightCm = 180,
        WeightKg = 80,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.Sedentary,
        Goal = ProfileOptions.Maintain
    };
}
