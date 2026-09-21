using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Pages
{
    [Authorize]
    public class CustomisationModel : PageModel
    {
        public const int CategoryPageSize = 9;
        private static readonly IReadOnlyDictionary<string, string[]> CategoryFilters =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["outfits"] = [CapyCategories.Clothes],
                ["face-accessories"] = [CapyCategories.FaceAccessory],
                ["neck-accessories"] = [CapyCategories.NeckAccessory],
                ["hats"] = [CapyCategories.HatHair],
                ["backgrounds"] = [CapyCategories.Background],
                ["colours"] = [CapyCategories.Expression],
                ["titles"] = []
            };
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CapyProvisioningService _capyProvisioningService;
        private readonly ProgressionAchievementHooks _progressionHooks;

        public CustomisationModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            CapyProvisioningService capyProvisioningService,
            ProgressionAchievementHooks progressionHooks)
        {
            _context = context;
            _userManager = userManager;
            _capyProvisioningService = capyProvisioningService;
            _progressionHooks = progressionHooks;
        }

        public UserCapyAppearance? CapyAppearance { get; set; }

        [BindProperty]
        public string? CapyName { get; set; }

        [BindProperty]
        public string? OutfitName { get; set; }

        public List<CapyItem> OwnedItems { get; set; } = [];
        public List<CapyItem> CatalogueItems { get; set; } = [];
        public List<SavedCapyOutfit> SavedOutfits { get; set; } = [];
        public bool NeedsProvisioning { get; set; }
        public bool HasUserProfile { get; set; }
        public string SelectedCategory { get; private set; } = "all";
        public string SelectedInventoryFilter { get; private set; } = "all";

        public async Task OnGetAsync(string? category = null, string? inventory = null)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return;

            HasUserProfile = await _context.UserProfiles
                .AnyAsync(profile => profile.UserId == userId);

            CapyAppearance = await _context.UserCapyAppearances
                .Include(appearance => appearance.Expression)
                .Include(appearance => appearance.HatHair)
                .Include(appearance => appearance.FaceAccessory)
                .Include(appearance => appearance.NeckAccessory)
                .Include(appearance => appearance.Clothes)
                .Include(appearance => appearance.Background)
                .FirstOrDefaultAsync(appearance =>
                    appearance.UserId == userId);

            CapyName = CapyAppearance?.Name;

            var ownedItems = await _context.UserCapyItems
                .Where(userItem => userItem.UserId == userId)
                .Include(userItem => userItem.CapyItem)
                .Where(userItem => userItem.CapyItem.IsActive)
                .ToListAsync();

            var ownedItemIds = ownedItems
                .Select(userItem => userItem.CapyItemId)
                .ToHashSet();

            NeedsProvisioning =
                CapyAppearance == null ||
                await _context.CapyItems.AnyAsync(item =>
                    item.IsActive &&
                    item.IsStarter &&
                    !ownedItemIds.Contains(item.Id));

            OwnedItems = ownedItems
                .Select(userItem => userItem.CapyItem)
                .OrderBy(item => item.Name)
                .ToList();

            SelectedCategory = CategoryFilters.ContainsKey(category ?? string.Empty)
                ? category!
                : "all";
            SelectedInventoryFilter = inventory == "owned" ? "owned" : "all";

            var catalogueQuery = _context.CapyItems
                .Where(item => item.IsActive);

            if (SelectedCategory != "all")
            {
                var itemCategories = CategoryFilters[SelectedCategory];
                catalogueQuery = catalogueQuery.Where(item => itemCategories.Contains(item.Category));
            }

            if (SelectedInventoryFilter == "owned")
                catalogueQuery = catalogueQuery.Where(item => ownedItemIds.Contains(item.Id));

            CatalogueItems = await catalogueQuery
                .OrderBy(item => item.Name)
                .ToListAsync();

            SavedOutfits = await _context.SavedCapyOutfits
                .Where(outfit => outfit.UserId == userId)
                .OrderBy(outfit => outfit.Name)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostRenameAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return Unauthorized();

            if (CapyName != null && ContainsForbiddenCharacters(CapyName))
            {
                ModelState.AddModelError(
                    nameof(CapyName),
                    "Capy name cannot contain line breaks or control characters.");
            }

            var trimmedName = CapyName?.Trim();

            if (!string.IsNullOrWhiteSpace(trimmedName) &&
                 trimmedName.Length > UserCapyAppearance.MaxNameLength)
            {
                ModelState.AddModelError(
                    nameof(CapyName),
                    $"Capy name must be {UserCapyAppearance.MaxNameLength} characters or fewer.");
            }

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                CapyName = trimmedName;
                return Page();
            }

            await _capyProvisioningService.ProvisionAsync(userId);

            var appearance = await _context.UserCapyAppearances
                .FirstOrDefaultAsync(item => item.UserId == userId);

            if (appearance == null)
                return NotFound();

            appearance.Name = string.IsNullOrWhiteSpace(trimmedName)
                ? null
                : trimmedName;

            await _context.SaveChangesAsync();

            TempData["UiStatusMessage"] = "Capy name saved.";
            return RedirectToPage();
        }

        private static bool ContainsForbiddenCharacters(string value) =>
            value.Any(character =>
                char.IsControl(character) ||
                character is '\u2028' or '\u2029');

        public async Task<IActionResult> OnPostProvisionAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return Unauthorized();

            await _capyProvisioningService.ProvisionAsync(userId);

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostEquipAsync(
            int? itemId,
            string category,
            CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return Unauthorized();

            // Equip also self-heals legacy users instead of depending on a prior GET provisioning request.
            await _capyProvisioningService.ProvisionAsync(userId);

            var appearance = await _context.UserCapyAppearances
                .FirstOrDefaultAsync(appearance =>
                    appearance.UserId == userId);

            if (appearance == null)
                return NotFound();

            CapyItem? item = null;

            if (itemId.HasValue)
            {
                item = await _context.CapyItems
                    .FirstOrDefaultAsync(item =>
                        item.Id == itemId.Value &&
                        item.IsActive);

                if (item == null)
                    return NotFound();

                if (item.Category != category)
                    return BadRequest();

                var userOwnsItem = await _context.UserCapyItems
                    .AnyAsync(userItem =>
                        userItem.UserId == userId &&
                        userItem.CapyItemId == item.Id);

                if (!userOwnsItem)
                    return Forbid();
            }

            switch (category)
            {
                case CapyCategories.Background:
                    if (item == null)
                        return BadRequest();

                    appearance.BackgroundId = item.Id;
                    break;

                case CapyCategories.Expression:
                    if (item == null)
                        return BadRequest();

                    appearance.ExpressionId = item.Id;
                    break;

                case CapyCategories.Clothes:
                    appearance.ClothesId = item?.Id;
                    break;

                case CapyCategories.NeckAccessory:
                    appearance.NeckAccessoryId = item?.Id;
                    break;

                case CapyCategories.HatHair:
                    appearance.HatHairId = item?.Id;
                    break;

                case CapyCategories.FaceAccessory:
                    appearance.FaceAccessoryId = item?.Id;
                    break;

                default:
                    return BadRequest();
            }

            await _context.SaveChangesAsync();
            await _progressionHooks.EvaluateCustomisationAsync(
                userId,
                cancellationToken);

            return new JsonResult(new
            {
                success = true,
                category,
                itemId = item?.Id,
                imagePath = item?.ImagePath
            });
        }

        public async Task<IActionResult> OnPostUnlockAsync(int itemId)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return Unauthorized();

            await _capyProvisioningService.ProvisionAsync(userId);

            // Temporary MVP policy: any active cosmetic may be self-unlocked.
            // Future achievement or currency rules must be enforced here on the server.
            var item = await _context.CapyItems
                .FirstOrDefaultAsync(item =>
                    item.Id == itemId &&
                    item.IsActive);

            if (item == null)
                return NotFound();

            var alreadyOwned = await _context.UserCapyItems
                .AnyAsync(userItem =>
                    userItem.UserId == userId &&
                    userItem.CapyItemId == item.Id);

            if (!alreadyOwned)
            {
                _context.UserCapyItems.Add(new UserCapyItem
                {
                    UserId = userId,
                    CapyItemId = item.Id
                });

                await _context.SaveChangesAsync();
            }

            return new JsonResult(new
            {
                success = true,
                itemId = item.Id,
                name = item.Name,
                category = item.Category,
                imagePath = item.ImagePath
            });
        }

        public async Task<IActionResult> OnPostSaveOutfitAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var name = OutfitName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > SavedCapyOutfit.MaxNameLength)
            {
                TempData["UiStatusMessage"] = $"Outfit names must be between 1 and {SavedCapyOutfit.MaxNameLength} characters.";
                return RedirectToPage();
            }

            await _capyProvisioningService.ProvisionAsync(userId);
            var appearance = await _context.UserCapyAppearances.SingleAsync(item => item.UserId == userId);
            _context.SavedCapyOutfits.Add(new SavedCapyOutfit
            {
                UserId = userId, Name = name,
                ExpressionId = appearance.ExpressionId, HatHairId = appearance.HatHairId,
                FaceAccessoryId = appearance.FaceAccessoryId, NeckAccessoryId = appearance.NeckAccessoryId,
                ClothesId = appearance.ClothesId, BackgroundId = appearance.BackgroundId
            });
            await _context.SaveChangesAsync();
            TempData["UiStatusMessage"] = $"{name} saved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEquipOutfitAsync(int outfitId, CancellationToken cancellationToken = default)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            await _capyProvisioningService.ProvisionAsync(userId);
            var outfit = await _context.SavedCapyOutfits.SingleOrDefaultAsync(item => item.Id == outfitId && item.UserId == userId, cancellationToken);
            if (outfit == null) return NotFound();
            var appearance = await _context.UserCapyAppearances.SingleAsync(item => item.UserId == userId, cancellationToken);
            var ownedActiveItems = await _context.UserCapyItems
                .Where(item => item.UserId == userId && item.CapyItem.IsActive)
                .Select(item => new { item.CapyItemId, item.CapyItem.Category })
                .ToListAsync(cancellationToken);

            int? AvailableItem(int? itemId, string category) => itemId.HasValue && ownedActiveItems.Any(item => item.CapyItemId == itemId && item.Category == category) ? itemId : null;
            appearance.ExpressionId = AvailableItem(outfit.ExpressionId, CapyCategories.Expression) ?? appearance.ExpressionId;
            appearance.BackgroundId = AvailableItem(outfit.BackgroundId, CapyCategories.Background) ?? appearance.BackgroundId;
            appearance.HatHairId = AvailableItem(outfit.HatHairId, CapyCategories.HatHair);
            appearance.FaceAccessoryId = AvailableItem(outfit.FaceAccessoryId, CapyCategories.FaceAccessory);
            appearance.NeckAccessoryId = AvailableItem(outfit.NeckAccessoryId, CapyCategories.NeckAccessory);
            appearance.ClothesId = AvailableItem(outfit.ClothesId, CapyCategories.Clothes);
            await _context.SaveChangesAsync(cancellationToken);
            await _progressionHooks.EvaluateCustomisationAsync(userId, cancellationToken);
            TempData["UiStatusMessage"] = $"{outfit.Name} equipped.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteOutfitAsync(int outfitId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var outfit = await _context.SavedCapyOutfits.SingleOrDefaultAsync(item => item.Id == outfitId && item.UserId == userId);
            if (outfit == null) return NotFound();
            _context.SavedCapyOutfits.Remove(outfit);
            await _context.SaveChangesAsync();
            TempData["UiStatusMessage"] = "Saved outfit deleted.";
            return RedirectToPage();
        }

    }
}
