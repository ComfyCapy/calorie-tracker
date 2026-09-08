namespace CalorieTracker.Services;

public enum CalorieBalanceClassification
{
    HeavyCut,
    LightCut,
    Maintenance,
    LightGain,
    HeavyGain
}

public sealed record CalorieBalanceLegendDefinition(
    CalorieBalanceClassification Classification,
    string DisplayName,
    string PercentageRange);

public static class CalorieBalanceClassifier
{
    public const decimal HeavyCutMaximumPercentage = -20m;
    public const decimal MaintenanceMinimumPercentage = -5m;
    public const decimal MaintenanceMaximumPercentage = 5m;
    public const decimal HeavyGainMinimumPercentage = 20m;

    public static IReadOnlyList<CalorieBalanceLegendDefinition>
        LegendDefinitions { get; } =
        [
            new(
                CalorieBalanceClassification.HeavyCut,
                "Heavy Cut",
                $"≤ {HeavyCutMaximumPercentage:0}%"),
            new(
                CalorieBalanceClassification.LightCut,
                "Light Cut",
                $"> {HeavyCutMaximumPercentage:0}% and < {MaintenanceMinimumPercentage:0}%"),
            new(
                CalorieBalanceClassification.Maintenance,
                "Maintenance",
                $"{MaintenanceMinimumPercentage:0}% to +{MaintenanceMaximumPercentage:0}%"),
            new(
                CalorieBalanceClassification.LightGain,
                "Light Gain",
                $"> +{MaintenanceMaximumPercentage:0}% and < +{HeavyGainMinimumPercentage:0}%"),
            new(
                CalorieBalanceClassification.HeavyGain,
                "Heavy Gain",
                $"≥ +{HeavyGainMinimumPercentage:0}%")
        ];

    public static CalorieBalanceClassification Classify(
        decimal balancePercentage) => balancePercentage switch
        {
            <= HeavyCutMaximumPercentage =>
                CalorieBalanceClassification.HeavyCut,
            < MaintenanceMinimumPercentage =>
                CalorieBalanceClassification.LightCut,
            <= MaintenanceMaximumPercentage =>
                CalorieBalanceClassification.Maintenance,
            < HeavyGainMinimumPercentage =>
                CalorieBalanceClassification.LightGain,
            _ => CalorieBalanceClassification.HeavyGain
        };

    public static CalorieBalanceLegendDefinition GetDefinition(
        CalorieBalanceClassification classification) =>
        LegendDefinitions.Single(definition =>
            definition.Classification == classification);
}
