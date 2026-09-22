using CalorieTracker.Models;

namespace CalorieTracker.Services;

public sealed record FoodValidationError(string Field, string Message);

public sealed record FoodValidationResult(
    string Name,
    string ServingUnit,
    string? PortionLabel,
    decimal CanonicalServingSize,
    MeasurementDimension Dimension,
    IReadOnlyList<FoodValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;

    // Invalid forms historically receive partial normalization too.
    public void ApplyTo(Food food)
    {
        food.Name = Name;
        food.ServingUnit = ServingUnit;
        food.PortionLabel = PortionLabel;
        food.CanonicalServingSize = CanonicalServingSize;
    }
}

public static class FoodValidator
{
    public static FoodValidationResult Validate(Food input)
    {
        var errors = new List<FoodValidationError>();
        var food = new Food
        {
            Name = input.Name, Calories = input.Calories,
            Protein = input.Protein, Carbohydrates = input.Carbohydrates,
            Fat = input.Fat, ServingBasis = input.ServingBasis,
            ServingSize = input.ServingSize, ServingUnit = input.ServingUnit,
            PortionLabel = input.PortionLabel,
            CanonicalServingSize = input.CanonicalServingSize
        };

        void AddError(string field, string message) =>
            errors.Add(new FoodValidationError(field, message));

        void ValidateNonNegative(decimal value, string field)
        {
            if (value < 0) AddError(field, $"{field} cannot be negative.");
        }

        var dimension = default(MeasurementDimension);

        if (string.IsNullOrWhiteSpace(food.Name))
        {
            AddError(
                "Name",
                "Please enter a food name.");
        }

        if (food.Calories < 0)
        {
            AddError(
                "Calories",
                "Calories cannot be negative.");
        }

        ValidateNonNegative(food.Protein, "Protein");

        ValidateNonNegative(food.Carbohydrates, "Carbohydrates");

        ValidateNonNegative(food.Fat, "Fat");

        if (!Enum.IsDefined(food.ServingBasis))
        {
            AddError(
                "ServingBasis",
                "Select a supported serving basis.");
        }

        if (food.ServingSize <= 0)
        {
            AddError(
                "ServingSize",
                "Serving size must be greater than 0.");
        }

        if (food.ServingBasis == FoodServingBasis.Portion)
        {
            var portionLabel = food.PortionLabel?.Trim() ?? string.Empty;

            if (portionLabel.Length == 0)
            {
                AddError(
                    "PortionLabel",
                    "Please enter a portion name.");
            }
            else if (portionLabel.Length > Food.MaxPortionLabelLength)
            {
                AddError(
                    "PortionLabel",
                    $"Portion name must be {Food.MaxPortionLabelLength} characters or fewer.");
            }
            else if (portionLabel.Any(char.IsControl))
            {
                AddError(
                    "PortionLabel",
                    "Portion name cannot contain line breaks or control characters.");
            }
            else
            {
                food.PortionLabel = portionLabel;
            }

            if (food.ServingSize > 0)
            {
                food.CanonicalServingSize = food.ServingSize;
            }
        }
        else if (!MeasurementUnits.TryNormalize(
                     food.ServingUnit,
                     out var normalizedUnit,
                     out dimension))
        {
            AddError(
                "ServingUnit",
                "Select a supported serving unit.");
        }
        else if (!MeasurementUnits.TryToCanonical(
                     food.ServingSize,
                     normalizedUnit,
                     out var canonicalServingSize,
                     out _,
                     out _))
        {
            AddError(
                "ServingSize",
                "Serving size is too large to convert.");
        }
        else if (canonicalServingSize <= 0)
        {
            AddError(
                "ServingSize",
                "Serving size must convert to a positive value.");
        }
        else
        {
            food.ServingUnit = normalizedUnit;
            food.CanonicalServingSize = canonicalServingSize;
            food.PortionLabel = null;
        }

        if (!string.IsNullOrWhiteSpace(food.Name))
        {
            food.Name = food.Name.Trim();
        }

        return new FoodValidationResult(food.Name, food.ServingUnit,
            food.PortionLabel, food.CanonicalServingSize, dimension, errors);
    }
}
