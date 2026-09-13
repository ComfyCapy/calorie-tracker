using CalorieTracker.Services;

namespace CalorieTracker.Tests.Progression;

public class ProgressionLevelCalculatorTests
{
    private readonly ProgressionLevelCalculator _calculator = new();

    public static TheoryData<int, int> ExactThresholds => new()
    {
        { 0, 1 },
        { 50, 2 },
        { 125, 3 },
        { 225, 4 },
        { 350, 5 },
        { 500, 6 },
        { 700, 7 },
        { 950, 8 },
        { 1250, 9 },
        { 1600, 10 }
    };

    public static TheoryData<int, int, int, int, decimal> BetweenThresholds =>
        new()
        {
            { 25, 1, 25, 25, 50m },
            { 100, 2, 50, 25, 66.666666666666666666666666667m },
            { 175, 3, 50, 50, 50m },
            { 300, 4, 75, 50, 60m },
            { 425, 5, 75, 75, 50m },
            { 600, 6, 100, 100, 50m },
            { 825, 7, 125, 125, 50m },
            { 1100, 8, 150, 150, 50m },
            { 1425, 9, 175, 175, 50m }
        };

    [Fact]
    public void Thresholds_ExposeApprovedExtensibleCurve()
    {
        ProgressionLevelThreshold[] expected =
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
        ];

        Assert.Equal(expected, ProgressionLevelCalculator.Thresholds);
    }

    [Theory]
    [MemberData(nameof(ExactThresholds))]
    public void Calculate_AtEveryThreshold_StartsExpectedLevel(
        int totalXp,
        int expectedLevel)
    {
        var result = _calculator.Calculate(totalXp);

        Assert.Equal(expectedLevel, result.CurrentLevel);
        Assert.Equal(totalXp, result.TotalXp);
        Assert.Equal(totalXp, result.CurrentLevelThreshold);
        Assert.Equal(0, result.XpEarnedWithinLevel);

        if (expectedLevel < 10)
        {
            Assert.False(result.IsFinalLevel);
            Assert.Equal(0m, result.ProgressPercentage);
            Assert.Equal(
                result.XpSpanToNextLevel,
                result.XpRemainingToNextLevel);
        }
        else
        {
            AssertFinalLevel(result);
        }
    }

    [Theory]
    [MemberData(nameof(BetweenThresholds))]
    public void Calculate_BetweenThresholds_ReturnsProgressWithinLevel(
        int totalXp,
        int expectedLevel,
        int expectedEarned,
        int expectedRemaining,
        decimal expectedPercentage)
    {
        var result = _calculator.Calculate(totalXp);

        Assert.Equal(expectedLevel, result.CurrentLevel);
        Assert.Equal(expectedEarned, result.XpEarnedWithinLevel);
        Assert.Equal(expectedRemaining, result.XpRemainingToNextLevel);
        Assert.Equal(expectedPercentage, result.ProgressPercentage);
        Assert.False(result.IsFinalLevel);
    }

    [Fact]
    public void Calculate_AboveFinalThreshold_RemainsAtFinalLevel()
    {
        var result = _calculator.Calculate(2500);

        Assert.Equal(10, result.CurrentLevel);
        Assert.Equal(2500, result.TotalXp);
        Assert.Equal(1600, result.CurrentLevelThreshold);
        Assert.Equal(900, result.XpEarnedWithinLevel);
        AssertFinalLevel(result);
    }

    [Fact]
    public void Calculate_WithNegativeXp_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calculator.Calculate(-1));
    }

    private static void AssertFinalLevel(ProgressionLevelResult result)
    {
        Assert.True(result.IsFinalLevel);
        Assert.Null(result.NextLevelThreshold);
        Assert.Null(result.XpSpanToNextLevel);
        Assert.Null(result.XpRemainingToNextLevel);
        Assert.Equal(100m, result.ProgressPercentage);
    }
}
