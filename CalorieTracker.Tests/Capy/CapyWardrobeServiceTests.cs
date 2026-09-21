using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Capy;

public sealed class CapyWardrobeServiceTests
{
    [Theory]
    [InlineData(999, CapyCategories.HatHair, WardrobeEquipStatus.NotFound)]
    [InlineData(14, CapyCategories.HatHair, WardrobeEquipStatus.NotOwned)]
    [InlineData(2, CapyCategories.Background, WardrobeEquipStatus.InvalidCategory)]
    [InlineData(null, CapyCategories.Background, WardrobeEquipStatus.InvalidCategory)]
    [InlineData(null, CapyCategories.Expression, WardrobeEquipStatus.InvalidCategory)]
    [InlineData(null, "unknown", WardrobeEquipStatus.InvalidCategory)]
    public async Task InvalidEquip_PreservesExistingHat(int? id, string category, WardrobeEquipStatus status)
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        var service = CreateService(database);
        await service.EquipAsync("owner", 2, CapyCategories.HatHair);

        Assert.Equal(status, (await service.EquipAsync("owner", id, category)).Status);

        database.Context.ChangeTracker.Clear();
        Assert.Equal(2, (await database.Context.UserCapyAppearances.SingleAsync()).HatHairId);
    }

    [Fact]
    public async Task EquipAndRemoveHat_PersistAndReturnPreviewAsset()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        var service = CreateService(database);
        var result = await service.EquipAsync("owner", 2, CapyCategories.HatHair);
        Assert.Equal(WardrobeEquipStatus.Equipped, result.Status);
        Assert.Equal((await database.Context.CapyItems.FindAsync(2))!.ImagePath, result.Item!.ImagePath);
        database.Context.ChangeTracker.Clear();
        Assert.Equal(2, (await database.Context.UserCapyAppearances.SingleAsync()).HatHairId);

        Assert.Equal(WardrobeEquipStatus.Equipped,
            (await service.EquipAsync("owner", null, CapyCategories.HatHair)).Status);
        database.Context.ChangeTracker.Clear();
        Assert.Null((await database.Context.UserCapyAppearances.SingleAsync()).HatHairId);
    }

    [Fact]
    public async Task SavedOutfit_IsOwnerScopedAndRestoresCapturedEquipment()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("owner", "owner");
        await database.AddUserAsync("other", "other");
        var service = CreateService(database);
        await service.EquipAsync("owner", 2, CapyCategories.HatHair);
        await service.EquipAsync("owner", 15, CapyCategories.Clothes);
        await service.SaveOutfitAsync("owner", "Favourite");
        var outfit = await database.Context.SavedCapyOutfits.SingleAsync();
        await service.EquipAsync("owner", 3, CapyCategories.HatHair);
        Assert.Null(await service.EquipOutfitAsync("other", outfit.Id));
        Assert.False(await service.DeleteOutfitAsync("other", outfit.Id));
        Assert.Equal("Favourite", await service.EquipOutfitAsync("owner", outfit.Id));
        database.Context.ChangeTracker.Clear();
        var appearance = await database.Context.UserCapyAppearances.SingleAsync(x => x.UserId == "owner");
        Assert.Equal(2, appearance.HatHairId);
        Assert.Equal(15, appearance.ClothesId);
        Assert.Equal(outfit.BackgroundId, appearance.BackgroundId);
        Assert.Equal(outfit.ExpressionId, appearance.ExpressionId);
        Assert.True(await service.DeleteOutfitAsync("owner", outfit.Id));
        Assert.False(await service.DeleteOutfitAsync("owner", outfit.Id));
        Assert.Null(await service.EquipOutfitAsync("owner", outfit.Id));
    }

    [Fact]
    public async Task SavedOutfit_InvalidSlotsPreserveRequiredLayersAndClearOptionalLayers()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("owner");
        var service = CreateService(database);
        await service.EquipAsync("owner", 2, CapyCategories.HatHair);
        await service.EquipAsync("owner", 15, CapyCategories.Clothes);
        await service.SaveOutfitAsync("owner", "Legacy");
        var appearance = await database.Context.UserCapyAppearances.SingleAsync();
        var background = appearance.BackgroundId;
        var expression = appearance.ExpressionId;
        var outfit = await database.Context.SavedCapyOutfits.SingleAsync();
        outfit.BackgroundId = 2; // Existing item, but wrong category.
        outfit.ExpressionId = null;
        outfit.ClothesId = 14; // Active but unowned.
        (await database.Context.CapyItems.FindAsync(2))!.IsActive = false;
        await database.Context.SaveChangesAsync();

        Assert.Equal(WardrobeEquipStatus.NotFound,
            (await service.EquipAsync("owner", 2, CapyCategories.HatHair)).Status);
        Assert.Equal("Legacy", await service.EquipOutfitAsync("owner", outfit.Id));
        database.Context.ChangeTracker.Clear();
        appearance = await database.Context.UserCapyAppearances.SingleAsync();
        Assert.Equal(background, appearance.BackgroundId);
        Assert.Equal(expression, appearance.ExpressionId);
        Assert.Null(appearance.HatHairId);
        Assert.Null(appearance.ClothesId);
    }

    private static CapyWardrobeService CreateService(TestDatabase database) =>
        new(database.Context, new CapyProvisioningService(database.Context));
}
