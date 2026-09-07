using System.Net;
using System.Text.Json;
using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services
{
    public enum ExternalFoodFailure
    {
        None,
        InvalidId,
        Missing,
        Unavailable
    }

    public sealed record ExternalFoodResolution(
        Food? Food,
        FoodSearchResult? Result,
        ExternalFoodFailure Failure,
        bool UsedCachedFallback);

    public sealed class ExternalFoodResolver
    {
        private readonly ApplicationDbContext _context;
        private readonly IFoodSearchService _foodSearchService;

        public ExternalFoodResolver(
            ApplicationDbContext context,
            IFoodSearchService foodSearchService)
        {
            _context = context;
            _foodSearchService = foodSearchService;
        }

        public async Task<ExternalFoodResolution> ResolveAsync(
            string userId,
            string? externalId)
        {
            if (!ExternalFoodIds.TryNormalizeUsdaId(
                    externalId,
                    out var normalizedId))
            {
                return new(
                    null,
                    null,
                    ExternalFoodFailure.InvalidId,
                    false);
            }

            var cachedFood = await _context.Foods
                .Include(food => food.Portions)
                .FirstOrDefaultAsync(food =>
                    food.UserId == userId &&
                    food.Source == FoodSources.Usda &&
                    food.ExternalId == normalizedId);

            // Prefer fresh USDA data, but keep this user's cached copy usable during upstream failures.
            try
            {
                var result = await _foodSearchService
                    .GetFoodAsync(normalizedId);

                if (result == null)
                {
                    return cachedFood != null
                        ? Cached(cachedFood)
                        : new(
                            null,
                            null,
                            ExternalFoodFailure.Missing,
                            false);
                }

                if (string.IsNullOrWhiteSpace(result.Name) ||
                    result.Calories < 0 ||
                    result.Calories > int.MaxValue ||
                    result.Protein < 0 ||
                    result.Carbohydrates < 0 ||
                    result.Fat < 0)
                {
                    return cachedFood != null
                        ? Cached(cachedFood)
                        : new(
                            null,
                            null,
                            ExternalFoodFailure.Unavailable,
                            false);
                }

                if (cachedFood != null &&
                    MeasurementUnits.TryNormalize(
                        cachedFood.ServingUnit,
                        out _,
                    out var cachedDimension) &&
                    cachedDimension != MeasurementDimension.Mass)
                {
                    // Do not refresh a historical volume food to USDA's gram basis and reinterpret its history.
                    var hasPortions = cachedFood.Portions.Count > 0;

                    var hasDiaryHistory = await _context.DiaryEntries
                        .AnyAsync(entry =>
                            entry.FoodId == cachedFood.Id);

                    if (hasPortions || hasDiaryHistory)
                    {
                        return Cached(cachedFood);
                    }
                }

                var food = cachedFood ?? new Food
                {
                    UserId = userId,
                    Source = FoodSources.Usda,
                    ExternalId = normalizedId
                };

                food.Name = result.Name.Trim();
                food.Calories = (int)Math.Round(result.Calories);
                food.Protein = result.Protein;
                food.Carbohydrates = result.Carbohydrates;
                food.Fat = result.Fat;
                food.ServingSize = 100;
                food.ServingUnit = "g";
                food.CanonicalServingSize = 100;
                food.IsDeleted = false;

                AddPortionsWhenNoneExist(food, result.Portions);

                if (cachedFood == null)
                {
                    _context.Foods.Add(food);
                }

                return new(
                    food,
                    ToResult(food),
                    ExternalFoodFailure.None,
                    false);
            }
            catch (HttpRequestException exception)
                when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return cachedFood != null
                    ? Cached(cachedFood)
                    : new(
                        null,
                        null,
                        ExternalFoodFailure.Missing,
                        false);
            }
            catch (Exception exception)
                when (exception is HttpRequestException or
                    TaskCanceledException or
                    JsonException or
                    NotSupportedException or
                    InvalidOperationException)
            {
                return cachedFood != null
                    ? Cached(cachedFood)
                    : new(
                        null,
                        null,
                        ExternalFoodFailure.Unavailable,
                        false);
            }
        }

        private static ExternalFoodResolution Cached(Food food)
        {
            // A cached fallback is usable again even if the row was previously soft-deleted.
            food.IsDeleted = false;

            return new(
                food,
                ToResult(food),
                ExternalFoodFailure.None,
                true);
        }

        private static FoodSearchResult ToResult(Food food) => new()
        {
            ExternalId = food.ExternalId ?? string.Empty,
            Source = food.Source ?? FoodSources.Usda,
            IsFavourite = food.IsFavourite,
            Name = food.Name,
            Calories = food.Calories,
            Protein = food.Protein,
            Carbohydrates = food.Carbohydrates,
            Fat = food.Fat,
            ServingSize = food.ServingSize,
            ServingUnit = food.ServingUnit
        };

        private static void AddPortionsWhenNoneExist(
            Food food,
            IEnumerable<FoodPortionCandidate> candidates)
        {
            if (food.Portions.Count > 0)
            {
                return;
            }

            var seen = new HashSet<(string Name, decimal GramWeight)>();

            foreach (var candidate in candidates)
            {
                var name = string.Join(
                    " ",
                    candidate.Name.Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries));
                var key = (name.ToUpperInvariant(), candidate.GramWeight);

                if (candidate.GramWeight <= 0 ||
                    name.Length == 0 ||
                    name.Length > FoodPortion.MaxNameLength ||
                    name.Any(char.IsControl) ||
                    !seen.Add(key))
                {
                    continue;
                }

                food.Portions.Add(new FoodPortion
                {
                    Name = name,
                    Amount = candidate.GramWeight
                });
            }
        }
    }
}
