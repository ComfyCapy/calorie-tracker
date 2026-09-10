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
        private readonly FoodCatalogue _foodCatalogue;

        public ExternalFoodResolver(
            ApplicationDbContext context,
            FoodCatalogue foodCatalogue)
        {
            _context = context;
            _foodCatalogue = foodCatalogue;
        }

        public Task<ExternalFoodResolution> ResolveAsync(
            string userId,
            string? externalId) =>
            ResolveAsync(
                userId,
                FoodCatalogueProviders.LegacyDefault,
                externalId);

        public async Task<ExternalFoodResolution> ResolveAsync(
            string userId,
            string? providerId,
            string? externalId)
        {
            if (!_foodCatalogue.TryGetProvider(providerId, out var provider) ||
                !provider.TryNormalizeExternalId(
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
                    food.Source == provider.Source &&
                    food.ExternalId == normalizedId);

            // Prefer authoritative provider data, but keep only this user's cached copy usable on failure.
            try
            {
                var result = await provider.ResolveAsync(normalizedId);

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
                    result.Fat < 0 ||
                    result.ServingSize <= 0 ||
                    !MeasurementUnits.TryToCanonical(
                        result.ServingSize,
                        result.ServingUnit,
                        out var canonicalServingSize,
                        out var servingUnit,
                        out var resultDimension))
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
                    cachedDimension != resultDimension)
                {
                    // Do not reinterpret history across mass and volume dimensions.
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
                    Source = provider.Source,
                    ExternalId = normalizedId
                };

                food.Name = result.Name.Trim();
                food.Calories = (int)Math.Round(
                    result.Calories,
                    MidpointRounding.ToEven);
                food.Protein = result.Protein;
                food.Carbohydrates = result.Carbohydrates;
                food.Fat = result.Fat;
                food.ServingSize = result.ServingSize;
                food.ServingUnit = servingUnit;
                food.CanonicalServingSize = canonicalServingSize;
                food.IsDeleted = false;

                if (resultDimension == MeasurementDimension.Mass)
                {
                    AddMissingMeasuredPortions(food, result.Portions);
                }

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

        private static FoodSearchResult ToResult(Food food)
        {
            var provider = FoodCatalogueProviders.ForSource(food.Source);

            return new()
            {
                ExternalId = food.ExternalId ?? string.Empty,
                Provider = provider,
                Source = food.Source!,
                IsFavourite = food.IsFavourite,
                Name = food.Name,
                Calories = food.Calories,
                Protein = food.Protein,
                Carbohydrates = food.Carbohydrates,
                Fat = food.Fat,
                ServingSize = food.ServingSize,
                ServingUnit = food.ServingUnit
            };
        }

        private static void AddMissingMeasuredPortions(
            Food food,
            IEnumerable<FoodPortionCandidate> candidates)
        {
            // Portion provenance is not stored, so never update or resurrect an
            // existing row. Matching deleted rows also count as existing: this
            // respects a user's removal while allowing other provider portions
            // to be added alongside manual portions.
            var existingNames = food.Portions
                .Select(portion => NormalizePortionName(portion.Name))
                .Where(name => name.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates)
            {
                var name = NormalizePortionName(candidate.Name);

                if (candidate.GramWeight <= 0 ||
                    name.Length == 0 ||
                    name.Length > FoodPortion.MaxNameLength ||
                    name.Any(char.IsControl) ||
                    !existingNames.Add(name))
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

        private static string NormalizePortionName(string? name) =>
            string.Join(
                " ",
                (name ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));
    }
}
