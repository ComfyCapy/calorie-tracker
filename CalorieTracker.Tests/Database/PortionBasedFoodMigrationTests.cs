using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Database;

public class PortionBasedFoodMigrationTests
{
    private const string PreviousMigration =
        "20260908134734_AddDailyMaintenanceSnapshots";

    [Fact]
    public async Task Migration_PreservesExistingMeasuredFoodAndDiarySnapshots()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync(PreviousMigration);

        await LegacyDatabaseFixtures.InsertUserAsync(
            context,
            "user-1",
            "user-1@example.test");

        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Foods"
                ("Id", "Name", "Calories", "Protein", "Carbohydrates", "Fat",
                 "ServingSize", "CanonicalServingSize", "ServingUnit", "UserId",
                 "IsDeleted", "IsFavourite")
            VALUES
                (7001, 'Existing measured food', 200, 10, 20, 5,
                 100, 100, 'g', 'user-1', 0, 0);
            """);

        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO "DiaryEntries"
                ("Id", "Date", "MealType", "FoodId", "Quantity", "UserId",
                 "FoodNameSnapshot", "ServingSizeSnapshot",
                 "CanonicalServingSizeSnapshot", "ServingUnitSnapshot",
                 "CaloriesSnapshot", "ProteinSnapshot",
                 "CarbohydratesSnapshot", "FatSnapshot")
            VALUES
                (8001, '2026-01-02 00:00:00', 'Lunch', 7001, 50, 'user-1',
                 'Existing measured food', 100, 100, 'g', 200, 10, 20, 5);
            """);

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        var food = await context.Foods.SingleAsync(food => food.Id == 7001);
        var entry = await context.DiaryEntries.SingleAsync(entry => entry.Id == 8001);

        Assert.Equal(FoodServingBasis.Measured, food.ServingBasis);
        Assert.Null(food.PortionLabel);
        Assert.Equal(100, food.ServingSize);
        Assert.Equal(100, food.CanonicalServingSize);
        Assert.Equal("g", food.ServingUnit);

        Assert.Equal(FoodServingBasis.Measured, entry.ServingBasisSnapshot);
        Assert.Null(entry.PortionLabelSnapshot);
        Assert.Equal(50, entry.Quantity);
        Assert.Equal(100, entry.CaloriesConsumed);
        Assert.Equal(5, entry.ProteinConsumed);
        Assert.Equal(10, entry.CarbohydratesConsumed);
        Assert.Equal(2.5m, entry.FatConsumed);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}
