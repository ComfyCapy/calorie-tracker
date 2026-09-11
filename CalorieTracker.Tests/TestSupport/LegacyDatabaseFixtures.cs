using CalorieTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Tests.TestSupport;

/// <summary>
/// Seeds identity rows into migration-baseline databases without asking the
/// current EF model to write columns that did not exist at that baseline.
/// </summary>
public static class LegacyDatabaseFixtures
{
    public static Task InsertUserAsync(
        ApplicationDbContext context,
        string userId,
        string? email = null) =>
        context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AspNetUsers"
                ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
                 "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled",
                 "LockoutEnabled", "AccessFailedCount", "SecurityStamp")
            VALUES
                ({userId}, {userId}, {userId.ToUpperInvariant()}, {email},
                 {email?.ToUpperInvariant()}, {false}, {false}, {false}, {false}, {0},
                 {Guid.NewGuid().ToString()});
            """);
}
