using CalorieTracker.Models;
using CalorieTracker.Pages;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Capy;

public class CapyTests
{
    [Fact]
    public async Task ProvisionFreshUser_CreatesDefaultsAndGrantsOnlyStarterInventory()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new CapyProvisioningService(database.Context);

        await service.ProvisionAsync("user-1");

        var appearance = await database.Context.UserCapyAppearances.SingleAsync();
        var defaultExpression = await database.Context.CapyItems.SingleAsync(item =>
            item.Category == CapyCategories.Expression && item.IsDefault);
        var defaultBackground = await database.Context.CapyItems.SingleAsync(item =>
            item.Category == CapyCategories.Background && item.IsDefault);
        var expectedStarterIds = await database.Context.CapyItems
            .Where(item => item.IsActive && item.IsStarter)
            .Select(item => item.Id)
            .OrderBy(id => id)
            .ToListAsync();
        var ownedIds = await database.Context.UserCapyItems
            .Where(item => item.UserId == "user-1")
            .Select(item => item.CapyItemId)
            .OrderBy(id => id)
            .ToListAsync();

        Assert.Equal(defaultExpression.Id, appearance.ExpressionId);
        Assert.Equal(defaultBackground.Id, appearance.BackgroundId);
        Assert.Equal(expectedStarterIds, ownedIds);
        Assert.DoesNotContain(14, ownedIds);
    }

    [Fact]
    public async Task Provisioning_IsIdempotent()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var service = new CapyProvisioningService(database.Context);

        await service.ProvisionAsync("user-1");
        var inventoryCount = await database.Context.UserCapyItems.CountAsync();
        await service.ProvisionAsync("user-1");

        Assert.Equal(1, await database.Context.UserCapyAppearances.CountAsync());
        Assert.Equal(inventoryCount, await database.Context.UserCapyItems.CountAsync());
    }

    [Fact]
    public async Task ProvisionLegacyUser_RepairsMissingStarterInventory()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        database.Context.UserCapyAppearances.Add(new UserCapyAppearance
        {
            UserId = "user-1",
            ExpressionId = 1,
            BackgroundId = 13
        });
        database.Context.UserCapyItems.Add(new UserCapyItem
        {
            UserId = "user-1",
            CapyItemId = 1
        });
        await database.Context.SaveChangesAsync();
        var service = new CapyProvisioningService(database.Context);

        await service.ProvisionAsync("user-1");

        var starterCount = await database.Context.CapyItems
            .CountAsync(item => item.IsActive && item.IsStarter);
        Assert.Equal(
            starterCount,
            await database.Context.UserCapyItems.CountAsync(item =>
                item.UserId == "user-1"));
        Assert.Equal(1, await database.Context.UserCapyAppearances.CountAsync());
    }

    [Fact]
    public async Task UnlockThenEquip_PersistsForCurrentUserOnly()
    {
        await using var database = await TwoUserDatabaseAsync();
        var provisioning = new CapyProvisioningService(database.Context);
        await provisioning.ProvisionAsync("user-1");
        await provisioning.ProvisionAsync("user-2");
        var secondAppearance = await database.Context.UserCapyAppearances
            .SingleAsync(item => item.UserId == "user-2");
        var originalSecondHat = secondAppearance.HatHairId;
        var model = CreateModel(database, provisioning, "user-1");

        var unlockResult = await model.OnPostUnlockAsync(14);
        var equipResult = await model.OnPostEquipAsync(14, CapyCategories.HatHair);

        Assert.IsType<JsonResult>(unlockResult);
        Assert.IsType<JsonResult>(equipResult);
        Assert.True(await database.Context.UserCapyItems.AnyAsync(item =>
            item.UserId == "user-1" && item.CapyItemId == 14));
        Assert.False(await database.Context.UserCapyItems.AnyAsync(item =>
            item.UserId == "user-2" && item.CapyItemId == 14));
        Assert.Equal(
            14,
            (await database.Context.UserCapyAppearances.SingleAsync(item =>
                item.UserId == "user-1")).HatHairId);
        Assert.Equal(originalSecondHat, secondAppearance.HatHairId);
    }

    [Fact]
    public async Task EquipUnownedCosmetic_ReturnsForbidWithoutChangingAppearance()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var provisioning = new CapyProvisioningService(database.Context);
        await provisioning.ProvisionAsync("user-1");
        var appearance = await database.Context.UserCapyAppearances.SingleAsync();
        var model = CreateModel(database, provisioning, "user-1");

        var result = await model.OnPostEquipAsync(14, CapyCategories.HatHair);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(appearance.HatHairId);
    }

    [Fact]
    public async Task EquipCosmetic_WithManipulatedCategory_ReturnsBadRequest()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1");
        var provisioning = new CapyProvisioningService(database.Context);
        await provisioning.ProvisionAsync("user-1");
        var model = CreateModel(database, provisioning, "user-1");

        var result = await model.OnPostEquipAsync(2, CapyCategories.Background);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task ProvisionTwoUsers_KeepsInventoryAndAppearanceRowsIsolated()
    {
        await using var database = await TwoUserDatabaseAsync();
        var provisioning = new CapyProvisioningService(database.Context);

        await provisioning.ProvisionAsync("user-1");
        await provisioning.ProvisionAsync("user-2");

        Assert.Equal(2, await database.Context.UserCapyAppearances.CountAsync());
        var firstInventory = await database.Context.UserCapyItems
            .CountAsync(item => item.UserId == "user-1");
        var secondInventory = await database.Context.UserCapyItems
            .CountAsync(item => item.UserId == "user-2");
        Assert.True(firstInventory > 0);
        Assert.Equal(firstInventory, secondInventory);
    }

    private static CustomisationModel CreateModel(
        TestDatabase database,
        CapyProvisioningService provisioning,
        string userId)
    {
        var model = new CustomisationModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            provisioning);
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static async Task<TestDatabase> TwoUserDatabaseAsync()
    {
        var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync("user-1", "first");
        await database.AddUserAsync("user-2", "second");
        return database;
    }
}
