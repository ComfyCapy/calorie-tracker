using CalorieTracker.Models;
using CalorieTracker.Services;

namespace CalorieTracker.Tests.Calculations;

public class UserProfileCalculationTests
{
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

        Assert.Equal(expected, profile.BMR);
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

        Assert.Equal((decimal)expected, profile.TDEE, 2);
    }

    [Fact]
    public void DailyTarget_ForMaintenance_EqualsTdee()
    {
        var profile = CreateProfile();
        profile.Goal = ProfileOptions.Maintain;

        Assert.Equal(profile.TDEE, profile.DailyCalorieTarget);
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

        Assert.Equal(expected, profile.DailyCalorieTarget);
    }

    [Fact]
    public void DailyTarget_WithoutWeeklyGoal_FallsBackToMaintenance()
    {
        var profile = CreateProfile();
        profile.Goal = ProfileOptions.Lose;
        profile.WeeklyGoalKg = null;

        Assert.Equal(profile.TDEE, profile.DailyCalorieTarget);
    }

    [Fact]
    public void EffectiveTarget_CustomValueOverridesCalculatedValue()
    {
        var profile = CreateProfile();
        profile.CustomCalorieTarget = 1950;

        Assert.Equal(1950, profile.EffectiveCalorieTarget);
    }

    [Fact]
    public void Age_UsesCompletedYears()
    {
        var profile = CreateProfile();
        profile.DateOfBirth = DateTime.Today.AddYears(-30);

        Assert.Equal(30, profile.Age);
    }

    private static UserProfile CreateProfile() => new()
    {
        DateOfBirth = DateTime.Today.AddYears(-30),
        HeightCm = 180,
        WeightKg = 80,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.Sedentary,
        Goal = ProfileOptions.Maintain
    };
}
