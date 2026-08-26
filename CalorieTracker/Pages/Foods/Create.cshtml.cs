using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace CalorieTracker.Pages.Foods
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public Food Food { get; set; } = new Food();

        [BindProperty]
        public bool AddToFavourites { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool ReturnToDiary { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DiaryDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DiaryMeal { get; set; }

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            Food.UserId = userId;
            Food.IsFavourite = AddToFavourites;

            _context.Foods.Add(Food);
            await _context.SaveChangesAsync();

            if (ReturnToDiary)
            {
                return RedirectToPage(
                    "/Diary/Create",
                    new
                    {
                        date = DiaryDate?.ToString("yyyy-MM-dd"),
                        meal = DiaryMeal,
                        foodId = Food.Id
                    });
            }

            return RedirectToPage("./Index");
        }
    }
}