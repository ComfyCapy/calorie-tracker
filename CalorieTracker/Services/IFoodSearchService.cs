using CalorieTracker.Models;

namespace CalorieTracker.Services
{
    public interface IFoodSearchService
    {
        Task<List<FoodSearchResult>> SearchFoodsAsync(string searchTerm);

        Task<FoodSearchResult?> GetFoodAsync(string externalId);
    }
}