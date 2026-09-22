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
                "Portion",
                "Approximate"
            };

        public static bool ValidateFood(
            Food food,
            ModelStateDictionary modelState,
            string prefix,
            out MeasurementDimension dimension)
        {
            var result = FoodValidator.Validate(food);
            result.ApplyTo(food);
            dimension = result.Dimension;
            foreach (var error in result.Errors)
                modelState.AddModelError($"{prefix}.{error.Field}", error.Message);
            return modelState.IsValid;
        }

        public static void ValidateDiaryDate(
            DateTime date,
            ModelStateDictionary modelState,
            string key)
        {
            if (!IsValidDiaryDate(date))
            {
                modelState.AddModelError(
                    key,
                    "Date must be between 1 January 1900 and 31 December 2100.");
            }
        }

        public static bool IsValidDiaryDate(DateTime date) =>
            date.Date >= MinimumDiaryDate &&
            date.Date <= MaximumDiaryDate;

        public static bool HasBindingError(
            ModelStateDictionary modelState,
            string key)
        {
            return modelState.TryGetValue(key, out var entry) &&
                entry.Errors.Count > 0;
        }

    }
}
