using System.Collections.ObjectModel;

namespace CalorieTracker.Services;

public sealed record ProgressionLevelThreshold(
    int Level,
    int RequiredXp);

public sealed record ProgressionLevelResult(
    int CurrentLevel,
    int TotalXp,
    int CurrentLevelThreshold,
    int? NextLevelThreshold,
    int XpEarnedWithinLevel,
    int? XpSpanToNextLevel,
    int? XpRemainingToNextLevel,
    decimal ProgressPercentage,
    bool IsFinalLevel);

public sealed class ProgressionLevelCalculator
{
    public static IReadOnlyList<ProgressionLevelThreshold> Thresholds
        { get; } = new ReadOnlyCollection<ProgressionLevelThreshold>(
        [
            new(1, 0),
            new(2, 50),
            new(3, 125),
            new(4, 225),
            new(5, 350),
            new(6, 500),
            new(7, 700),
            new(8, 950),
            new(9, 1250),
            new(10, 1600)
        ]);

    public ProgressionLevelResult Calculate(int totalXp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalXp);

        var currentIndex = Thresholds.Count - 1;

        while (Thresholds[currentIndex].RequiredXp > totalXp)
        {
            currentIndex--;
        }

        var current = Thresholds[currentIndex];
        var xpEarnedWithinLevel = totalXp - current.RequiredXp;
        var isFinalLevel = currentIndex == Thresholds.Count - 1;

        if (isFinalLevel)
        {
            return new ProgressionLevelResult(
                current.Level,
                totalXp,
                current.RequiredXp,
                null,
                xpEarnedWithinLevel,
                null,
                null,
                100m,
                true);
        }

        var next = Thresholds[currentIndex + 1];
        var xpSpanToNextLevel = next.RequiredXp - current.RequiredXp;

        return new ProgressionLevelResult(
            current.Level,
            totalXp,
            current.RequiredXp,
            next.RequiredXp,
            xpEarnedWithinLevel,
            xpSpanToNextLevel,
            next.RequiredXp - totalXp,
            xpEarnedWithinLevel * 100m / xpSpanToNextLevel,
            false);
    }
}
