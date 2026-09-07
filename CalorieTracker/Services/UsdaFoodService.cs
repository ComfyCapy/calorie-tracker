using CalorieTracker.Models;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;

namespace CalorieTracker.Services
{
    public class UsdaFoodService : IFoodSearchService
    {
        private const string FoundationDataType = "Foundation";
        private const string FnddsDataType = "Survey (FNDDS)";

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public UsdaFoodService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<FoodSearchPage> SearchFoodsPageAsync(
            string searchTerm,
            int pageNumber,
            int pageSize)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new FoodSearchPage();
            }

            var apiKey = _configuration["FoodDataCentral:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "FoodData Central API key is missing.");
            }

            var request = new UsdaSearchRequest
            {
                Query = searchTerm,
                PageNumber = pageNumber,
                PageSize = pageSize,
                DataType =
                [
                    "Foundation",
                    "Survey (FNDDS)",
                    "SR Legacy"
                ]
            };

            var url =
                $"https://api.nal.usda.gov/fdc/v1/foods/search?api_key={apiKey}";

            var response = await _httpClient.PostAsJsonAsync(
                url,
                request);

            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadFromJsonAsync<UsdaSearchResponse>();

            if (result == null)
            {
                return new FoodSearchPage
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }

            var foods = result.Foods
                .Where(food =>
                    food.FdcId > 0 &&
                    !string.IsNullOrWhiteSpace(food.Description))
                .Select(food => new FoodSearchResult
                {
                    ExternalId = food.FdcId.ToString(),
                    Source = FoodSources.Usda,
                    Name = food.Description,

                    Calories = GetEnergy(food),
                    Protein = GetNutrient(food, 1003),
                    Carbohydrates = GetNutrient(food, 1005),
                    Fat = GetNutrient(food, 1004),

                    ServingSize = 100,
                    ServingUnit = "g"
                })
                .ToList();

            return new FoodSearchPage
            {
                Foods = foods,
                PageNumber = result.CurrentPage > 0
                    ? result.CurrentPage
                    : pageNumber,
                PageSize = pageSize,
                TotalResults = result.TotalHits,
                TotalPages = result.TotalPages
            };
        }

        public async Task<FoodSearchResult?> GetFoodAsync(string externalId)
        {
            if (!ExternalFoodIds.TryNormalizeUsdaId(
                    externalId,
                    out var normalizedId))
            {
                return null;
            }

            var apiKey = _configuration["FoodDataCentral:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "FoodData Central API key is missing.");
            }

            var url =
                $"https://api.nal.usda.gov/fdc/v1/food/{normalizedId}?api_key={apiKey}";

            var food =
                await _httpClient.GetFromJsonAsync<UsdaFoodDetails>(url);

            if (food == null)
            {
                return null;
            }

            return new FoodSearchResult
            {
                ExternalId = food.FdcId.ToString(),
                Source = FoodSources.Usda,
                Name = food.Description,

                Calories = GetDetailEnergy(food),
                Protein = GetDetailNutrient(food, 1003),
                Carbohydrates = GetDetailNutrient(food, 1005),
                Fat = GetDetailNutrient(food, 1004),

                ServingSize = 100,
                ServingUnit = "g",
                Portions = GetPortions(food)
            };
        }

        private static decimal GetEnergy(UsdaFood food)
        {
            var energy = GetNutrient(food, 1008);

            if (energy > 0)
            {
                return energy;
            }

            energy = GetNutrient(food, 2047);

            if (energy > 0)
            {
                return energy;
            }

            return GetNutrient(food, 2048);
        }

        private static decimal GetNutrient(
            UsdaFood food,
            int nutrientId)
        {
            var nutrient = food.FoodNutrients
                .FirstOrDefault(nutrient =>
                    nutrient.NutrientId == nutrientId);

            return Math.Max(nutrient?.Value ?? 0, 0);
        }

        private class UsdaSearchRequest
        {
            public string Query { get; set; } = string.Empty;

            public int PageSize { get; set; }

            public int PageNumber { get; set; }

            public List<string> DataType { get; set; } = [];
        }

        private class UsdaSearchResponse
        {
            public List<UsdaFood> Foods { get; set; } = [];

            public int CurrentPage { get; set; }

            public int TotalHits { get; set; }

            public int TotalPages { get; set; }
        }

        private class UsdaFood
        {
            public int FdcId { get; set; }

            public string Description { get; set; } = string.Empty;

            public List<UsdaNutrient> FoodNutrients { get; set; } = [];
        }

        private class UsdaNutrient
        {
            public int NutrientId { get; set; }

            public decimal Value { get; set; }
        }
        private class UsdaFoodDetails
        {
            public int FdcId { get; set; }

            public string Description { get; set; } = string.Empty;

            public string DataType { get; set; } = string.Empty;

            public List<UsdaDetailNutrient> FoodNutrients { get; set; } = [];

            public List<UsdaFoodPortion?>? FoodPortions { get; set; } = [];
        }

        private class UsdaFoodPortion
        {
            public JsonElement GramWeight { get; set; }

            public string? PortionDescription { get; set; }

            public string? Modifier { get; set; }

            public JsonElement Amount { get; set; }

            public UsdaMeasureUnit? MeasureUnit { get; set; }
        }

        private class UsdaMeasureUnit
        {
            public string? Name { get; set; }

            public string? Abbreviation { get; set; }
        }

        private class UsdaDetailNutrient
        {
            public UsdaNutrientInfo Nutrient { get; set; } = new();

            public decimal Amount { get; set; }
        }

        private class UsdaNutrientInfo
        {
            public int Id { get; set; }
        }

        private static decimal GetDetailNutrient(
            UsdaFoodDetails food,
            int nutrientId)
        {
            var nutrient = food.FoodNutrients
                .FirstOrDefault(foodNutrient =>
                    foodNutrient.Nutrient.Id == nutrientId);

            return Math.Max(nutrient?.Amount ?? 0, 0);
        }

        private static decimal GetDetailEnergy(UsdaFoodDetails food)
        {
            var energy = GetDetailNutrient(food, 1008);

            if (energy > 0)
            {
                return energy;
            }

            energy = GetDetailNutrient(food, 2047);

            if (energy > 0)
            {
                return energy;
            }

            return GetDetailNutrient(food, 2048);
        }

        private static List<FoodPortionCandidate> GetPortions(
            UsdaFoodDetails food)
        {
            var portions = new List<FoodPortionCandidate>();
            var seen = new HashSet<(string Name, decimal GramWeight)>();

            foreach (var portion in food.FoodPortions ?? [])
            {
                if (portion == null)
                {
                    continue;
                }

                if (!TryGetPositiveDecimal(
                        portion.GramWeight,
                        out var gramWeight))
                {
                    continue;
                }

                var label = GetPortionLabel(portion, food.DataType);

                if (label.Length == 0 ||
                    label.Length > FoodPortion.MaxNameLength ||
                    label.Any(char.IsControl))
                {
                    continue;
                }

                var key = (label.ToUpperInvariant(), gramWeight);

                if (!seen.Add(key))
                {
                    continue;
                }

                portions.Add(new FoodPortionCandidate(
                    label,
                    gramWeight));
            }

            return portions;
        }

        private static string GetPortionLabel(
            UsdaFoodPortion portion,
            string dataType)
        {
            var description = NormalizeWhitespace(
                portion.PortionDescription);

            if (string.Equals(
                    dataType,
                    FnddsDataType,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (description.Equals(
                        "Quantity not specified",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                if (description.Length > 0)
                {
                    return description;
                }
            }

            if (string.Equals(
                    dataType,
                    FoundationDataType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return GetFoundationPortionLabel(
                    portion,
                    description);
            }

            return GetGeneralPortionLabel(portion, description);
        }

        private static string GetFoundationPortionLabel(
            UsdaFoodPortion portion,
            string description)
        {
            var modifier = NormalizeWhitespace(portion.Modifier);
            var measureUnit = GetMeasureUnitLabel(portion);
            var hasAmount = TryGetPositiveDecimal(
                portion.Amount,
                out var amount);

            var baseLabel = measureUnit.Length > 0
                ? hasAmount
                    ? $"{FormatAmount(amount)} {measureUnit}"
                    : measureUnit
                : string.Empty;

            if (baseLabel.Length > 0)
            {
                if (modifier.Length > 0 &&
                    !baseLabel.Contains(
                        modifier,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return $"{baseLabel}, {modifier}";
                }

                return baseLabel;
            }

            if (description.Length > 0)
            {
                return description;
            }

            if (modifier.Length > 0)
            {
                if (hasAmount && !StartsWithNumber(modifier))
                {
                    return $"{FormatAmount(amount)} {modifier}";
                }

                return modifier;
            }

            return hasAmount ? FormatAmount(amount) : string.Empty;
        }

        private static string GetGeneralPortionLabel(
            UsdaFoodPortion portion,
            string description)
        {
            if (description.Length > 0)
            {
                return description;
            }

            var modifier = NormalizeWhitespace(portion.Modifier);

            if (modifier.Length > 0)
            {
                if (TryGetPositiveDecimal(
                        portion.Amount,
                        out var modifierAmount) &&
                    !StartsWithNumber(modifier))
                {
                    return $"{FormatAmount(modifierAmount)} {modifier}";
                }

                return modifier;
            }

            var measureUnit = GetMeasureUnitLabel(portion);

            if (measureUnit.Length > 0 &&
                TryGetPositiveDecimal(
                    portion.Amount,
                    out var measureAmount))
            {
                return $"{FormatAmount(measureAmount)} {measureUnit}";
            }

            return string.Empty;
        }

        private static string GetMeasureUnitLabel(
            UsdaFoodPortion portion)
        {
            var measureUnit = NormalizeWhitespace(
                portion.MeasureUnit?.Name);

            return measureUnit.Length > 0
                ? measureUnit
                : NormalizeWhitespace(
                    portion.MeasureUnit?.Abbreviation);
        }

        private static bool TryGetPositiveDecimal(
            JsonElement element,
            out decimal value)
        {
            value = 0;

            return element.ValueKind == JsonValueKind.Number &&
                element.TryGetDouble(out var doubleValue) &&
                double.IsFinite(doubleValue) &&
                doubleValue > 0 &&
                element.TryGetDecimal(out value) &&
                value > 0;
        }

        private static string NormalizeWhitespace(string? value) =>
            string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));

        private static bool StartsWithNumber(string value) =>
            char.IsDigit(value[0]) ||
            value[0] is '+' or '-' or '.';

        private static string FormatAmount(decimal value) =>
            value.ToString("G29", CultureInfo.InvariantCulture);
    }
}
