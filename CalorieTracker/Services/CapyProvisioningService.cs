using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public class CapyProvisioningService
{
    private readonly ApplicationDbContext _context;

    public CapyProvisioningService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ProvisionAsync(string userId)
    {
        // This idempotent path also upgrades legacy users without relying on a prior Customisation GET.
        var appearanceExists = await _context.UserCapyAppearances
            .AnyAsync(appearance => appearance.UserId == userId);

        if (!appearanceExists)
        {
            var defaultExpressionId = await _context.CapyItems
                .Where(item =>
                    item.Category == CapyCategories.Expression &&
                    item.IsDefault &&
                    item.IsActive)
                .Select(item => item.Id)
                .FirstAsync();

            var defaultBackgroundId = await _context.CapyItems
                .Where(item =>
                    item.Category == CapyCategories.Background &&
                    item.IsDefault &&
                    item.IsActive)
                .Select(item => item.Id)
                .FirstAsync();

            _context.UserCapyAppearances.Add(new UserCapyAppearance
            {
                UserId = userId,
                ExpressionId = defaultExpressionId,
                BackgroundId = defaultBackgroundId
            });
        }

        var ownedItemIds = await _context.UserCapyItems
            .Where(userItem => userItem.UserId == userId)
            .Select(userItem => userItem.CapyItemId)
            .ToHashSetAsync();

        var missingStarterItemIds = await _context.CapyItems
            .Where(item =>
                item.IsActive &&
                item.IsStarter &&
                !ownedItemIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync();

        _context.UserCapyItems.AddRange(
            missingStarterItemIds.Select(itemId => new UserCapyItem
            {
                UserId = userId,
                CapyItemId = itemId
            }));

        await _context.SaveChangesAsync();
    }
}
