using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CalorieTracker.Tests.Progression;

public sealed class ProgressionMigrationTests
{
    private const string PreviousProductionMigration =
        "20260911012839_AddLastNameToApplicationUser";

    [Fact]
    public async Task AddProgression_UsesOnlyFourAdditiveCreateTableOperations()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var migration = Migration(database.Context);
        var operations = migration.UpOperations;
        var expectedTables = new HashSet<string>(StringComparer.Ordinal)
        {
            "UserAchievements",
            "UserDailyActivities",
            "UserProgressionStates",
            "UserXpEvents"
        };

        Assert.Equal(4, operations.Count);
        var createTables = operations
            .Select(operation => Assert.IsType<CreateTableOperation>(operation))
            .ToList();
        Assert.True(expectedTables.SetEquals(
            createTables.Select(operation => operation.Name)));

        var migrationId = AddProgressionMigrationId(database.Context);
        var migrator = database.Context.GetService<IMigrator>();
        var sql = migrator.GenerateScript(
            PreviousProductionMigration,
            migrationId);

        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__temp_", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            4,
            expectedTables.Count(table => sql.Contains(
                $"CREATE TABLE \"{table}\"",
                StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Migration_UpgradesPreviousProductionSchemaAndPreservesData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync(PreviousProductionMigration);
        await LegacyDatabaseFixtures.InsertUserAsync(
            context,
            "existing-user",
            "existing@example.test");
        var profile = new UserProfile
        {
            UserId = "existing-user",
            MeasurementSystem = ProfileOptions.Metric,
            ThemePreference = ProfileOptions.SystemTheme,
            DateOfBirth = new DateTime(1990, 1, 1),
            HeightCm = 175,
            WeightKg = 75,
            CalculationSex = ProfileOptions.Female,
            ActivityLevel = ProfileOptions.Sedentary,
            Goal = ProfileOptions.Maintain
        };
        var food = TestData.Food("existing-user", name: "Preserved food");
        var diaryEntry = TestData.DiaryEntry("existing-user", food, 100);
        context.AddRange(profile, food, diaryEntry);
        await context.SaveChangesAsync();

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        Assert.True(await context.Users.AnyAsync(user =>
            user.Id == "existing-user"));
        Assert.True(await context.UserProfiles.AnyAsync(item =>
            item.UserId == "existing-user" && item.HeightCm == 175));
        Assert.True(await context.Foods.AnyAsync(item =>
            item.UserId == "existing-user" && item.Name == "Preserved food"));
        Assert.True(await context.DiaryEntries.AnyAsync(item =>
            item.UserId == "existing-user"));
        Assert.Empty(await context.UserDailyActivities.ToListAsync());
        Assert.Empty(await context.UserAchievements.ToListAsync());
        Assert.Empty(await context.UserXpEvents.ToListAsync());
        Assert.Empty(await context.UserProgressionStates.ToListAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private static Migration Migration(ApplicationDbContext context)
    {
        var assembly = context.GetService<IMigrationsAssembly>();
        var migration = assembly.Migrations.Single(pair =>
            pair.Key.EndsWith("_AddProgression", StringComparison.Ordinal));

        return assembly.CreateMigration(
            migration.Value,
            context.Database.ProviderName!);
    }

    private static string AddProgressionMigrationId(
        ApplicationDbContext context) => context.Database
        .GetMigrations()
        .Single(id => id.EndsWith("_AddProgression", StringComparison.Ordinal));
}
