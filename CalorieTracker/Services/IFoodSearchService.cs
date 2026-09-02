using CalorieTracker.Models;

namespace CalorieTracker.Services
{
    public interface IFoodSearchService
    {
        Task<List<FoodSearchResult>> SearchFoodsAsync(string searchTerm);

        Task<FoodSearchPage> SearchFoodsPageAsync(
            string searchTerm,
            int pageNumber,
            int pageSize);

        Task<FoodSearchResult?> GetFoodAsync(string externalId);
    }
}
