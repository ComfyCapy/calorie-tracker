using CalorieTracker.Models;
using System.Net.Http.Json;

namespace CalorieTracker.Services
{
    public class UsdaFoodService : IFoodSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public UsdaFoodService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<List<FoodSearchResult>> SearchFoodsAsync(
            string searchTerm)
        {
            var page = await SearchFoodsPageAsync(
                searchTerm,
                1,
                20);

            return page.Foods;
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
                .Select(food => new FoodSearchResult
                {
                    ExternalId = food.FdcId.ToString(),
                    Source = "USDA",
                    Name = food.Description,

                    Calories = GetEnergy(food),
                    Protein = GetNutrient(food, 1003),
                    Carbohydrates = GetNutrient(food, 1005),
                    Fat = GetNutrient(food, 1004),

                    ServingSize = 100,
                    ServingUnit = "g"
                })
                .Where(food =>
                    food.Calories > 0 ||
                    food.Protein > 0 ||
                    food.Carbohydrates > 0 ||
                    food.Fat > 0)
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
            if (string.IsNullOrWhiteSpace(externalId))
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
                $"https://api.nal.usda.gov/fdc/v1/food/{externalId}?api_key={apiKey}";

            var food =
                await _httpClient.GetFromJsonAsync<UsdaFoodDetails>(url);

            if (food == null)
            {
                return null;
            }

            return new FoodSearchResult
            {
                ExternalId = food.FdcId.ToString(),
                Source = "USDA",
                Name = food.Description,

                Calories = GetDetailEnergy(food),
                Protein = GetDetailNutrient(food, 1003),
                Carbohydrates = GetDetailNutrient(food, 1005),
                Fat = GetDetailNutrient(food, 1004),

                ServingSize = 100,
                ServingUnit = "g"
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

            public List<UsdaDetailNutrient> FoodNutrients { get; set; } = [];
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
    }
}
