using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Database;

public class DailyMaintenanceSnapshotMigrationTests
{
    private const string PreviousProductionMigration =
        "20260907141728_AddCapyName";

    [Fact]
    public async Task Migration_UpgradesPreviousSchemaAndBackfillsValidDistinctDiaryDates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync(PreviousProductionMigration);

        await AddUserAsync(context, "user-1");
        await AddUserAsync(context, "user-2");
        await AddUserAsync(context, "invalid-user");

        var firstProfile = Profile(
            "user-1",
            new DateTime(2000, 2, 29),
            ProfileOptions.Male,
            ProfileOptions.Sedentary,
            180,
            80);
        firstProfile.Goal = ProfileOptions.Gain;
        firstProfile.WeeklyGoalKg = 1;
        firstProfile.CustomCalorieTarget = 9999;
        var secondProfile = Profile(
            "user-2",
            new DateTime(1990, 1, 1),
            ProfileOptions.Female,
            ProfileOptions.ModeratelyActive,
            170,
            70);
        var invalidProfile = Profile(
            "invalid-user",
            new DateTime(2010, 1, 1),
            ProfileOptions.Male,
            ProfileOptions.Sedentary,
            180,
            80);
        context.UserProfiles.AddRange(
            firstProfile,
            secondProfile,
            invalidProfile);
        await context.SaveChangesAsync();

        // Seed through the previous schema: the current Food/DiaryEntry model
        // contains columns introduced by the migration after this one.
        await InsertMeasuredFoodAsync(context, 7001, "user-1", "First food");
        await InsertMeasuredFoodAsync(context, 7002, "user-2", "Second food");
        await InsertMeasuredFoodAsync(context, 7003, "invalid-user", "Invalid food");

        await InsertDiaryEntryAsync(context, 8001, "user-1", 7001, "2025-02-28 00:00:00");
        await InsertDiaryEntryAsync(context, 8002, "user-1", 7001, "2025-02-28 18:30:00");
        await InsertDiaryEntryAsync(context, 8003, "user-1", 7001, "2025-03-01 00:00:00");
        await InsertDiaryEntryAsync(context, 8004, "user-2", 7002, "2025-02-28 00:00:00");
        await InsertDiaryEntryAsync(context, 8005, "invalid-user", 7003, "2025-02-28 00:00:00");

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        var snapshots = await context.DailyMaintenanceSnapshots
            .OrderBy(snapshot => snapshot.UserId)
            .ThenBy(snapshot => snapshot.Date)
            .ToListAsync();

        Assert.Equal(3, snapshots.Count);
        Assert.DoesNotContain(snapshots, snapshot =>
            snapshot.UserId == "invalid-user");

        var firstUserSnapshots = snapshots
            .Where(snapshot => snapshot.UserId == "user-1")
            .ToList();
        Assert.Equal(2, firstUserSnapshots.Count);
        Assert.Equal(new DateOnly(2025, 2, 28), firstUserSnapshots[0].Date);
        Assert.Equal(2172m, firstUserSnapshots[0].MaintenanceCalories);
        Assert.Equal(new DateOnly(2025, 3, 1), firstUserSnapshots[1].Date);
        Assert.Equal(2166m, firstUserSnapshots[1].MaintenanceCalories);

        var secondUserSnapshot = Assert.Single(snapshots, snapshot =>
            snapshot.UserId == "user-2");
        Assert.Equal(2211.075m, secondUserSnapshot.MaintenanceCalories, 3);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private static Task AddUserAsync(
        ApplicationDbContext context,
        string userId) =>
        LegacyDatabaseFixtures.InsertUserAsync(
            context,
            userId,
            $"{userId}@example.test");

    private static UserProfile Profile(
        string userId,
        DateTime dateOfBirth,
        string calculationSex,
        string activityLevel,
        decimal heightCm,
        decimal weightKg) => new()
    {
        UserId = userId,
        MeasurementSystem = ProfileOptions.Metric,
        ThemePreference = ProfileOptions.SystemTheme,
        DateOfBirth = dateOfBirth,
        HeightCm = heightCm,
        WeightKg = weightKg,
        CalculationSex = calculationSex,
        ActivityLevel = activityLevel,
        Goal = ProfileOptions.Maintain
    };

    private static Task<int> InsertMeasuredFoodAsync(
        ApplicationDbContext context,
        int id,
        string userId,
        string name) => context.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "Foods"
            ("Id", "Name", "Calories", "Protein", "Carbohydrates", "Fat",
             "ServingSize", "CanonicalServingSize", "ServingUnit", "UserId",
             "IsDeleted", "IsFavourite")
        VALUES
            ({id}, {name}, 200, 10, 20, 5, 100, 100, 'g', {userId}, 0, 0);
        """);

    private static Task<int> InsertDiaryEntryAsync(
        ApplicationDbContext context,
        int id,
        string userId,
        int foodId,
        string date) => context.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "DiaryEntries"
            ("Id", "Date", "MealType", "FoodId", "Quantity", "UserId",
             "FoodNameSnapshot", "ServingSizeSnapshot",
             "CanonicalServingSizeSnapshot", "ServingUnitSnapshot",
             "CaloriesSnapshot", "ProteinSnapshot",
             "CarbohydratesSnapshot", "FatSnapshot")
        VALUES
            ({id}, {date}, 'Dinner', {foodId}, 100, {userId},
             'Snapshot food', 100, 100, 'g', 200, 10, 20, 5);
        """);
}
