using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CalorieTracker.Services;

namespace CalorieTracker.Pages.Foods
{
    [Authorize]
    public class SearchModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public bool ReturnToDiary { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DiaryDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DiaryMeal { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }
        public IActionResult OnGet()
        {
            if (!ModelState.IsValid ||
                (DiaryDate.HasValue &&
                 (DiaryDate.Value.Date < ValidationRules.MinimumDiaryDate ||
                  DiaryDate.Value.Date > ValidationRules.MaximumDiaryDate)) ||
                (!string.IsNullOrEmpty(DiaryMeal) &&
                 !ValidationRules.MealTypes.Contains(DiaryMeal)))
            {
                return BadRequest();
            }

            return Page();
        }
    }
}
