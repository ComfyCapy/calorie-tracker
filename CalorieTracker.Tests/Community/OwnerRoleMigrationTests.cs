using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Security;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CalorieTracker.Tests.Community;

public sealed class OwnerRoleMigrationTests
{
    private const string CurrentReleaseMigration =
        "20260914051759_AddCommunityFoodsAndAccessRoles";

    [Fact]
    public async Task FreshDatabase_HasCanonicalFourRoleSet()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var roles = await database.Context.Roles
            .OrderBy(role => role.Name)
            .Select(role => new { role.Id, role.Name, role.NormalizedName })
            .ToArrayAsync();

        Assert.Equal(
            new[] { AccessRoles.Admin, AccessRoles.Beta, AccessRoles.Owner, AccessRoles.Standard },
            roles.Select(role => role.Name));
        var owner = Assert.Single(roles, role =>
            role.NormalizedName == AccessRoles.NormalizedOwner);
        Assert.Equal("role-owner", owner.Id);
        Assert.Empty(await database.Context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task UpgradeFromReleasedSchema_PreservesUsersMembershipsAndCommunityData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync(CurrentReleaseMigration);
        await LegacyDatabaseFixtures.InsertUserAsync(
            context,
            "existing",
            "existing@example.test");

        var standardRoleId = await context.Roles
            .Where(role => role.NormalizedName == AccessRoles.NormalizedStandard)
            .Select(role => role.Id)
            .SingleAsync();
        var adminRoleId = await context.Roles
            .Where(role => role.NormalizedName == AccessRoles.NormalizedAdmin)
            .Select(role => role.Id)
            .SingleAsync();
        context.UserRoles.AddRange(
            new IdentityUserRole<string>
            {
                UserId = "existing",
                RoleId = standardRoleId
            },
            new IdentityUserRole<string>
            {
                UserId = "existing",
                RoleId = adminRoleId
            });
        context.CommunityFoods.Add(new CommunityFood
        {
            SubmitterId = "existing",
            SubmittedUtc = DateTime.UtcNow,
            Status = CommunityFoodStatus.Pending,
            Name = "Preserved food",
            Calories = 1,
            Protein = 0,
            Carbohydrates = 0,
            Fat = 0,
            ServingSize = 1,
            CanonicalServingSize = 1,
            ServingUnit = "g"
        });
        await context.SaveChangesAsync();

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(4, await context.Roles.CountAsync());
        Assert.Equal(2, await context.UserRoles.CountAsync(
            membership => membership.UserId == "existing"));
        Assert.Equal("Preserved food", (await context.CommunityFoods.SingleAsync()).Name);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Upgrade_PreservesPreExistingNormalizedOwnerAndMembership()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync(CurrentReleaseMigration);
        await LegacyDatabaseFixtures.InsertUserAsync(
            context,
            "existing-owner",
            "existing-owner@example.test");
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AspNetRoles" ("Id", "ConcurrencyStamp", "Name", "NormalizedName")
            VALUES ('preserved-owner-role', 'preserved-owner-role', 'oWnEr', 'OWNER');
            INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
            VALUES ('existing-owner', 'preserved-owner-role');
            """);

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        var owner = await context.Roles.SingleAsync(role =>
            role.NormalizedName == AccessRoles.NormalizedOwner);
        Assert.Equal("preserved-owner-role", owner.Id);
        Assert.Equal("oWnEr", owner.Name);
        Assert.True(await context.UserRoles.AnyAsync(membership =>
            membership.UserId == "existing-owner" &&
            membership.RoleId == "preserved-owner-role"));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task MigrationScript_IsRoleDataOnlyAndNormalizedNameSafe()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var context = database.Context;
        var migrationId = context.Database.GetMigrations().Single(id =>
            id.EndsWith("_AddOwnerRole", StringComparison.Ordinal));
        var sql = context.GetService<IMigrator>()
            .GenerateScript(CurrentReleaseMigration, migrationId);

        Assert.Contains("INSERT OR IGNORE INTO \"AspNetRoles\"", sql);
        Assert.Contains("'OWNER'", sql);
        Assert.DoesNotContain("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
    }
}
