using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
        public void OnGet()
        {
        }
    }
}