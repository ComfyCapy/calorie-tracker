using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CalorieTracker.Tests.Database;

public class ProductionReadinessTests
{
    [Fact]
    public async Task FreshDatabase_MigrationsCreateCurrentSchemaAndCapyCatalog()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();

        var availableMigrations = database.Context.Database
            .GetMigrations()
            .ToList();
        var appliedMigrations = (await database.Context.Database
                .GetAppliedMigrationsAsync())
            .ToList();
        var items = await database.Context.CapyItems
            .OrderBy(item => item.Id)
            .ToListAsync();

        Assert.Equal(availableMigrations, appliedMigrations);
        Assert.Equal(28, appliedMigrations.Count);
        Assert.Equal(14, items.Count);

        var defaultExpression = Assert.Single(items, item =>
            item.Category == CapyCategories.Expression && item.IsDefault);
        Assert.Equal(1, defaultExpression.Id);
        Assert.Equal("Base Capy", defaultExpression.Name);
        Assert.Equal(
            "/images/capy/expressions/Capy-Base.png",
            defaultExpression.ImagePath);
        Assert.True(defaultExpression.IsActive);
        Assert.True(defaultExpression.IsStarter);

        var defaultBackground = Assert.Single(items, item =>
            item.Category == CapyCategories.Background && item.IsDefault);
        Assert.Equal(13, defaultBackground.Id);
        Assert.Equal("White", defaultBackground.Name);
        Assert.Equal(
            "/images/capy/backgrounds/BG-White.png",
            defaultBackground.ImagePath);
        Assert.True(defaultBackground.IsActive);
        Assert.True(defaultBackground.IsStarter);

        var starterIds = items
            .Where(item => item.IsActive && item.IsStarter)
            .Select(item => item.Id);
        Assert.Equal(Enumerable.Range(1, 13), starterIds);

        var crown = Assert.Single(items, item => item.Name == "Gold Crown");
        Assert.Equal(14, crown.Id);
        Assert.Equal(CapyCategories.HatHair, crown.Category);
        Assert.Equal(
            "/images/capy/hats-hair/Capy-Crown-Gold.png",
            crown.ImagePath);
        Assert.True(crown.IsActive);
        Assert.False(crown.IsDefault);
        Assert.False(crown.IsStarter);
    }

    [Fact]
    public async Task FreshMigratedDatabase_IdentityUserCreationAndProvisioningWork()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        using var userManager = CreateUserManager(database.Context);
        var user = new ApplicationUser
        {
            Id = "fresh-user",
            UserName = "fresh-user",
            Email = "fresh-user@example.test",
            FirstName = "Fresh"
        };

        var creationResult = await userManager.CreateAsync(
            user,
            "FreshUser1!");

        Assert.True(
            creationResult.Succeeded,
            string.Join("; ", creationResult.Errors.Select(error =>
                error.Description)));

        var service = new CapyProvisioningService(database.Context);

        await service.ProvisionAsync("fresh-user");
        await service.ProvisionAsync("fresh-user");

        var appearance = await database.Context.UserCapyAppearances
            .SingleAsync(item => item.UserId == "fresh-user");
        var ownedIds = await database.Context.UserCapyItems
            .Where(item => item.UserId == "fresh-user")
            .Select(item => item.CapyItemId)
            .OrderBy(id => id)
            .ToListAsync();

        Assert.Equal(1, appearance.ExpressionId);
        Assert.Equal(13, appearance.BackgroundId);
        Assert.Equal(Enumerable.Range(1, 13), ownedIds);
        Assert.DoesNotContain(14, ownedIds);
    }

    [Fact]
    public async Task FreshMigratedDatabase_RejectsDuplicateProfilesPerUser()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");

        database.Context.UserProfiles.Add(Profile("user-1"));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        database.Context.UserProfiles.Add(Profile("user-1"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task FreshMigratedDatabase_RejectsDuplicateCapyInventoryRows()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");

        database.Context.UserCapyItems.AddRange(
            new UserCapyItem { UserId = "user-1", CapyItemId = 14 },
            new UserCapyItem { UserId = "user-1", CapyItemId = 14 });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task FreshMigratedDatabase_RejectsDuplicateCapyAppearancesPerUser()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");

        database.Context.UserCapyAppearances.AddRange(
            new UserCapyAppearance
            {
                UserId = "user-1",
                ExpressionId = 1,
                BackgroundId = 13
            },
            new UserCapyAppearance
            {
                UserId = "user-1",
                ExpressionId = 1,
                BackgroundId = 13
            });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task FreshMigratedDatabase_RejectsDuplicateExternalFoodsPerUser()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");
        var first = TestData.Food("user-1");
        first.Source = FoodSources.Usda;
        first.ExternalId = "123";
        var duplicate = TestData.Food("user-1");
        duplicate.Source = FoodSources.Usda;
        duplicate.ExternalId = "123";

        database.Context.Foods.AddRange(first, duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    private static UserProfile Profile(string userId) => new()
    {
        UserId = userId,
        MeasurementSystem = ProfileOptions.Metric,
        ThemePreference = ProfileOptions.SystemTheme,
        DateOfBirth = new DateTime(1990, 1, 1),
        HeightCm = 175,
        WeightKg = 75,
        CalculationSex = ProfileOptions.Male,
        ActivityLevel = ProfileOptions.Sedentary,
        Goal = ProfileOptions.Maintain
    };

    private static UserManager<ApplicationUser> CreateUserManager(
        ApplicationDbContext context)
    {
        var identityOptions = new IdentityOptions();
        identityOptions.User.RequireUniqueEmail = true;
        identityOptions.SignIn.RequireConfirmedAccount = true;

        return new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(context),
            Options.Create(identityOptions),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }
}
