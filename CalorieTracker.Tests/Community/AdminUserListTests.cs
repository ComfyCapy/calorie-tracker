using CalorieTracker.Data;
using CalorieTracker.Pages.Admin;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace CalorieTracker.Tests.Community;

public sealed class AdminUserListTests
{
    [Fact]
    public async Task UserList_PreservesSearchLimitOrderingAndMultipleRoles()
    {
        using var factory = new IntegrationTestFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Standard", "Beta" })
            if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
        for (var i = 0; i < 31; i++)
        {
            var user = new ApplicationUser { UserName = $"match-{i:D2}", Email = $"match-{i:D2}@example.test" };
            Assert.True((await users.CreateAsync(user)).Succeeded);
            if (i == 0) Assert.True((await users.AddToRolesAsync(user, ["Standard", "Beta"])).Succeeded);
        }
        Assert.True((await users.CreateAsync(new ApplicationUser { UserName = "unrelated", Email = "other@example.test" })).Succeeded);
        var counter = new ReadCounter();
        await using var listContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(db.Database.GetDbConnection()).AddInterceptors(counter).Options);
        var page = new UsersModel(listContext, users, scope.ServiceProvider.GetRequiredService<IAuthorizationService>()) { Query = " match- " };
        PageModelTestContext.Attach(page, "actor");
        await page.OnGetAsync(default);
        Assert.Equal(2, counter.Count);
        Assert.Equal(30, page.Accounts.Count);
        Assert.Equal("match-00", page.Accounts[0].User.UserName);
        Assert.Equal("match-29", page.Accounts[^1].User.UserName);
        Assert.Equal(new[] { "Beta", "Standard" }, page.Accounts[0].Roles.Order());
        Assert.Equal(await users.GetRolesAsync(page.Accounts[0].User), page.Accounts[0].Roles);
        Assert.All(page.Accounts.Skip(1), account => Assert.Empty(account.Roles));
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
