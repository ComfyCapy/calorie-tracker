using CalorieTracker.Models;

namespace CalorieTracker.Services
{
    public interface IFoodSearchService
    {
        Task<FoodSearchPage> SearchFoodsPageAsync(
            string searchTerm,
            int pageNumber,
            int pageSize);

        Task<FoodSearchResult?> GetFoodAsync(string externalId);
    }
}
