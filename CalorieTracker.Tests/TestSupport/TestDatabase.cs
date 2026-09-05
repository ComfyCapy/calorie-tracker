using CalorieTracker.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.TestSupport;

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private TestDatabase(
        SqliteConnection connection,
        ApplicationDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public ApplicationDbContext Context { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        return await CreateAsync(useMigrations: false);
    }

    public static async Task<TestDatabase> CreateMigratedAsync()
    {
        return await CreateAsync(useMigrations: true);
    }

    private static async Task<TestDatabase> CreateAsync(bool useMigrations)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ApplicationDbContext(options);

        if (useMigrations)
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        return new TestDatabase(connection, context);
    }

    public async Task AddUserAsync(
        string userId,
        string userName = "test-user")
    {
        Context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@example.test",
            NormalizedEmail = $"{userName}@example.test".ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString()
        });

        await Context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
