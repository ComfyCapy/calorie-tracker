using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CalorieTracker.Tests.Foods;

public class FoodValidatorTests
{
    [Theory]
    [InlineData(" KG ", 2, "kg", 2000, MeasurementDimension.Mass)]
    [InlineData("l", 2, "L", 2000, MeasurementDimension.Volume)]
    [InlineData("g", 100, "g", 100, MeasurementDimension.Mass)]
    public void MeasuredFood_NormalizesWithoutMutatingInput(
        string unit, int amount, string normalized, int canonical, MeasurementDimension dimension)
    {
        var food = new Food { Name = " Soup ", ServingUnit = unit, ServingSize = amount, PortionLabel = "stale" };
        var result = FoodValidator.Validate(food);
        Assert.True(result.IsValid);
        Assert.Equal(" Soup ", food.Name);
        Assert.Equal(unit, food.ServingUnit);
        Assert.Equal("Soup", result.Name);
        Assert.Equal(normalized, result.ServingUnit);
        Assert.Equal(canonical, result.CanonicalServingSize);
        Assert.Equal(dimension, result.Dimension);
        Assert.Null(result.PortionLabel);
    }

    [Theory]
    [InlineData(" slice ", true, "slice")]
    [InlineData(" ", false, " ")]
    [InlineData("a\nb", false, "a\nb")]
    public void PortionFood_UsesCountAndPreservesInvalidLabel(string label, bool valid, string expected)
    {
        var food = new Food { Name = "Toast", ServingBasis = FoodServingBasis.Portion,
            ServingSize = 2, ServingUnit = "unused", PortionLabel = label };
        var result = FoodValidator.Validate(food);
        Assert.Equal(valid, result.IsValid);
        Assert.Equal(expected, result.PortionLabel);
        Assert.Equal(2, result.CanonicalServingSize);
        Assert.Equal("unused", result.ServingUnit);
    }

    [Fact]
    public void InvalidFields_ReportAllErrorsAndStillNormalizeValidFields()
    {
        var result = FoodValidator.Validate(new Food { Name = " food ", Calories = -1,
            Protein = -1, Carbohydrates = -1, Fat = -1, ServingBasis = (FoodServingBasis)99,
            ServingUnit = "KG", ServingSize = 1 });
        Assert.Equal(new[] { "Calories", "Protein", "Carbohydrates", "Fat", "ServingBasis" },
            result.Errors.Select(error => error.Field));
        Assert.Equal("food", result.Name);
        Assert.Equal(1000, result.CanonicalServingSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveMeasuredSize_PreservesBothHistoricalErrors(int size)
    {
        var result = FoodValidator.Validate(new Food { Name = "Food", ServingSize = size });
        Assert.Equal(new[] { "Serving size must be greater than 0.", "Serving size must convert to a positive value." },
            result.Errors.Select(error => error.Message));
        Assert.Equal(100, result.CanonicalServingSize);
    }

    [Fact]
    public void OverflowAndUnsupportedUnits_DoNotReplaceCanonicalSize()
    {
        var food = new Food { Name = "Food", ServingSize = decimal.MaxValue, ServingUnit = "kg", CanonicalServingSize = 123 };
        Assert.Equal("Serving size is too large to convert.", Assert.Single(FoodValidator.Validate(food).Errors).Message);
        food.ServingUnit = "unknown";
        var result = FoodValidator.Validate(food);
        Assert.Equal("ServingUnit", Assert.Single(result.Errors).Field);
        Assert.Equal(123, result.CanonicalServingSize);
    }

    [Fact]
    public void EmptyNameAndOverlongPortion_AreRejected()
    {
        var result = FoodValidator.Validate(new Food { Name = " ", ServingBasis = FoodServingBasis.Portion,
            PortionLabel = new string('x', Food.MaxPortionLabelLength + 1) });
        Assert.Equal(new[] { "Name", "PortionLabel" }, result.Errors.Select(error => error.Field));
    }

    [Fact]
    public void MvcAdapter_PreservesExistingBindingErrorsAndAppliesPartialNormalization()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("OtherField", "Existing error");
        var food = new Food { Name = " Food ", Protein = -1 };
        Assert.False(ValidationRules.ValidateFood(food, state, "Input", out _));
        Assert.Equal("Food", food.Name);
        Assert.Equal("Protein cannot be negative.", Assert.Single(state["Input.Protein"]!.Errors).ErrorMessage);
        Assert.Equal("Existing error", Assert.Single(state["OtherField"]!.Errors).ErrorMessage);
    }
}
