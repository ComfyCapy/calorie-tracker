using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Security;
using CalorieTracker.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CalorieTracker.Tests.Community;

public sealed class CommunityMigrationTests
{
    private const string PreviousProductionMigration = "20260913053152_AddProgression";

    [Fact]
    public async Task Migration_UpgradesProgressionSchema_BackfillsStandard_AndPreservesData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync(PreviousProductionMigration);
        await LegacyDatabaseFixtures.InsertUserAsync(context, "existing", "existing@example.test");
        context.UserProgressionStates.Add(new UserProgressionState
        {
            UserId = "existing", AchievementBackfillVersion = 1
        });
        await context.SaveChangesAsync();

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(new[] { AccessRoles.Admin, AccessRoles.Beta, AccessRoles.Owner, AccessRoles.Standard },
            await context.Roles.OrderBy(x => x.Name).Select(x => x.Name!).ToArrayAsync());
        Assert.True(await (from membership in context.UserRoles
                           join role in context.Roles on membership.RoleId equals role.Id
                           where membership.UserId == "existing" && role.NormalizedName == "STANDARD"
                           select membership).AnyAsync());
        Assert.Equal(1, (await context.UserProgressionStates.SingleAsync()).AchievementBackfillVersion);
        Assert.Empty(await context.CommunityFoods.ToListAsync());
        Assert.Empty(await context.CommunityFoodVotes.ToListAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Migration_IsAdditive_AndFreshDatabaseEnforcesVoteValue()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var context = database.Context;
        var migrationId = context.Database.GetMigrations().Single(id =>
            id.EndsWith("_AddCommunityFoodsAndAccessRoles", StringComparison.Ordinal));
        var sql = context.GetService<IMigrator>().GenerateScript(PreviousProductionMigration, migrationId);
        Assert.Contains("CREATE TABLE \"CommunityFoods\"", sql);
        Assert.Contains("CREATE TABLE \"CommunityFoodVotes\"", sql);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__temp_", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);

        await database.AddUserAsync("user");
        context.CommunityFoods.Add(new CommunityFood
        {
            Name = "Valid", SubmittedUtc = DateTime.UtcNow,
            Calories = 1, ServingSize = 1, CanonicalServingSize = 1,
            ServingUnit = "g", Status = CommunityFoodStatus.Pending
        });
        await context.SaveChangesAsync();
        var id = await context.CommunityFoods.Select(x => x.Id).SingleAsync();
        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"CommunityFoodVotes\" (\"CommunityFoodId\", \"UserId\", \"Value\") VALUES ({0}, 'user', 0)", id));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}
