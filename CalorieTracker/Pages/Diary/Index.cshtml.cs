using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CalorieTracker.Services;

namespace CalorieTracker.Pages.Diary
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MacroTargetCalculator _macroTargetCalculator;
        private readonly IUserLocalTimeProvider _userLocalTimeProvider;
        private readonly DiaryCopyService _diaryCopyService;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            MacroTargetCalculator macroTargetCalculator,
            IUserLocalTimeProvider userLocalTimeProvider,
            DiaryCopyService diaryCopyService)
        {
            _context = context;
            _userManager = userManager;
            _macroTargetCalculator = macroTargetCalculator;
            _userLocalTimeProvider = userLocalTimeProvider;
            _diaryCopyService = diaryCopyService;
        }

        public List<DiaryEntry> Entries { get; set; } = [];

        public decimal TotalCalories { get; set; }
        public decimal TotalProtein { get; set; }
        public decimal TotalCarbohydrates { get; set; }
        public decimal TotalFat { get; set; }

        public decimal DailyCalorieTarget { get; set; }
        public decimal CaloriesRemaining { get; set; }
        public decimal CalorieProgressPercent { get; set; }
        public MacroGoalProgress? MacroGoalProgress { get; set; }

        public DateTime SelectedDate { get; set; }
        public DateOnly CurrentDate { get; private set; }

        public DateTime? PreviousDate =>
            SelectedDate > ValidationRules.MinimumDiaryDate
                ? SelectedDate.AddDays(-1)
                : null;

        public DateTime? NextDate =>
            SelectedDate < ValidationRules.MaximumDiaryDate
                ? SelectedDate.AddDays(1)
                : null;

        public async Task<IActionResult> OnGetAsync(DateTime? date)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);
            CurrentDate = _userLocalTimeProvider.Today;
            SelectedDate = date?.Date ?? CurrentDate.ToDateTime(TimeOnly.MinValue);

            if (SelectedDate < ValidationRules.MinimumDiaryDate ||
                SelectedDate > ValidationRules.MaximumDiaryDate)
            {
                return BadRequest();
            }

            Entries = await _context.DiaryEntries
                .Include(entry => entry.Food)
                .Include(entry => entry.FoodPortion)
                .Where(entry =>
                    entry.UserId == userId &&
                    entry.Date.Date == SelectedDate)
                .ToListAsync();

            TotalCalories = Entries.Sum(entry => entry.CaloriesConsumed);
            TotalProtein = Entries.Sum(entry => entry.ProteinConsumed);
            TotalCarbohydrates = Entries.Sum(entry => entry.CarbohydratesConsumed);
            TotalFat = Entries.Sum(entry => entry.FatConsumed);

            var profile = await _context.UserProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == userId);

            if (profile != null)
            {
                DailyCalorieTarget = profile.CalculateEffectiveCalorieTarget(CurrentDate);
                CaloriesRemaining = DailyCalorieTarget - TotalCalories;

                if (DailyCalorieTarget > 0)
                {
                    CalorieProgressPercent =
                        (TotalCalories / DailyCalorieTarget) * 100;

                    CalorieProgressPercent =
                        Math.Min(CalorieProgressPercent, 100);
                }

                MacroGoalProgress = _macroTargetCalculator
                    .Calculate(profile, CurrentDate)?
                    .CreateProgress(
                        TotalProtein,
                        TotalCarbohydrates,
                        TotalFat);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCopyPreviousDayAsync(
            DateTime targetDate,
            bool confirmAdditive)
        {
            if (!ValidationRules.IsValidDiaryDate(targetDate) ||
                targetDate.Date <= ValidationRules.MinimumDiaryDate)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var userId = _userManager.GetUserId(User);

            if (userId == null)
            {
                return Challenge();
            }

            var result = await _diaryCopyService.CopyAsync(
                userId,
                targetDate.AddDays(-1),
                targetDate,
                confirmAdditive);

            TempData["UiStatusMessage"] = result.Outcome switch
            {
                DiaryCopyOutcome.Success =>
                    $"Copied {result.EntriesCopied} {(result.EntriesCopied == 1 ? "entry" : "entries")} from the previous day.",
                DiaryCopyOutcome.NoSourceEntries =>
                    "There are no entries on the previous day to copy.",
                DiaryCopyOutcome.TargetNotEmpty =>
                    "The destination already contains entries. Tick the confirmation box to add the copied entries without replacing anything.",
                _ => "That copy request is not valid."
            };

            return RedirectToPage(new
            {
                date = targetDate.ToString("yyyy-MM-dd")
            });
        }
    }
}
