using CalorieTracker.Models;
using CalorieTracker.Tests.TestSupport;

namespace CalorieTracker.Tests.Diary;

public class DiaryEntrySnapshotTests
{
    [Fact]
    public void CaptureSnapshot_CopiesHistoricalFoodAndPortionValues()
    {
        var food = TestData.Food(
            "user-1",
            "oz",
            1,
            28.349523125m,
            "Freedom Cheese");
        food.Calories = 120;
        food.Protein = 7;
        food.Carbohydrates = 1;
        food.Fat = 9;
        var portion = new FoodPortion { Name = "slice", Amount = 14.1747615625m };
        var entry = new DiaryEntry();

        entry.CaptureSnapshot(food, portion);

        Assert.Equal("Freedom Cheese", entry.FoodNameSnapshot);
        Assert.Equal(1, entry.ServingSizeSnapshot);
        Assert.Equal("oz", entry.ServingUnitSnapshot);
        Assert.Equal(28.349523125m, entry.CanonicalServingSizeSnapshot);
        Assert.Equal(120, entry.CaloriesSnapshot);
        Assert.Equal(7, entry.ProteinSnapshot);
        Assert.Equal(1, entry.CarbohydratesSnapshot);
        Assert.Equal(9, entry.FatSnapshot);
        Assert.Equal("slice", entry.PortionNameSnapshot);
    }

    [Fact]
    public void Nutrition_UsesCanonicalSnapshotAndLoggedQuantity()
    {
        var entry = new DiaryEntry
        {
            Quantity = 56.69904625m,
            CanonicalServingSizeSnapshot = 28.349523125m,
            CaloriesSnapshot = 120,
            ProteinSnapshot = 7,
            CarbohydratesSnapshot = 1,
            FatSnapshot = 9
        };

        Assert.Equal(240, entry.CaloriesConsumed);
        Assert.Equal(14, entry.ProteinConsumed);
        Assert.Equal(2, entry.CarbohydratesConsumed);
        Assert.Equal(18, entry.FatConsumed);
    }

    [Fact]
    public void Nutrition_DoesNotChangeWhenFoodIsEditedLater()
    {
        var food = TestData.Food("user-1");
        var entry = TestData.DiaryEntry("user-1", food, 50);
        var originalCalories = entry.CaloriesConsumed;

        food.Name = "Changed food";
        food.Calories = 999;
        food.Protein = 999;
        food.CanonicalServingSize = 1;

        Assert.Equal(originalCalories, entry.CaloriesConsumed);
        Assert.Equal("Test food", entry.FoodNameSnapshot);
        Assert.Equal(5, entry.ProteinConsumed);
    }

    [Fact]
    public void PortionHistory_DoesNotChangeWhenPortionIsEditedOrDeletedLater()
    {
        var food = TestData.Food("user-1", name: "Cinnamon roll");
        var portion = new FoodPortion
        {
            Name = "1 roll",
            Amount = 75
        };
        var entry = TestData.DiaryEntry(
            "user-1",
            food,
            150,
            portion,
            2);
        var originalCalories = entry.CaloriesConsumed;

        portion.Name = "renamed serving";
        portion.Amount = 100;
        portion.IsDeleted = true;

        Assert.Equal(150, entry.Quantity);
        Assert.Equal(2, entry.PortionQuantity);
        Assert.Equal("1 roll", entry.PortionNameSnapshot);
        Assert.Equal(originalCalories, entry.CaloriesConsumed);
    }

    [Fact]
    public void Nutrition_WithInvalidHistoricalDenominator_ReturnsZero()
    {
        var entry = new DiaryEntry
        {
            Quantity = 100,
            CanonicalServingSizeSnapshot = 0,
            CaloriesSnapshot = 200,
            ProteinSnapshot = 10,
            CarbohydratesSnapshot = 20,
            FatSnapshot = 5
        };

        Assert.Equal(0, entry.CaloriesConsumed);
        Assert.Equal(0, entry.ProteinConsumed);
        Assert.Equal(0, entry.CarbohydratesConsumed);
        Assert.Equal(0, entry.FatConsumed);
    }
}
