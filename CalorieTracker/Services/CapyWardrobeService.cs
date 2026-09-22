using CalorieTracker.Data;
using CalorieTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public enum WardrobeEquipStatus { Equipped, NotFound, InvalidCategory, NotOwned }

public sealed record WardrobeEquipResult(WardrobeEquipStatus Status, CapyItem? Item = null);

public sealed class CapyWardrobeService(
    ApplicationDbContext context,
    CapyProvisioningService provisioning)
{
    public async Task<WardrobeEquipResult> EquipAsync(string userId, int? itemId, string category)
    {
        // Equip also self-heals legacy users instead of depending on a prior GET provisioning request.
        await provisioning.ProvisionAsync(userId);

        var appearance = await context.UserCapyAppearances
            .FirstOrDefaultAsync(appearance =>
                appearance.UserId == userId);

        if (appearance == null)
            return new(WardrobeEquipStatus.NotFound);

        CapyItem? item = null;

        if (itemId.HasValue)
        {
            item = await context.CapyItems
                .FirstOrDefaultAsync(item =>
                    item.Id == itemId.Value &&
                    item.IsActive);

            if (item == null)
                return new(WardrobeEquipStatus.NotFound);

            if (item.Category != category)
                return new(WardrobeEquipStatus.InvalidCategory);

            var userOwnsItem = await context.UserCapyItems
                .AnyAsync(userItem =>
                    userItem.UserId == userId &&
                    userItem.CapyItemId == item.Id);

            if (!userOwnsItem)
                return new(WardrobeEquipStatus.NotOwned);
        }

        switch (category)
        {
            case CapyCategories.Background:
                if (item == null)
                    return new(WardrobeEquipStatus.InvalidCategory);

                appearance.BackgroundId = item.Id;
                break;

            case CapyCategories.Expression:
                if (item == null)
                    return new(WardrobeEquipStatus.InvalidCategory);

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
                return new(WardrobeEquipStatus.InvalidCategory);
        }

        await context.SaveChangesAsync();
        return new(WardrobeEquipStatus.Equipped, item);
    }

    public async Task SaveOutfitAsync(string userId, string name)
    {
        await provisioning.ProvisionAsync(userId);
        var appearance = await context.UserCapyAppearances.SingleAsync(item => item.UserId == userId);
        context.SavedCapyOutfits.Add(new SavedCapyOutfit
        {
            UserId = userId, Name = name,
            ExpressionId = appearance.ExpressionId, HatHairId = appearance.HatHairId,
            FaceAccessoryId = appearance.FaceAccessoryId, NeckAccessoryId = appearance.NeckAccessoryId,
            ClothesId = appearance.ClothesId, BackgroundId = appearance.BackgroundId
        });
        await context.SaveChangesAsync();
    }

    public async Task<string?> EquipOutfitAsync(string userId, int outfitId, CancellationToken cancellationToken = default)
    {
        await provisioning.ProvisionAsync(userId);
        var outfit = await context.SavedCapyOutfits.SingleOrDefaultAsync(item => item.Id == outfitId && item.UserId == userId, cancellationToken);
        if (outfit == null) return null;
        var appearance = await context.UserCapyAppearances.SingleAsync(item => item.UserId == userId, cancellationToken);
        var ownedActiveItems = await context.UserCapyItems
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
        await context.SaveChangesAsync(cancellationToken);
        return outfit.Name;
    }

    public async Task<bool> DeleteOutfitAsync(string userId, int outfitId)
    {
        var outfit = await context.SavedCapyOutfits.SingleOrDefaultAsync(
            item => item.Id == outfitId && item.UserId == userId);
        if (outfit == null) return false;
        context.SavedCapyOutfits.Remove(outfit);
        await context.SaveChangesAsync();
        return true;
    }
}
