using CalorieTracker.Services;

namespace CalorieTracker.Tests.Calculations;

public class CalorieBalanceClassifierTests
{
    [Theory]
    [InlineData(-20.01, CalorieBalanceClassification.HeavyCut)]
    [InlineData(-20, CalorieBalanceClassification.HeavyCut)]
    [InlineData(-10, CalorieBalanceClassification.LightCut)]
    [InlineData(-5, CalorieBalanceClassification.Maintenance)]
    [InlineData(0, CalorieBalanceClassification.Maintenance)]
    [InlineData(5, CalorieBalanceClassification.Maintenance)]
    [InlineData(10, CalorieBalanceClassification.LightGain)]
    [InlineData(20, CalorieBalanceClassification.HeavyGain)]
    [InlineData(20.01, CalorieBalanceClassification.HeavyGain)]
    public void Classify_UsesAuthoritativeBoundarySemantics(
        double percentage,
        CalorieBalanceClassification expected)
    {
        Assert.Equal(
            expected,
            CalorieBalanceClassifier.Classify((decimal)percentage));
    }
}
