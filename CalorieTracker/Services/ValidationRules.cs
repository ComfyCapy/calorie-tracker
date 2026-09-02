using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CalorieTracker.Services
{
    public static class ValidationRules
    {
        public static readonly DateTime MinimumDiaryDate =
            new(1900, 1, 1);

        public static readonly DateTime MaximumDiaryDate =
            new(2100, 12, 31);

        public static readonly HashSet<string> MealTypes =
            new(StringComparer.Ordinal)
            {
                "Breakfast",
                "Lunch",
                "Dinner",
                "Snack"
            };

        public static readonly HashSet<string> MeasurementModes =
            new(StringComparer.Ordinal)
            {
                "Exact",
                "Portion"
            };

        public static bool ValidateFood(
            Food food,
            ModelStateDictionary modelState,
            string prefix,
            out MeasurementDimension dimension)
        {
            dimension = default;

            if (string.IsNullOrWhiteSpace(food.Name))
            {
                modelState.AddModelError(
                    $"{prefix}.Name",
                    "Please enter a food name.");
            }

            if (food.Calories < 0)
            {
                modelState.AddModelError(
                    $"{prefix}.Calories",
                    "Calories cannot be negative.");
            }

            ValidateNonNegative(
                food.Protein,
                $"{prefix}.Protein",
                "Protein",
                modelState);

            ValidateNonNegative(
                food.Carbohydrates,
                $"{prefix}.Carbohydrates",
                "Carbohydrates",
                modelState);

            ValidateNonNegative(
                food.Fat,
                $"{prefix}.Fat",
                "Fat",
                modelState);

            if (food.ServingSize <= 0)
            {
                modelState.AddModelError(
                    $"{prefix}.ServingSize",
                    "Serving size must be greater than 0.");
            }

            if (!MeasurementUnits.TryNormalize(
                    food.ServingUnit,
                    out var normalizedUnit,
                    out dimension))
            {
                modelState.AddModelError(
                    $"{prefix}.ServingUnit",
                    "Select a supported serving unit.");
            }
            else if (!MeasurementUnits.TryToCanonical(
                         food.ServingSize,
                         normalizedUnit,
                         out var canonicalServingSize,
                         out _,
                         out _))
            {
                modelState.AddModelError(
                    $"{prefix}.ServingSize",
                    "Serving size is too large to convert.");
            }
            else if (canonicalServingSize <= 0)
            {
                modelState.AddModelError(
                    $"{prefix}.ServingSize",
                    "Serving size must convert to a positive value.");
            }
            else
            {
                food.ServingUnit = normalizedUnit;
                food.CanonicalServingSize = canonicalServingSize;
            }

            if (!string.IsNullOrWhiteSpace(food.Name))
            {
                food.Name = food.Name.Trim();
            }

            return modelState.IsValid;
        }

        public static void ValidateDiaryDate(
            DateTime date,
            ModelStateDictionary modelState,
            string key)
        {
            if (date.Date < MinimumDiaryDate ||
                date.Date > MaximumDiaryDate)
            {
                modelState.AddModelError(
                    key,
                    "Date must be between 1 January 1900 and 31 December 2100.");
            }
        }

        public static bool HasBindingError(
            ModelStateDictionary modelState,
            string key)
        {
            return modelState.TryGetValue(key, out var entry) &&
                entry.Errors.Count > 0;
        }

        public static void CaptureSnapshot(
            DiaryEntry entry,
            Food food,
            FoodPortion? portion)
        {
            entry.FoodNameSnapshot = food.Name;
            entry.ServingSizeSnapshot = food.ServingSize;
            entry.ServingUnitSnapshot = food.ServingUnit;
            entry.CanonicalServingSizeSnapshot =
                food.CanonicalServingSize;
            entry.CaloriesSnapshot = food.Calories;
            entry.ProteinSnapshot = food.Protein;
            entry.CarbohydratesSnapshot = food.Carbohydrates;
            entry.FatSnapshot = food.Fat;
            entry.PortionNameSnapshot = portion?.Name;
        }

        private static void ValidateNonNegative(
            decimal value,
            string key,
            string label,
            ModelStateDictionary modelState)
        {
            if (value < 0)
            {
                modelState.AddModelError(
                    key,
                    $"{label} cannot be negative.");
            }
        }
    }
}
