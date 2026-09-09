using CalorieTracker.Models;

namespace CalorieTracker.Services;

public enum GoalTimelineStatus
{
    IncompleteProfile,
    Maintenance,
    GoalReached,
    ProjectionAvailable,
    DirectionMismatch,
    NoMeaningfulEnergyDifference,
    OutsideSupportedRate,
    InvalidCalculation
}

public sealed record GoalTimelineResult(
    GoalTimelineStatus Status,
    string MeasurementSystem,
    decimal? EstimatedWeeklyChangeKg = null,
    decimal? RemainingWeightKg = null,
    int? EstimatedWeeks = null,
    DateOnly? EstimatedTargetDate = null);

public sealed class GoalTimelineCalculator
{
    public const decimal MaximumSupportedWeeklyChangeKg = 1m;
    public const decimal MinimumMeaningfulWeeklyChangeKg = 0.01m;

    private const decimal CaloriesPerKilogram = 7700m;
    private const decimal DaysPerWeek = 7m;

    public GoalTimelineResult Calculate(
        UserProfile? profile,
        DateOnly currentDate)
    {
        if (profile == null || IsIncomplete(profile))
        {
            return Result(GoalTimelineStatus.IncompleteProfile, profile);
        }

        if (!HasValidCalculationInputs(profile, currentDate))
        {
            return Result(GoalTimelineStatus.InvalidCalculation, profile);
        }

        decimal weeklyChangeKg;

        try
        {
            weeklyChangeKg =
                (profile.CalculateEffectiveCalorieTarget(currentDate) -
                 profile.CalculateTdee(currentDate)) *
                DaysPerWeek /
                CaloriesPerKilogram;
        }
        catch (OverflowException)
        {
            return Result(GoalTimelineStatus.InvalidCalculation, profile);
        }

        var absoluteWeeklyChangeKg = Math.Abs(weeklyChangeKg);

        if (profile.Goal == ProfileOptions.Maintain)
        {
            return absoluteWeeklyChangeKg < MinimumMeaningfulWeeklyChangeKg
                ? Result(GoalTimelineStatus.Maintenance, profile)
                : Result(
                    GoalTimelineStatus.DirectionMismatch,
                    profile,
                    weeklyChangeKg);
        }

        if (profile.GoalWeightKg == null)
        {
            return Result(GoalTimelineStatus.IncompleteProfile, profile);
        }

        var goalWeightKg = profile.GoalWeightKg.Value;

        if (goalWeightKg is < 20 or > 500)
        {
            return Result(GoalTimelineStatus.InvalidCalculation, profile);
        }

        if ((profile.Goal == ProfileOptions.Lose &&
             profile.WeightKg <= goalWeightKg) ||
            (profile.Goal == ProfileOptions.Gain &&
             profile.WeightKg >= goalWeightKg))
        {
            return Result(GoalTimelineStatus.GoalReached, profile);
        }

        if (absoluteWeeklyChangeKg < MinimumMeaningfulWeeklyChangeKg)
        {
            return Result(
                GoalTimelineStatus.NoMeaningfulEnergyDifference,
                profile,
                weeklyChangeKg);
        }

        var directionMatches =
            (profile.Goal == ProfileOptions.Lose && weeklyChangeKg < 0) ||
            (profile.Goal == ProfileOptions.Gain && weeklyChangeKg > 0);

        if (!directionMatches)
        {
            return Result(
                GoalTimelineStatus.DirectionMismatch,
                profile,
                weeklyChangeKg);
        }

        if (absoluteWeeklyChangeKg > MaximumSupportedWeeklyChangeKg)
        {
            return Result(
                GoalTimelineStatus.OutsideSupportedRate,
                profile,
                weeklyChangeKg);
        }

        var remainingWeightKg = Math.Abs(profile.WeightKg - goalWeightKg);
        decimal weeksDecimal;

        try
        {
            weeksDecimal = decimal.Ceiling(
                remainingWeightKg / absoluteWeeklyChangeKg);
        }
        catch (OverflowException)
        {
            return Result(GoalTimelineStatus.InvalidCalculation, profile);
        }

        if (weeksDecimal <= 0 || weeksDecimal > int.MaxValue)
        {
            return Result(GoalTimelineStatus.InvalidCalculation, profile);
        }

        var weeks = (int)weeksDecimal;
        DateOnly estimatedTargetDate;

        try
        {
            estimatedTargetDate = currentDate.AddDays(checked(weeks * 7));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result(GoalTimelineStatus.InvalidCalculation, profile);
        }
        catch (OverflowException)
        {
            return Result(GoalTimelineStatus.InvalidCalculation, profile);
        }

        return new GoalTimelineResult(
            GoalTimelineStatus.ProjectionAvailable,
            profile.MeasurementSystem,
            weeklyChangeKg,
            remainingWeightKg,
            weeks,
            estimatedTargetDate);
    }

    private static bool IsIncomplete(UserProfile profile) =>
        !profile.DateOfBirth.HasValue ||
        profile.HeightCm <= 0 ||
        profile.WeightKg <= 0 ||
        string.IsNullOrWhiteSpace(profile.CalculationSex) ||
        string.IsNullOrWhiteSpace(profile.ActivityLevel) ||
        string.IsNullOrWhiteSpace(profile.Goal) ||
        ((profile.Goal == ProfileOptions.Lose ||
          profile.Goal == ProfileOptions.Gain) &&
         !profile.GoalWeightKg.HasValue);

    private static bool HasValidCalculationInputs(
        UserProfile profile,
        DateOnly currentDate) =>
        profile.HasUsableCalorieEstimatesOn(currentDate) &&
        (profile.MeasurementSystem == ProfileOptions.Metric ||
         profile.MeasurementSystem == ProfileOptions.Imperial) &&
        ProfileOptions.Goals.Contains(profile.Goal);

    private static GoalTimelineResult Result(
        GoalTimelineStatus status,
        UserProfile? profile,
        decimal? weeklyChangeKg = null) =>
        new(
            status,
            profile?.MeasurementSystem ?? ProfileOptions.Metric,
            weeklyChangeKg);
}
