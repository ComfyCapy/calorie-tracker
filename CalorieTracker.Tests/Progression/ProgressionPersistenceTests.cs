using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CalorieTracker.Tests.Progression;

public sealed class ProgressionPersistenceTests
{
    private static readonly DateTime AuditTimeUtc =
        new(2026, 9, 13, 5, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FreshMigratedSchema_HasApprovedTablesKeysAndForeignKeys()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var connection = (SqliteConnection)database.Context.Database
            .GetDbConnection();
        string[] expectedTables =
        [
            "UserAchievements",
            "UserDailyActivities",
            "UserProgressionStates",
            "UserXpEvents"
        ];

        Assert.Equal(expectedTables, await ProgressionTableNamesAsync(connection));
        Assert.Equal(
            ["UserId", "AchievementKey"],
            await PrimaryKeyColumnsAsync(connection, "UserAchievements"));
        Assert.Equal(
            ["UserId", "LocalDate"],
            await PrimaryKeyColumnsAsync(connection, "UserDailyActivities"));
        Assert.Equal(
            ["UserId"],
            await PrimaryKeyColumnsAsync(connection, "UserProgressionStates"));
        Assert.Equal(
            ["UserId", "EventKey"],
            await PrimaryKeyColumnsAsync(connection, "UserXpEvents"));

        foreach (var table in expectedTables)
        {
            var foreignKey = Assert.Single(
                await ForeignKeysAsync(connection, table));
            Assert.Equal("AspNetUsers", foreignKey.PrincipalTable);
            Assert.Equal("UserId", foreignKey.Column);
            Assert.Equal("Id", foreignKey.PrincipalColumn);
            Assert.Equal("CASCADE", foreignKey.OnDelete);
        }
    }

    [Fact]
    public async Task Model_HasApprovedLengthsDefaultsConstraintsAndNoExtraIndexes()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        var model = database.Context
            .GetService<IDesignTimeModel>()
            .Model;
        var activity = EntityType<UserDailyActivity>(model);
        var achievement = EntityType<UserAchievement>(model);
        var xpEvent = EntityType<UserXpEvent>(model);
        var state = EntityType<UserProgressionState>(model);

        Assert.Equal(
            UserDailyActivity.MaxTimeZoneIdLength,
            activity.FindProperty(nameof(UserDailyActivity.TimeZoneId))!
                .GetMaxLength());
        Assert.Equal(
            UserAchievement.MaxAchievementKeyLength,
            achievement.FindProperty(nameof(UserAchievement.AchievementKey))!
                .GetMaxLength());
        Assert.Equal(
            UserXpEvent.MaxEventKeyLength,
            xpEvent.FindProperty(nameof(UserXpEvent.EventKey))!
                .GetMaxLength());
        Assert.Equal(
            0,
            state.FindProperty(
                    nameof(UserProgressionState.AchievementBackfillVersion))!
                .GetDefaultValue());

        var xpCheck = Assert.Single(xpEvent.GetCheckConstraints());
        Assert.Equal("CK_UserXpEvents_Amount_Positive", xpCheck.Name);
        Assert.Equal("\"Amount\" > 0", xpCheck.Sql);

        var stateCheck = Assert.Single(state.GetCheckConstraints());
        Assert.Equal(
            "CK_UserProgressionStates_BackfillVersion_NonNegative",
            stateCheck.Name);
        Assert.Equal(
            "\"AchievementBackfillVersion\" >= 0",
            stateCheck.Sql);

        foreach (var entityType in new[]
                 {
                     activity,
                     achievement,
                     xpEvent,
                     state
                 })
        {
            Assert.Empty(entityType.GetIndexes());
            var foreignKey = Assert.Single(entityType.GetForeignKeys());
            Assert.Equal(typeof(ApplicationUser), foreignKey.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        }
    }

    [Fact]
    public async Task DuplicateUserAndLocalDate_IsRejected()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");
        var date = new DateOnly(2026, 9, 13);
        database.Context.UserDailyActivities.Add(
            Activity("user-1", date));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        database.Context.UserDailyActivities.Add(
            Activity("user-1", date));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicateUserAndAchievementKey_IsRejected()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");
        database.Context.UserAchievements.Add(
            Achievement("user-1", "diary.first-entry"));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        database.Context.UserAchievements.Add(
            Achievement("user-1", "diary.first-entry"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicateUserAndXpEventKey_IsRejected()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");
        database.Context.UserXpEvents.Add(
            XpEvent("user-1", "achievement:diary.first-entry"));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        database.Context.UserXpEvents.Add(
            XpEvent("user-1", "achievement:diary.first-entry"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task DifferentUsers_MayUseTheSameProgressionKeys()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1", "user-1");
        await database.AddUserAsync("user-2", "user-2");
        var date = new DateOnly(2026, 9, 13);

        foreach (var userId in new[] { "user-1", "user-2" })
        {
            database.Context.AddRange(
                Activity(userId, date),
                Achievement(userId, "diary.first-entry"),
                XpEvent(userId, "achievement:diary.first-entry"),
                new UserProgressionState { UserId = userId });
        }

        await database.Context.SaveChangesAsync();

        Assert.Equal(2, await database.Context.UserDailyActivities.CountAsync());
        Assert.Equal(2, await database.Context.UserAchievements.CountAsync());
        Assert.Equal(2, await database.Context.UserXpEvents.CountAsync());
        Assert.Equal(2, await database.Context.UserProgressionStates.CountAsync());
    }

    [Fact]
    public async Task DateOnly_RoundTripsAndOrdersAsIsoCalendarText()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");
        DateOnly[] expectedDates =
        [
            new(2024, 2, 29),
            new(2025, 12, 31),
            new(2026, 9, 13)
        ];
        database.Context.UserDailyActivities.AddRange(
            expectedDates
                .Reverse()
                .Select(date => Activity("user-1", date)));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var actualDates = await database.Context.UserDailyActivities
            .Where(activity => activity.UserId == "user-1")
            .OrderBy(activity => activity.LocalDate)
            .Select(activity => activity.LocalDate)
            .ToArrayAsync();
        var storedValues = await StoredDateValuesAsync(
            (SqliteConnection)database.Context.Database.GetDbConnection());

        Assert.Equal(expectedDates, actualDates);
        Assert.Equal(
            ["2024-02-29", "2025-12-31", "2026-09-13"],
            storedValues.Select(value => value.Value));
        Assert.All(storedValues, value => Assert.Equal("text", value.StorageType));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveXpAmount_IsRejected(int amount)
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");
        var xpEvent = XpEvent("user-1", $"invalid:{amount}");
        xpEvent.Amount = amount;
        database.Context.UserXpEvents.Add(xpEvent);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task NegativeBackfillVersion_IsRejected()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("user-1");
        database.Context.UserProgressionStates.Add(
            new UserProgressionState
            {
                UserId = "user-1",
                AchievementBackfillVersion = -1
            });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    private static UserDailyActivity Activity(string userId, DateOnly date) =>
        new()
        {
            UserId = userId,
            LocalDate = date,
            RecordedAtUtc = AuditTimeUtc,
            TimeZoneId = "Europe/London"
        };

    private static UserAchievement Achievement(string userId, string key) =>
        new()
        {
            UserId = userId,
            AchievementKey = key,
            UnlockedAtUtc = AuditTimeUtc
        };

    private static UserXpEvent XpEvent(string userId, string key) =>
        new()
        {
            UserId = userId,
            EventKey = key,
            Amount = 25,
            AwardedAtUtc = AuditTimeUtc
        };

    private static IEntityType EntityType<TEntity>(IModel model) =>
        model.FindEntityType(typeof(TEntity))!;

    private static async Task<string[]> ProgressionTableNamesAsync(
        SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table' AND name LIKE 'User%'
                AND name IN (
                    'UserAchievements',
                    'UserDailyActivities',
                    'UserProgressionStates',
                    'UserXpEvents')
            ORDER BY name;
            """;

        return await ReadStringsAsync(command);
    }

    private static async Task<string[]> PrimaryKeyColumnsAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT name
            FROM pragma_table_info('{tableName}')
            WHERE pk > 0
            ORDER BY pk;
            """;

        return await ReadStringsAsync(command);
    }

    private static async Task<ForeignKeyRow[]> ForeignKeysAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT "table", "from", "to", "on_delete"
            FROM pragma_foreign_key_list('{tableName}');
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<ForeignKeyRow>();

        while (await reader.ReadAsync())
        {
            rows.Add(new ForeignKeyRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return rows.ToArray();
    }

    private static async Task<StoredDateRow[]> StoredDateValuesAsync(
        SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT LocalDate, typeof(LocalDate)
            FROM UserDailyActivities
            ORDER BY LocalDate;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<StoredDateRow>();

        while (await reader.ReadAsync())
        {
            rows.Add(new StoredDateRow(
                reader.GetString(0),
                reader.GetString(1)));
        }

        return rows.ToArray();
    }

    private static async Task<string[]> ReadStringsAsync(
        SqliteCommand command)
    {
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();

        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values.ToArray();
    }

    private sealed record ForeignKeyRow(
        string PrincipalTable,
        string Column,
        string PrincipalColumn,
        string OnDelete);

    private sealed record StoredDateRow(
        string Value,
        string StorageType);
}
