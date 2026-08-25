using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalorieTracker.Pages.Foods
{
    [Authorize]
    public class ApiSearchModel : PageModel
    {
        private readonly IFoodSearchService _foodSearchService;

        public ApiSearchModel(IFoodSearchService foodSearchService)
        {
            _foodSearchService = foodSearchService;
        }

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty;

        public List<FoodSearchResult> Results { get; set; } = [];

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                Results =
                    await _foodSearchService.SearchFoodsAsync(SearchTerm);
            }
        }
    }
}