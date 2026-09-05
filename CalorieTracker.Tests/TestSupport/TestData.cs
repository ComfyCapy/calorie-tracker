using CalorieTracker.Models;
using CalorieTracker.Services;

namespace CalorieTracker.Tests.TestSupport;

public static class TestData
{
    public static Food Food(
        string userId,
        string unit = "g",
        decimal servingSize = 100,
        decimal canonicalServingSize = 100,
        string name = "Test food") =>
        new()
        {
            UserId = userId,
            Name = name,
            Calories = 200,
            Protein = 10,
            Carbohydrates = 20,
            Fat = 5,
            ServingSize = servingSize,
            ServingUnit = unit,
            CanonicalServingSize = canonicalServingSize
        };

    public static DiaryEntry DiaryEntry(
        string userId,
        Food food,
        decimal quantity,
        FoodPortion? portion = null,
        decimal? portionQuantity = null)
    {
        var entry = new DiaryEntry
        {
            UserId = userId,
            Date = new DateTime(2026, 9, 5),
            MealType = "Dinner",
            Food = food,
            FoodId = food.Id,
            FoodPortion = portion,
            FoodPortionId = portion?.Id,
            PortionQuantity = portionQuantity,
            Quantity = quantity
        };

        entry.CaptureSnapshot(food, portion);
        return entry;
    }

    public static FoodSearchResult UsdaResult(
        string externalId = "123",
        string name = "USDA food") =>
        new()
        {
            ExternalId = externalId,
            Source = FoodSources.Usda,
            Name = name,
            Calories = 120,
            Protein = 7,
            Carbohydrates = 1,
            Fat = 9,
            ServingSize = 100,
            ServingUnit = "g"
        };
}
