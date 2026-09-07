using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Pages;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Capy;

public class CapyNameTests
{
    [Fact]
    public void DisplayName_UsesDefaultForMissingOrWhitespaceName()
    {
        Assert.Equal(
            UserCapyAppearance.DefaultName,
            new UserCapyAppearance { Name = null }.DisplayName);
        Assert.Equal(
            UserCapyAppearance.DefaultName,
            new UserCapyAppearance { Name = " \t" }.DisplayName);
    }

    [Fact]
    public async Task Rename_TrimsAndPersistsNormalName()
    {
        await using var database = await CreateDatabaseWithCapyAsync("user-1");
        var model = CreateModel(database, "user-1");
        model.CapyName = "  Mabel  ";

        var result = await model.OnPostRenameAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Mabel", (await AppearanceAsync(database, "user-1")).Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rename_BlankInputResetsToDefault(string? input)
    {
        await using var database = await CreateDatabaseWithCapyAsync("user-1", "Old Name");
        var model = CreateModel(database, "user-1");
        model.CapyName = input;

        await model.OnPostRenameAsync();

        var appearance = await AppearanceAsync(database, "user-1");
        Assert.Null(appearance.Name);
        Assert.Equal(UserCapyAppearance.DefaultName, appearance.DisplayName);
    }

    [Fact]
    public async Task Rename_AcceptsExactlyFortyCharacters()
    {
        await using var database = await CreateDatabaseWithCapyAsync("user-1");
        var model = CreateModel(database, "user-1");
        model.CapyName = new string('A', UserCapyAppearance.MaxNameLength);

        var result = await model.OnPostRenameAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(
            UserCapyAppearance.MaxNameLength,
            (await AppearanceAsync(database, "user-1")).Name!.Length);
    }

    [Fact]
    public async Task Rename_RejectsOverlongNames()
    {
        await using var database = await CreateDatabaseWithCapyAsync("user-1", "Existing");
        var model = CreateModel(database, "user-1");
        model.CapyName = new string('A', UserCapyAppearance.MaxNameLength + 1);

        var result = await model.OnPostRenameAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Existing", (await AppearanceAsync(database, "user-1")).Name);
    }

    [Theory]
    [InlineData("Line\nBreak")]
    [InlineData("Tab\tName")]
    [InlineData("\n")]
    [InlineData("Line\u2028Break")]
    public async Task Rename_RejectsLineBreaksAndControlCharacters(string input)
    {
        await using var database = await CreateDatabaseWithCapyAsync("user-1", "Existing");
        var model = CreateModel(database, "user-1");
        model.CapyName = input;

        var result = await model.OnPostRenameAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Existing", (await AppearanceAsync(database, "user-1")).Name);
    }

    [Fact]
    public async Task Rename_AcceptsNormalUnicode()
    {
        await using var database = await CreateDatabaseWithCapyAsync("user-1");
        var model = CreateModel(database, "user-1");
        model.CapyName = "Élodie 🐹";

        await model.OnPostRenameAsync();

        Assert.Equal("Élodie 🐹", (await AppearanceAsync(database, "user-1")).Name);
    }

    [Fact]
    public async Task Rename_CannotModifyAnotherUsersAppearance()
    {
        await using var database = await CreateDatabaseWithCapyAsync("user-1");
        await database.AddUserAsync("user-2", "second");
        database.Context.UserCapyAppearances.Add(new UserCapyAppearance
        {
            UserId = "user-2",
            Name = "Second Capy"
        });
        await database.Context.SaveChangesAsync();

        var model = CreateModel(database, "user-1");
        model.CapyName = "First Capy";
        await model.OnPostRenameAsync();

        Assert.Equal("First Capy", (await AppearanceAsync(database, "user-1")).Name);
        Assert.Equal("Second Capy", (await AppearanceAsync(database, "user-2")).Name);
    }

    [Fact]
    public async Task CustomisationAndDashboardRenderNameAsEncodedUserContent()
    {
        const string userId = "render-name-user";
        const string name = "<b>Capy</b>";
        using var factory = new IntegrationTestFactory();
        await SeedAppearanceAsync(factory, userId, name);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", userId);

        var customisation = await client.GetStringAsync("/Customisation");
        var dashboard = await client.GetStringAsync("/");

        Assert.Contains("<details class=\"capy-name-editor\">", customisation);
        Assert.DoesNotContain(
            "<details class=\"capy-name-editor\" open",
            customisation);
        Assert.Contains("&lt;b&gt;Capy&lt;/b&gt;", customisation);
        Assert.DoesNotContain(name, customisation);
        Assert.Contains("&lt;b&gt;Capy&lt;/b&gt;", dashboard);
        Assert.DoesNotContain(name, dashboard);
    }

    private static CustomisationModel CreateModel(
        TestDatabase database,
        string userId)
    {
        var model = new CustomisationModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new CapyProvisioningService(database.Context));
        PageModelTestContext.Attach(model, userId);
        return model;
    }

    private static async Task<TestDatabase> CreateDatabaseWithCapyAsync(
        string userId,
        string? name = null)
    {
        var database = await TestDatabase.CreateAsync();
        await database.AddUserAsync(userId);
        database.Context.UserCapyAppearances.Add(new UserCapyAppearance
        {
            UserId = userId,
            Name = name
        });
        await database.Context.SaveChangesAsync();
        return database;
    }

    private static Task<UserCapyAppearance> AppearanceAsync(
        TestDatabase database,
        string userId) =>
        database.Context.UserCapyAppearances.SingleAsync(item => item.UserId == userId);

    private static async Task SeedAppearanceAsync(
        IntegrationTestFactory factory,
        string userId,
        string name)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = $"{userId}@EXAMPLE.TEST",
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        context.UserCapyAppearances.Add(new UserCapyAppearance
        {
            UserId = userId,
            Name = name
        });
        await context.SaveChangesAsync();
    }
}
