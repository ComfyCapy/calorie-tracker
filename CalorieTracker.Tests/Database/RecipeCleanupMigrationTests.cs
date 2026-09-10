using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CalorieTracker.Tests.Database;

public sealed class RecipeCleanupMigrationTests
{
    [Fact]
    public async Task CleanupDropsRecipeDefinitionsButPreservesHistoricalDiaryFood()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ApplicationDbContext(options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260910052902_AddLazycoreFoodReuse");
        context.Users.Add(new ApplicationUser
        {
            Id = "owner",
            UserName = "owner",
            NormalizedUserName = "OWNER",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        var recipeFood = TestData.Food("owner", name: "Historical recipe");
        recipeFood.Source = "Recipe";
        recipeFood.ServingBasis = FoodServingBasis.Portion;
        recipeFood.ServingSize = 1;
        recipeFood.CanonicalServingSize = 1;
        recipeFood.ServingUnit = "serving";
        recipeFood.PortionLabel = "serving";
        context.Foods.Add(recipeFood);
        await context.SaveChangesAsync();
        var diaryEntry = TestData.DiaryEntry("owner", recipeFood, 1);
        context.DiaryEntries.Add(diaryEntry);
        await context.SaveChangesAsync();
        const string ownerId = "owner";
        const string recipeName = "Historical recipe";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"Recipes\" (\"FoodId\", \"UserId\", \"Name\", \"YieldServings\") VALUES ({recipeFood.Id}, {ownerId}, {recipeName}, {1})");

        await migrator.MigrateAsync();

        context.ChangeTracker.Clear();
        var preservedFood = await context.Foods.SingleAsync();
        var preservedEntry = await context.DiaryEntries.SingleAsync();
        Assert.True(preservedFood.IsDeleted);
        Assert.Equal("Historical recipe", preservedEntry.FoodNameSnapshot);
        Assert.Equal(200m, preservedEntry.CaloriesConsumed);
        Assert.Equal(0, await TableCountAsync(connection, "Recipes"));
        Assert.Equal(0, await TableCountAsync(connection, "RecipeIngredients"));
    }

    private static async Task<long> TableCountAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
