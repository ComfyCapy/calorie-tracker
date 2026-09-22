using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;

namespace CalorieTracker.Tests.Diary;

public sealed class DiaryMeasurementResolverTests
{
    [Theory]
    [InlineData("g", "2", "2")]
    [InlineData("kg", "0.125", "125")]
    [InlineData("L", "0.5", "500")]
    [InlineData("oz", "2", "56.69904625")]
    [InlineData("g", "0.0000000000000000000000000001", "0.0000000000000000000000000001")]
    [InlineData("g", "79228162514264337593543950335", "79228162514264337593543950335")]
    public void Exact_PreservesPrecisionAndDecimalBoundaries(string unit, string input, string expected)
    {
        var food = TestData.Food("owner", unit);
        var result = DiaryMeasurementResolver.Resolve(
            new("Exact", Parse(input), null, null, null, "Medium"), food, []);
        Assert.Empty(result.Errors);
        Assert.Equal(Parse(expected), result.Quantity);
        Assert.Equal(unit, food.ServingUnit);
    }

    [Theory]
    [InlineData("Portion", 0, 0)]
    [InlineData("Portion", -1, -2)]
    [InlineData("Approximate", 0, 123)]
    [InlineData("Approximate", -1, 123)]
    public void LegacyNonpositiveServingAmounts_RetainExistingModeSpecificBehavior(string mode, int amount, int expected)
    {
        var food = TestData.Food("owner");
        var portion = new FoodPortion { Id = 1, Food = food, Amount = amount };
        var result = DiaryMeasurementResolver.Resolve(new(mode, 123, 1, 2, 1, "Large"), food, [portion]);
        Assert.Empty(result.Errors);
        Assert.Equal(expected, result.Quantity);
        Assert.Equal(amount, portion.Amount);
    }

    [Theory]
    [InlineData("Portion", 99, 1, "Medium")]
    [InlineData("Portion", 1, 0, "Medium")]
    [InlineData("Approximate", 99, 1, "Medium")]
    [InlineData("Approximate", 1, 1, "invalid")]
    [InlineData("Exact", 1, 1, "Medium")]
    public void HistoricalAmountPolicies_AreNotEvaluatedForUnresolvedSelections(
        string mode, int portionId, int count, string size)
    {
        var food = TestData.Food("owner");
        var portion = new FoodPortion { Id = 1, Food = food, Amount = 40 };
        DiaryMeasurementResolver.Resolve(new(mode, 10, portionId, count, portionId, size), food, [portion],
            _ => throw new InvalidOperationException("Unexpected portion policy"),
            _ => throw new InvalidOperationException("Unexpected estimate policy"));
    }

    [Theory]
    [InlineData("Portion", "PortionQuantity", "The resulting quantity is too large.")]
    [InlineData("Approximate", "ApproximationSize", "The estimated quantity is too large.")]
    public void Overflow_ReturnsFieldErrorWithoutLosingSelectionOrChangingInput(
        string mode, string field, string message)
    {
        var food = TestData.Food("owner");
        var portion = new FoodPortion { Id = 1, Food = food, Amount = decimal.MaxValue };
        var input = new DiaryMeasurementInput(mode, 123, 1, 2, 1, "Large");
        var result = DiaryMeasurementResolver.Resolve(input, food, [portion]);
        Assert.Equal(new DiaryMeasurementError(field, message), Assert.Single(result.Errors));
        Assert.Same(portion, result.Portion);
        Assert.Equal(123, result.Quantity);
        Assert.Equal(123, input.Quantity);
        Assert.Contains("DiaryEntry.Quantity", result.DerivedFields);
    }

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
