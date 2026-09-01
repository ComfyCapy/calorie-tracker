using CalorieTracker.Data;
using CalorieTracker.Models;
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
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomisationModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public UserCapyAppearance? CapyAppearance { get; set; }
        public string Username { get; set; } = string.Empty;

        public List<CapyItem> OwnedItems { get; set; } = [];
        public List<CapyItem> CatalogueItems { get; set; } = [];

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return;

            Username = _userManager.GetUserName(User) ?? "Your";

            CapyAppearance = await _context.UserCapyAppearances
                .Include(appearance => appearance.Expression)
                .Include(appearance => appearance.HatHair)
                .Include(appearance => appearance.FaceAccessory)
                .Include(appearance => appearance.NeckAccessory)
                .Include(appearance => appearance.Clothes)
                .Include(appearance => appearance.Background)
                .FirstOrDefaultAsync(appearance =>
                    appearance.UserId == userId);
 
            if (CapyAppearance == null)
            {
                var defaultExpression = await _context.CapyItems
                    .FirstAsync(item =>
                        item.Category == "Expression" &&
                        item.IsDefault);

                var defaultBackground = await _context.CapyItems
                    .FirstAsync(item =>
                        item.Category == "Background" &&
                        item.IsDefault);

                CapyAppearance = new UserCapyAppearance
                {
                    UserId = userId,
                    ExpressionId = defaultExpression.Id,
                    Expression = defaultExpression,
                    BackgroundId = defaultBackground.Id,
                    Background = defaultBackground
                };

                _context.UserCapyAppearances.Add(CapyAppearance);

                await _context.SaveChangesAsync();
            }

            // Give the user every currently available starter Capy item.
            var starterItems = await _context.CapyItems
                .Where(item =>
                    item.IsActive &&
                    item.IsStarter)
                .ToListAsync();

            var ownedItemIds = await _context.UserCapyItems
                .Where(userItem => userItem.UserId == userId)
                .Select(userItem => userItem.CapyItemId)
                .ToListAsync();

            var missingStarterItems = starterItems
                .Where(item => !ownedItemIds.Contains(item.Id))
                .Select(item => new UserCapyItem
                {
                    UserId = userId,
                    CapyItemId = item.Id
                });

            _context.UserCapyItems.AddRange(missingStarterItems);

            await _context.SaveChangesAsync();

            OwnedItems = await _context.UserCapyItems
            .Where(userItem => userItem.UserId == userId)
            .Include(userItem => userItem.CapyItem)
            .Where(userItem => userItem.CapyItem.IsActive)
            .Select(userItem => userItem.CapyItem)
            .OrderBy(item => item.Name)
            .ToListAsync();

            CatalogueItems = await _context.CapyItems
                .Where(item => item.IsActive)
                .OrderBy(item => item.Name)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostEquipAsync(
        int? itemId,
        string category)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return Unauthorized();

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
                case "Background":
                    if (item == null)
                        return BadRequest();

                    appearance.BackgroundId = item.Id;
                    break;

                case "Expression":
                    if (item == null)
                        return BadRequest();

                    appearance.ExpressionId = item.Id;
                    break;

                case "Clothes":
                    appearance.ClothesId = item?.Id;
                    break;

                case "NeckAccessory":
                    appearance.NeckAccessoryId = item?.Id;
                    break;

                case "HatHair":
                    appearance.HatHairId = item?.Id;
                    break;

                case "FaceAccessory":
                    appearance.FaceAccessoryId = item?.Id;
                    break;

                default:
                    return BadRequest();
            }

            await _context.SaveChangesAsync();

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

    }
}