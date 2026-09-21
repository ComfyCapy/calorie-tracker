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
    public async Task SavedOutfit_RoundTripsThroughMigratedSchema()
    {
        await using var database = await TestDatabase.CreateMigratedAsync();
        await database.AddUserAsync("outfit-user");
        var model = CreateModel(database, "outfit-user");

        await new CapyProvisioningService(database.Context).ProvisionAsync("outfit-user");
        var appearance = await AppearanceAsync(database, "outfit-user");
        appearance.ClothesId = 15;
        await database.Context.SaveChangesAsync();

        model.OutfitName = "Cosy brown";
        Assert.IsType<RedirectToPageResult>(await model.OnPostSaveOutfitAsync());
        var outfit = await database.Context.SavedCapyOutfits.SingleAsync();
        await model.OnGetAsync();
        Assert.Single(model.SavedOutfits);

        appearance.ClothesId = null;
        await database.Context.SaveChangesAsync();
        Assert.IsType<RedirectToPageResult>(await model.OnPostEquipOutfitAsync(outfit.Id));
        Assert.Equal(15, (await AppearanceAsync(database, "outfit-user")).ClothesId);

        Assert.IsType<RedirectToPageResult>(await model.OnPostDeleteOutfitAsync(outfit.Id));
        Assert.Empty(await database.Context.SavedCapyOutfits.ToListAsync());
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
        var faceAccessories = await client.GetStringAsync(
            "/Customisation?category=face-accessories");
        var neckAccessories = await client.GetStringAsync(
            "/Customisation?category=neck-accessories");
        var hats = await client.GetStringAsync(
            "/Customisation?category=hats");
        var backgrounds = await client.GetStringAsync(
            "/Customisation?category=backgrounds");
        var dashboard = await client.GetStringAsync("/");

        Assert.Contains(
            "class=\"ct-scenic-header ct-scenic-header--with-art ct-scenic-header--full-bleed-art secondary-page-hero\"",
            customisation);
        Assert.Contains("class=\"ct-page-title\">Customisation</h1>", customisation);
        Assert.DoesNotContain("Make your Capy yours.", customisation);
        Assert.DoesNotContain("ct-page-subtitle", customisation);
        Assert.Contains("/images/brand/dashboard-hero-capy.png", customisation);
        Assert.Contains("capy-companion-summary", customisation);
        Assert.DoesNotContain("capy-customisation-hero", customisation);
        Assert.Contains("capy-category-tabs", customisation);
        Assert.Contains("capy-collection-card", customisation);
        Assert.Contains("capy-item-grid", customisation);
        Assert.Contains("capy-preview-panel", customisation);
        Assert.Contains("capy-loadout-card", customisation);
        Assert.Contains("capy-collection-summary", customisation);
        Assert.Contains("data-category-filter=\"outfits\"", customisation);
        Assert.Contains("data-category-filter=\"face-accessories\"", customisation);
        Assert.Contains("data-category-filter=\"neck-accessories\"", customisation);
        Assert.Contains("data-category-filter=\"titles\"", customisation);
        Assert.Contains("data-category-filter=\"colours\"", customisation);
        Assert.Matches(
            "data-category-filter=\\\"colours\\\"[^>]*>\\s*Expressions\\s*</a>",
            customisation);
        Assert.Contains("id=\"capy-category-title-colours\">Expressions</h3>", customisation);
        Assert.Contains("<dt>Expression</dt>", customisation);
        Assert.DoesNotContain("Colours", customisation);
        Assert.DoesNotContain("<dt>Colour</dt>", customisation);
        Assert.Contains("<strong>Aviators</strong>", faceAccessories);
        foreach (var accessory in new[]
        {
            ("Bandage", "capy-bandaid.png"),
            ("Duck Bill", "capy-duckbill.png"),
            ("Eye Patch", "capy-eyepatch.png"),
            ("Handlebar Moustache", "capy-handlebar-mustache.png"),
            ("Heart Glasses", "capy-heart-glasses.png"),
            ("Heart Sticker", "capy-heart-sticker.png"),
            ("Hypno Glasses", "capy-hypnoglasses.png"),
            ("Pixel Glasses", "capy-pixel-glasses.png")
        })
        {
            Assert.Contains($"<strong>{accessory.Item1}</strong>", faceAccessories);
            Assert.Contains(
                $"/images/capy/face-accessories/{accessory.Item2}",
                faceAccessories);
        }
        Assert.Contains("<strong>Green Scarf</strong>", neckAccessories);
        Assert.Contains("<strong>Green Bow Tie</strong>", neckAccessories);
        foreach (var accessory in new[]
        {
            ("Flower Lei", "capy-flower-lei.png"),
            ("Neck Goggles", "capy-goggles.png"),
            ("Moon Pendant", "capy-moon-pendant.png"),
            ("Ribbon Tie", "capy-ribbon.png"),
            ("Royal Cloak", "capy-royal-cloak.png"),
            ("Star Pendant", "capy-star-pendant.png"),
            ("Sun Pendant", "capy-sun-pendant.png")
        })
        {
            Assert.Contains($"<strong>{accessory.Item1}</strong>", neckAccessories);
            Assert.Contains(
                $"/images/capy/neck-accessories/{accessory.Item2}",
                neckAccessories);
        }
        foreach (var hat in new[]
        {
            ("Cowboy Hat", "Capy-CowboyHat.png"),
            ("Blue &amp; Yellow Party Hat", "PartyHat-BlueYellow.png"),
            ("Gold Crown", "Capy-Crown-Gold.png"),
            ("Orange", "capy-orange.png"),
            ("Rain Hat", "capy-rain-hat.png"),
            ("Frog Hat", "capy-frog-hat.png"),
            ("Witch Hat", "capy-witch-hut.png"),
            ("Wizard Hat", "capy-wizard-hat.png"),
            ("White Lily", "capy-white-lily.png")
        })
        {
            Assert.Contains($"<strong>{hat.Item1}</strong>", hats);
            Assert.Contains($"/images/capy/hats-hair/{hat.Item2}", hats);
        }
        foreach (var background in new[]
        {
            ("Peach", "BG-Peach.png"),
            ("Sage", "BG-Sage.png"),
            ("Powder Blue", "BG-PowderBlue.png"),
            ("Mocha", "BG-Mocha.png")
        })
        {
            Assert.Contains($"<strong>{background.Item1}</strong>", backgrounds);
            Assert.Contains(
                $"/images/capy/backgrounds/{background.Item2}",
                backgrounds);
        }
        Assert.DoesNotContain("capy-item-requirement", customisation);
        Assert.DoesNotContain("capy-item-requirement", faceAccessories);
        Assert.DoesNotContain("capy-item-requirement", neckAccessories);
        Assert.DoesNotContain("capy-item-requirement", hats);
        Assert.DoesNotContain("capy-item-requirement", backgrounds);
        Assert.DoesNotContain("Cool Sunglasses", faceAccessories);
        Assert.DoesNotContain("Green &amp; Red Scarf", customisation);
        Assert.DoesNotContain("Red &amp; White Tie", customisation);
        Assert.Contains("data-capy-loadout=\"FaceAccessory\"", customisation);
        Assert.Contains("data-capy-loadout=\"NeckAccessory\"", customisation);
        Assert.Contains("name=\"__RequestVerificationToken\"", customisation);
        Assert.DoesNotContain("data-category-filter=\"accessories\"", customisation);
        Assert.DoesNotContain("capy-next-unlock-card", customisation);
        Assert.DoesNotContain("capy-progress-bar", customisation);
        Assert.Contains("<details class=\"capy-name-editor\">", customisation);
        Assert.DoesNotContain(
            "<details class=\"capy-name-editor\" open",
            customisation);
        Assert.Contains("&lt;b&gt;Capy&lt;/b&gt;", customisation);
        Assert.DoesNotContain(name, customisation);
        Assert.Contains("&lt;b&gt;Capy&lt;/b&gt;", dashboard);
        Assert.DoesNotContain(name, dashboard);
    }

    [Fact]
    public async Task CustomisationRendersTShirtsForClientSideOutfitPagination()
    {
        const string userId = "render-tshirts-user";
        using var factory = new IntegrationTestFactory();
        await SeedAppearanceAsync(factory, userId, "T-Shirt Capy");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", userId);

        var customisation = await client.GetStringAsync("/Customisation");

        Assert.Contains("id=\"capy-category-outfits\"", customisation);
        Assert.Contains("id=\"capy-category-face-accessories\"", customisation);
        Assert.Contains("id=\"capy-category-neck-accessories\"", customisation);
        Assert.Contains("id=\"capy-category-hats\"", customisation);
        Assert.Contains("id=\"capy-category-backgrounds\"", customisation);
        Assert.Contains("id=\"capy-category-colours\"", customisation);
        Assert.Contains("id=\"capy-category-titles\"", customisation);
        Assert.Contains("data-capy-page-size=\"9\"", customisation);
        Assert.Contains("aria-label=\"Outfits pages\"", customisation);
        Assert.Contains("data-capy-page-previous", customisation);
        Assert.Contains("data-capy-page-next", customisation);
        Assert.Contains(">1 / 2</span>", customisation);
        Assert.DoesNotContain("capy-category-icon", customisation);
        Assert.DoesNotContain(" items</span>", customisation);
        Assert.DoesNotContain(" item</span>", customisation);
        Assert.DoesNotContain("Page 1 of", customisation);
        Assert.DoesNotContain("outfitsPage=", customisation);

        foreach (var name in new[]
        {
            "Brown T-Shirt", "Charcoal T-Shirt", "Comfy Capy T-Shirt",
            "Cream T-Shirt", "Lavender T-Shirt", "Lime T-Shirt",
            "Mustard T-Shirt", "Navy T-Shirt", "Pink T-Shirt",
            "Sage T-Shirt", "Sky Blue T-Shirt", "White T-Shirt", "ZZZ T-Shirt"
        })
        {
            Assert.Contains(name, customisation);
        }
    }

    [Theory]
    [InlineData(5, "Green Scarf")]
    [InlineData(6, "Green Bow Tie")]
    public async Task CustomisationUsesPresentationNamesForEquippedCosmetics(
        int neckAccessoryId,
        string expectedNeckAccessoryName)
    {
        const string userId = "render-cosmetic-name-user";
        using var factory = new IntegrationTestFactory();
        await SeedAppearanceAsync(
            factory,
            userId,
            "Display Name Capy",
            faceAccessoryId: 4,
            neckAccessoryId: neckAccessoryId);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-User", userId);

        var customisation = await client.GetStringAsync("/Customisation");

        Assert.Contains(
            "data-capy-loadout=\"FaceAccessory\">Aviators</dd>",
            customisation);
        Assert.Contains(
            $"data-capy-loadout=\"NeckAccessory\">{expectedNeckAccessoryName}</dd>",
            customisation);
    }

    private static CustomisationModel CreateModel(
        TestDatabase database,
        string userId)
    {
        var model = new CustomisationModel(
            database.Context,
            PageModelTestContext.CreateUserManager(),
            new CapyProvisioningService(database.Context),
            PageModelTestContext.CreateProgressionHooks(database.Context));
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
        string name,
        int? faceAccessoryId = null,
        int? neckAccessoryId = null)
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
            Name = name,
            FaceAccessoryId = faceAccessoryId,
            NeckAccessoryId = neckAccessoryId
        });
        await context.SaveChangesAsync();
    }
}
