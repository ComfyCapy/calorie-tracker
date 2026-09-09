using CalorieTracker.Models;

namespace CalorieTracker.Services;

public sealed record MacroProgress(
    decimal ConsumedGrams,
    decimal TargetGrams,
    decimal Percentage);

public sealed record MacroGoalProgress(
    MacroProgress Protein,
    MacroProgress Carbohydrates,
    MacroProgress Fat);

public sealed record MacroTargets(
    decimal ProteinGrams,
    decimal CarbohydratesGrams,
    decimal FatGrams,
    decimal DailyCalorieTarget)
{
    public decimal MacroCalories =>
        (ProteinGrams * MacroTargetCalculator.ProteinCaloriesPerGram) +
        (CarbohydratesGrams * MacroTargetCalculator.CarbohydrateCaloriesPerGram) +
        (FatGrams * MacroTargetCalculator.FatCaloriesPerGram);

    public MacroGoalProgress CreateProgress(
        decimal proteinConsumed,
        decimal carbohydratesConsumed,
        decimal fatConsumed) =>
        new(
            CreateProgress(proteinConsumed, ProteinGrams),
            CreateProgress(carbohydratesConsumed, CarbohydratesGrams),
            CreateProgress(fatConsumed, FatGrams));

    private static MacroProgress CreateProgress(
        decimal consumedGrams,
        decimal targetGrams)
    {
        var percentage = consumedGrams <= 0 || targetGrams <= 0
            ? 0
            : Math.Min(100, consumedGrams / targetGrams * 100);

        return new MacroProgress(
            consumedGrams,
            targetGrams,
            percentage);
    }
}

public sealed class MacroTargetCalculator
{
    public const decimal ProteinGramsPerKilogram = 1.6m;
    public const decimal FatGramsPerKilogram = 0.8m;
    public const decimal ProteinCaloriesPerGram = 4m;
    public const decimal CarbohydrateCaloriesPerGram = 4m;
    public const decimal FatCaloriesPerGram = 9m;

    public MacroTargets? Calculate(
        UserProfile? profile,
        DateOnly currentDate)
    {
        if (profile?.HasUsableCalorieEstimatesOn(currentDate) != true)
        {
            return null;
        }

        var calorieTarget = profile.CalculateEffectiveCalorieTarget(currentDate);
        var proteinGrams = RoundGrams(
            profile.WeightKg * ProteinGramsPerKilogram);
        var fatGrams = RoundGrams(
            profile.WeightKg * FatGramsPerKilogram);
        var remainingCalories =
            calorieTarget -
            (proteinGrams * ProteinCaloriesPerGram) -
            (fatGrams * FatCaloriesPerGram);

        if (proteinGrams <= 0 ||
            fatGrams <= 0 ||
            remainingCalories <= 0)
        {
            return null;
        }

        var carbohydratesGrams = RoundGrams(
            remainingCalories / CarbohydrateCaloriesPerGram);

        if (carbohydratesGrams <= 0)
        {
            return null;
        }

        return new MacroTargets(
            proteinGrams,
            carbohydratesGrams,
            fatGrams,
            calorieTarget);
    }

    private static decimal RoundGrams(decimal grams) =>
        Math.Round(grams, 1, MidpointRounding.AwayFromZero);
}
