using System.ComponentModel.DataAnnotations;
using CalorieTracker.Models;
using CalorieTracker.Services;

namespace CalorieTracker.Pages.Profile;

public sealed class ProfileInput
{
    [Required]
    public string MeasurementSystem { get; set; } = ProfileOptions.Metric;

    [Required]
    public string ThemePreference { get; set; } = ProfileOptions.SystemTheme;

    [Required(ErrorMessage = "Please enter your date of birth.")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Range(
        50,
        300,
        ErrorMessage = "Please enter a height between 50 cm and 300 cm.")]
    public decimal HeightCm { get; set; }

    [Range(
        20,
        500,
        ErrorMessage = "Please enter a weight between 20 kg and 500 kg.")]
    public decimal WeightKg { get; set; }

    [Required(ErrorMessage = "Please select a sex for the calorie calculation.")]
    public string CalculationSex { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select your activity level.")]
    public string ActivityLevel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select your goal.")]
    public string Goal { get; set; } = string.Empty;

    [Range(
        20,
        500,
        ErrorMessage = "Please enter a goal weight between 20 kg and 500 kg.")]
    public decimal? GoalWeightKg { get; set; }

    [Range(
        0.25,
        1.0,
        ErrorMessage = "Please select a weekly weight change between 0.25 kg and 1 kg.")]
    public decimal? WeeklyGoalKg { get; set; }

    [Range(
        500,
        10000,
        ErrorMessage = "Please enter a calorie target between 500 and 10,000 kcal.")]
    public decimal? CustomCalorieTarget { get; set; }


    public UserProfile ToEntity() => new()
    {
        MeasurementSystem = MeasurementSystem,
        ThemePreference = ThemePreference,
        DateOfBirth = DateOfBirth,
        HeightCm = HeightCm,
        WeightKg = WeightKg,
        CalculationSex = CalculationSex,
        ActivityLevel = ActivityLevel,
        Goal = Goal,
        GoalWeightKg = GoalWeightKg,
        WeeklyGoalKg = WeeklyGoalKg,
        CustomCalorieTarget = CustomCalorieTarget,
    };

    public static ProfileInput FromEntity(UserProfile profile) => new()
    {
        MeasurementSystem = profile.MeasurementSystem,
        ThemePreference = profile.ThemePreference,
        DateOfBirth = profile.DateOfBirth,
        HeightCm = profile.HeightCm,
        WeightKg = profile.WeightKg,
        CalculationSex = profile.CalculationSex,
        ActivityLevel = profile.ActivityLevel,
        Goal = profile.Goal,
        GoalWeightKg = profile.GoalWeightKg,
        WeeklyGoalKg = profile.WeeklyGoalKg,
        CustomCalorieTarget = profile.CustomCalorieTarget,
    };
}
