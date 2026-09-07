using CalorieTracker.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.Database;

public class CapyNameMigrationTests
{
    private const string PreviousProductionMigration =
        "20260902145133_BackfillValidationDataAndExternalUniqueness";

    [Fact]
    public async Task CapyNameMigration_UpgradesImmediatelyPreviousSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync(PreviousProductionMigration);

        var beforeUpgrade = await context.Database.GetDbConnection()
            .CreateCommand()
            .WithTextAsync("SELECT COUNT(*) FROM pragma_table_info('UserCapyAppearances') WHERE name = 'Name'");
        Assert.Equal(0L, beforeUpgrade);

        await context.Database.MigrateAsync();

        var afterUpgrade = await context.Database.GetDbConnection()
            .CreateCommand()
            .WithTextAsync("SELECT COUNT(*) FROM pragma_table_info('UserCapyAppearances') WHERE name = 'Name'");
        Assert.Equal(1L, afterUpgrade);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}

internal static class DbCommandExtensions
{
    public static async Task<long> WithTextAsync(
        this System.Data.Common.DbCommand command,
        string commandText)
    {
        await using (command)
        {
            command.CommandText = commandText;
            return (long)(await command.ExecuteScalarAsync())!;
        }
    }
}
