namespace CalorieTracker.Services;

public static class ApproximatePortions
{
    private static readonly IReadOnlyDictionary<string, decimal> Multipliers =
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["Small"] = 0.75m,
            ["Medium"] = 1m,
            ["Large"] = 1.5m
        };

    public static IReadOnlyCollection<string> Labels { get; } =
        ["Small", "Medium", "Large"];

    public static bool TryGetMultiplier(
        string? label,
        out decimal multiplier) =>
        Multipliers.TryGetValue(label ?? string.Empty, out multiplier);
}
