using CalorieTracker.Models;
using CalorieTracker.Services;

namespace CalorieTracker.Tests.Calculations;

public class GoalTimelineCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 7);
    private readonly GoalTimelineCalculator _calculator = new();

    [Fact]
    public void NormalWeightLoss_ReturnsProjection()
    {
        var profile = Profile(ProfileOptions.Lose, 71m);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) - 550m;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.ProjectionAvailable, result.Status);
        Assert.Equal(-0.5m, result.EstimatedWeeklyChangeKg);
        Assert.Equal(18, result.EstimatedWeeks);
        Assert.Equal(new DateOnly(2027, 1, 11), result.EstimatedTargetDate);
    }

    [Fact]
    public void NormalWeightGain_ReturnsProjection()
    {
        var profile = Profile(ProfileOptions.Gain, 81m);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) + 385m;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.ProjectionAvailable, result.Status);
        Assert.Equal(0.35m, result.EstimatedWeeklyChangeKg);
        Assert.Equal(3, result.EstimatedWeeks);
    }

    [Fact]
    public void CustomTarget_OverridesSelectedWeeklyGoal()
    {
        var profile = Profile(ProfileOptions.Lose, 77m);
        profile.WeeklyGoalKg = 0.25m;
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) - 825m;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(-0.75m, result.EstimatedWeeklyChangeKg);
        Assert.Equal(4, result.EstimatedWeeks);
    }

    [Fact]
    public void MaintenanceGoal_HasNoProjection()
    {
        var profile = Profile(ProfileOptions.Maintain, null);

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.Maintenance, result.Status);
        Assert.Null(result.EstimatedTargetDate);
    }

    [Theory]
    [InlineData(-10, GoalTimelineStatus.Maintenance)]
    [InlineData(10, GoalTimelineStatus.Maintenance)]
    [InlineData(-11, GoalTimelineStatus.DirectionMismatch)]
    [InlineData(11, GoalTimelineStatus.DirectionMismatch)]
    public void MaintenanceGoal_RespectsEffectiveCustomTarget(
        int calorieDelta,
        GoalTimelineStatus expectedStatus)
    {
        var profile = Profile(ProfileOptions.Maintain, null);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) + calorieDelta;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.EstimatedTargetDate);
    }

    [Fact]
    public void MaintenanceGoal_WithMeaningfulCustomDeficit_IsNotReportedAsMaintenance()
    {
        var profile = Profile(ProfileOptions.Maintain, null);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) - 110m;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.DirectionMismatch, result.Status);
        Assert.Equal(-0.1m, result.EstimatedWeeklyChangeKg);
        Assert.Null(result.EstimatedTargetDate);
    }

    [Fact]
    public void MaintenanceGoal_WithMeaningfulCustomSurplus_IsNotReportedAsMaintenance()
    {
        var profile = Profile(ProfileOptions.Maintain, null);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) + 110m;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.DirectionMismatch, result.Status);
        Assert.Equal(0.1m, result.EstimatedWeeklyChangeKg);
        Assert.Null(result.EstimatedTargetDate);
    }

    [Theory]
    [InlineData("Lose", 75, 75)]
    [InlineData("Lose", 70, 75)]
    [InlineData("Gain", 85, 85)]
    [InlineData("Gain", 90, 85)]
    public void ReachedOrOvershotGoal_ReturnsGoalReached(
        string goal,
        double currentWeight,
        double goalWeight)
    {
        var profile = Profile(
            goal,
            (decimal)goalWeight,
            (decimal)currentWeight);

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.GoalReached, result.Status);
        Assert.Null(result.EstimatedTargetDate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IncompleteProfile_ReturnsExplicitState(bool missingProfile)
    {
        var profile = missingProfile
            ? null
            : Profile(ProfileOptions.Lose, null);

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.IncompleteProfile, result.Status);
    }

    [Fact]
    public void ZeroEnergyDifference_HasNoMeaningfulProjection()
    {
        var profile = Profile(ProfileOptions.Lose, 75m);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today);

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(
            GoalTimelineStatus.NoMeaningfulEnergyDifference,
            result.Status);
    }

    [Fact]
    public void TinyEnergyDifference_HasNoMeaningfulProjection()
    {
        var profile = Profile(ProfileOptions.Lose, 75m);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) - 1m;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(
            GoalTimelineStatus.NoMeaningfulEnergyDifference,
            result.Status);
    }

    [Theory]
    [InlineData("Lose", 550)]
    [InlineData("Gain", -550)]
    public void TargetInOppositeDirection_ReturnsDirectionMismatch(
        string goal,
        double targetAdjustment)
    {
        var goalWeight = goal == ProfileOptions.Lose ? 75m : 85m;
        var profile = Profile(goal, goalWeight);
        profile.CustomCalorieTarget =
            profile.CalculateTdee(Today) + (decimal)targetAdjustment;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.DirectionMismatch, result.Status);
        Assert.Null(result.EstimatedTargetDate);
    }

    [Fact]
    public void ExactlyMaximumSupportedRate_ReturnsProjection()
    {
        var profile = Profile(ProfileOptions.Lose, 75m);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) - 1100m;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.ProjectionAvailable, result.Status);
        Assert.Equal(-1m, result.EstimatedWeeklyChangeKg);
    }

    [Fact]
    public void AboveMaximumSupportedRate_ReturnsOutsideSupportedRate()
    {
        var profile = Profile(ProfileOptions.Lose, 75m);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) - 1155m;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.OutsideSupportedRate, result.Status);
        Assert.Null(result.EstimatedTargetDate);
    }

    [Fact]
    public void MalformedCalculationInput_ReturnsInvalidCalculation()
    {
        var profile = Profile(ProfileOptions.Lose, 75m);
        profile.ActivityLevel = "unsupported";

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.InvalidCalculation, result.Status);
    }

    [Fact]
    public void ExtremeNumericInput_ReturnsInvalidCalculation()
    {
        var profile = Profile(ProfileOptions.Lose, 75m);
        profile.CustomCalorieTarget = decimal.MaxValue;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(GoalTimelineStatus.InvalidCalculation, result.Status);
    }

    [Fact]
    public void DateOverflow_ReturnsInvalidCalculation()
    {
        var profile = Profile(ProfileOptions.Lose, 79.5m);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) - 550m;

        var result = _calculator.Calculate(
            profile,
            new DateOnly(9999, 12, 26));

        Assert.Equal(GoalTimelineStatus.InvalidCalculation, result.Status);
        Assert.Null(result.EstimatedTargetDate);
    }

    [Fact]
    public void PartialWeek_RoundsUpToNextWholeWeekAndDate()
    {
        var profile = Profile(ProfileOptions.Lose, 78.99m);
        profile.CustomCalorieTarget = profile.CalculateTdee(Today) - 550m;

        var result = _calculator.Calculate(profile, Today);

        Assert.Equal(3, result.EstimatedWeeks);
        Assert.Equal(Today.AddDays(21), result.EstimatedTargetDate);
    }

    private static UserProfile Profile(
        string goal,
        decimal? goalWeightKg,
        decimal currentWeightKg = 80m) =>
        new()
        {
            DateOfBirth = new DateTime(1990, 1, 1),
            MeasurementSystem = ProfileOptions.Metric,
            HeightCm = 180m,
            WeightKg = currentWeightKg,
            GoalWeightKg = goalWeightKg,
            CalculationSex = ProfileOptions.Male,
            ActivityLevel = ProfileOptions.Sedentary,
            Goal = goal,
            WeeklyGoalKg = goal == ProfileOptions.Maintain ? null : 0.5m
        };
}
