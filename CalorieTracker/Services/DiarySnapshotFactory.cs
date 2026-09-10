using CalorieTracker.Models;

namespace CalorieTracker.Services;

public static class DiarySnapshotFactory
{
    public static DiaryEntry CopyEntry(
        DiaryEntry source,
        string userId,
        DateTime date) =>
        CreateEntry(
            userId,
            date,
            source.MealType,
            source.FoodId,
            source.FoodPortionId,
            source.Quantity,
            source.PortionQuantity,
            source.IsApproximate,
            source.ApproximationLabel,
            source.FoodNameSnapshot,
            source.ServingSizeSnapshot,
            source.ServingUnitSnapshot,
            source.CanonicalServingSizeSnapshot,
            source.ServingBasisSnapshot,
            source.PortionLabelSnapshot,
            source.CaloriesSnapshot,
            source.ProteinSnapshot,
            source.CarbohydratesSnapshot,
            source.FatSnapshot,
            source.PortionNameSnapshot);

    public static SavedMealItem ToSavedMealItem(DiaryEntry source) => new()
    {
        FoodId = source.FoodId,
        FoodPortionId = source.FoodPortionId,
        Quantity = source.Quantity,
        PortionQuantity = source.PortionQuantity,
        IsApproximate = source.IsApproximate,
        ApproximationLabel = source.ApproximationLabel,
        FoodNameSnapshot = source.FoodNameSnapshot,
        ServingSizeSnapshot = source.ServingSizeSnapshot,
        ServingUnitSnapshot = source.ServingUnitSnapshot,
        CanonicalServingSizeSnapshot = source.CanonicalServingSizeSnapshot,
        ServingBasisSnapshot = source.ServingBasisSnapshot,
        PortionLabelSnapshot = source.PortionLabelSnapshot,
        CaloriesSnapshot = source.CaloriesSnapshot,
        ProteinSnapshot = source.ProteinSnapshot,
        CarbohydratesSnapshot = source.CarbohydratesSnapshot,
        FatSnapshot = source.FatSnapshot,
        PortionNameSnapshot = source.PortionNameSnapshot
    };

    public static DiaryEntry FromSavedMealItem(
        SavedMealItem source,
        string userId,
        DateTime date,
        string mealType) =>
        CreateEntry(
            userId,
            date,
            mealType,
            source.FoodId,
            source.FoodPortionId,
            source.Quantity,
            source.PortionQuantity,
            source.IsApproximate,
            source.ApproximationLabel,
            source.FoodNameSnapshot,
            source.ServingSizeSnapshot,
            source.ServingUnitSnapshot,
            source.CanonicalServingSizeSnapshot,
            source.ServingBasisSnapshot,
            source.PortionLabelSnapshot,
            source.CaloriesSnapshot,
            source.ProteinSnapshot,
            source.CarbohydratesSnapshot,
            source.FatSnapshot,
            source.PortionNameSnapshot);

    private static DiaryEntry CreateEntry(
        string userId,
        DateTime date,
        string mealType,
        int foodId,
        int? foodPortionId,
        decimal quantity,
        decimal? portionQuantity,
        bool isApproximate,
        string? approximationLabel,
        string foodName,
        decimal servingSize,
        string servingUnit,
        decimal canonicalServingSize,
        FoodServingBasis servingBasis,
        string? portionLabel,
        decimal calories,
        decimal protein,
        decimal carbohydrates,
        decimal fat,
        string? portionName) => new()
        {
            UserId = userId,
            Date = date.Date,
            MealType = mealType,
            FoodId = foodId,
            FoodPortionId = foodPortionId,
            Quantity = quantity,
            PortionQuantity = portionQuantity,
            IsApproximate = isApproximate,
            ApproximationLabel = approximationLabel,
            FoodNameSnapshot = foodName,
            ServingSizeSnapshot = servingSize,
            ServingUnitSnapshot = servingUnit,
            CanonicalServingSizeSnapshot = canonicalServingSize,
            ServingBasisSnapshot = servingBasis,
            PortionLabelSnapshot = portionLabel,
            CaloriesSnapshot = calories,
            ProteinSnapshot = protein,
            CarbohydratesSnapshot = carbohydrates,
            FatSnapshot = fat,
            PortionNameSnapshot = portionName
        };
}
