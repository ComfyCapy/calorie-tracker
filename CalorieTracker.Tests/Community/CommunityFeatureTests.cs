using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Security;
using CalorieTracker.Services;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Community;

public sealed class CommunityFeatureTests
{
    private static ClaimsPrincipal Actor(string id, params string[] forgedRoles) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, id) }.Concat(forgedRoles.Select(x => new Claim(ClaimTypes.Role, x))), "Test"));

    private static async Task Seed(IntegrationTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (id, role) in new[]
                 {
                     ("owner", AccessRoles.Standard),
                     ("other", AccessRoles.Standard),
                     ("beta", AccessRoles.Beta),
                     ("admin", AccessRoles.Admin),
                     ("site-owner", AccessRoles.Owner)
                 })
        {
            var user = new ApplicationUser { Id = id, UserName = id, Email = id + "@example.test", EmailConfirmed = true, FirstName = id };
            Assert.True((await users.CreateAsync(user, "Password1!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
    }

    private static HttpClient Client(IntegrationTestFactory factory, string? id = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        if (id != null) client.DefaultRequestHeaders.Add("X-Test-User", id);
        return client;
    }

    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    private static async Task<HttpResponseMessage> Post(HttpClient client, string tokenPage, string route, Dictionary<string, string>? fields = null)
    {
        var html = await client.GetStringAsync(tokenPage);
        fields ??= [];
        fields["__RequestVerificationToken"] = Token(html);
        return await client.PostAsync(route, new FormUrlEncodedContent(fields));
    }

    [Theory]
    [InlineData("owner", false, false, false)]
    [InlineData("beta", false, false, true)]
    [InlineData("admin", false, true, true)]
    [InlineData("site-owner", true, true, true)]
    [InlineData("missing", false, false, false)]
    public async Task Policies_UseCurrentIdentityMembership_NotForgedClaims(
        string id,
        bool owner,
        bool admin,
        bool beta)
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var actor = Actor(id, "Owner", "Admin", "Beta");
        Assert.Equal(owner, (await auth.AuthorizeAsync(actor, AccessRoles.OwnerAccess)).Succeeded);
        Assert.Equal(admin, (await auth.AuthorizeAsync(actor, AccessRoles.AdminAccess)).Succeeded);
        Assert.Equal(beta, (await auth.AuthorizeAsync(actor, AccessRoles.BetaAccess)).Succeeded);
    }

    [Fact]
    public async Task PoliciesAndAdminUsers_UseNormalizedPreservedRoleNames()
    {
        using var factory = new IntegrationTestFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var adminRole = await db.Roles.SingleAsync(role =>
                role.NormalizedName == AccessRoles.NormalizedAdmin);
            var betaRole = await db.Roles.SingleAsync(role =>
                role.NormalizedName == AccessRoles.NormalizedBeta);
            var ownerRole = await db.Roles.SingleAsync(role =>
                role.NormalizedName == AccessRoles.NormalizedOwner);
            adminRole.Name = "ADMIN";
            betaRole.Name = "bEtA";
            ownerRole.Name = "oWnEr";
            await db.SaveChangesAsync();

            foreach (var (id, role) in new[]
                     {
                         ("preserved-standard", AccessRoles.Standard),
                         ("preserved-beta", AccessRoles.Beta),
                         ("preserved-admin", AccessRoles.Admin),
                         ("preserved-owner", AccessRoles.Owner)
                     })
            {
                var user = new ApplicationUser
                {
                    Id = id,
                    UserName = id,
                    Email = $"{id}@example.test",
                    EmailConfirmed = true,
                    FirstName = id
                };
                Assert.True((await users.CreateAsync(user, "Password1!")).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            }

            var auth = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            Assert.False((await auth.AuthorizeAsync(
                Actor("preserved-standard", "Admin", "Beta"),
                AccessRoles.BetaAccess)).Succeeded);
            Assert.False((await auth.AuthorizeAsync(
                Actor("preserved-beta"),
                AccessRoles.AdminAccess)).Succeeded);
            Assert.True((await auth.AuthorizeAsync(
                Actor("preserved-beta"),
                AccessRoles.BetaAccess)).Succeeded);
            Assert.True((await auth.AuthorizeAsync(
                Actor("preserved-admin"),
                AccessRoles.AdminAccess)).Succeeded);
            Assert.True((await auth.AuthorizeAsync(
                Actor("preserved-admin"),
                AccessRoles.BetaAccess)).Succeeded);
            Assert.True((await auth.AuthorizeAsync(
                Actor("preserved-owner"),
                AccessRoles.OwnerAccess)).Succeeded);
            Assert.True((await auth.AuthorizeAsync(
                Actor("preserved-owner"),
                AccessRoles.AdminAccess)).Succeeded);
            Assert.True((await auth.AuthorizeAsync(
                Actor("preserved-owner"),
                AccessRoles.BetaAccess)).Succeeded);
        }

        using var client = Client(factory, "preserved-admin");
        var adminHtml = await client.GetStringAsync(
            "/Admin/Users?Query=preserved-admin");
        Assert.DoesNotContain("Grant Beta access", adminHtml);
        Assert.DoesNotContain("Remove Beta access", adminHtml);
        var betaHtml = await client.GetStringAsync(
            "/Admin/Users?Query=preserved-beta");
        Assert.Contains("Remove Beta access", betaHtml);
        var ownerHtml = await client.GetStringAsync(
            "/Admin/Users?Query=preserved-owner");
        Assert.Contains("oWnEr", ownerHtml);
        Assert.DoesNotContain("Grant Beta access", ownerHtml);
        Assert.DoesNotContain("Remove Beta access", ownerHtml);

        using var revokeScope = factory.Services.CreateScope();
        var revokeUsers = revokeScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var preservedAdmin = await revokeUsers.FindByIdAsync("preserved-admin");
        Assert.NotNull(preservedAdmin);
        Assert.True((await revokeUsers.RemoveFromRoleAsync(
            preservedAdmin,
            AccessRoles.Admin)).Succeeded);
        var revokeAuth = revokeScope.ServiceProvider
            .GetRequiredService<IAuthorizationService>();
        Assert.False((await revokeAuth.AuthorizeAsync(
            Actor("preserved-admin", "Admin"),
            AccessRoles.AdminAccess)).Succeeded);

        var preservedOwner = await revokeUsers.FindByIdAsync("preserved-owner");
        Assert.NotNull(preservedOwner);
        Assert.True((await revokeUsers.RemoveFromRoleAsync(
            preservedOwner,
            AccessRoles.Owner)).Succeeded);
        Assert.False((await revokeAuth.AuthorizeAsync(
            Actor("preserved-owner", "Owner", "Admin", "Beta"),
            AccessRoles.OwnerAccess)).Succeeded);
        Assert.False((await revokeAuth.AuthorizeAsync(
            Actor("preserved-owner", "Owner", "Admin", "Beta"),
            AccessRoles.AdminAccess)).Succeeded);
        Assert.False((await revokeAuth.AuthorizeAsync(
            Actor("preserved-owner", "Owner", "Admin", "Beta"),
            AccessRoles.BetaAccess)).Succeeded);
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Users")]
    [InlineData("/Admin/CommunityFoods")]
    public async Task AdminRoutes_ProtectEveryGetAndPost(string route)
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        foreach (var id in new[] { "owner", "beta" })
        {
            using var client = Client(factory, id);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, "/Feedback", route + "?handler=Approve", new() { ["id"] = "1", ["Role"] = "Admin" })).StatusCode);
        }
        using var admin = Client(factory, "admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(route)).StatusCode);
        using var owner = Client(factory, "site-owner");
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(route)).StatusCode);
        using var anonymous = Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task Navigation_OnlyAdminSeesAdminLink_AndPagesStayNoindex()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        foreach (var id in new[] { "owner", "beta", "admin", "site-owner" })
        {
            using var client = Client(factory, id);
            var html = await client.GetStringAsync("/Foods");
            Assert.Equal(id is "admin" or "site-owner", html.Contains("href=\"/Admin\""));
            Assert.Contains("content=\"noindex,follow\"", html);
        }
    }

    [Fact]
    public async Task Registration_SeedsExactlyFourRoles_AndIgnoresPrivilegedPostedRole()
    {
        using var factory = new IntegrationTestFactory(); using var client = Client(factory);
        var response = await Post(client, "/Identity/Account/Register", "/Identity/Account/Register", new()
        {
            ["Input.Username"] = "new-user", ["Input.FirstName"] = "New", ["Input.Email"] = "new@example.test",
            ["Input.Password"] = "Password1!", ["Input.ConfirmPassword"] = "Password1!",
            ["Input.Role"] = "Owner", ["Role"] = "Admin"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(new[] { "Admin", "Beta", "Owner", "Standard" }, await db.Roles.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        var user = await users.FindByNameAsync("new-user");
        Assert.Equal(new[] { "Standard" }, await users.GetRolesAsync(user!));
    }

    [Fact]
    public void ExternalRegistrationCreation_AssignsStandardAfterIdentityLoginIsCreated()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "CalorieTracker", "Areas", "Identity", "Pages", "Account", "ExternalLogin.cshtml.cs"));
        var source = File.ReadAllText(path);
        var loginCreated = source.IndexOf("AddLoginAsync(user, info)", StringComparison.Ordinal);
        var standardAssigned = source.IndexOf("AccessRoles.AssignStandardAsync(_userManager, user)", StringComparison.Ordinal);
        Assert.True(loginCreated >= 0 && standardAssigned > loginCreated);
    }

    [Fact]
    public async Task OperationalRoleGrants_RequireConfirmedExistingAccount_AndAreIdempotent()
    {
        using var factory = new IntegrationTestFactory();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unconfirmed = new ApplicationUser
        {
            Id = "unconfirmed", UserName = "unconfirmed", Email = "unconfirmed@example.test",
            EmailConfirmed = false, FirstName = "Unconfirmed"
        };
        Assert.True((await users.CreateAsync(unconfirmed, "Password1!")).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AccessRoles.GrantAdminOperationallyAsync(users, "missing"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AccessRoles.GrantAdminOperationallyAsync(users, unconfirmed.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AccessRoles.GrantOwnerOperationallyAsync(users, "missing"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AccessRoles.GrantOwnerOperationallyAsync(users, unconfirmed.Id));
        unconfirmed.EmailConfirmed = true; Assert.True((await users.UpdateAsync(unconfirmed)).Succeeded);
        await AccessRoles.GrantAdminOperationallyAsync(users, unconfirmed.Id);
        await AccessRoles.GrantAdminOperationallyAsync(users, unconfirmed.Id);
        await AccessRoles.GrantOwnerOperationallyAsync(users, unconfirmed.Id);
        await AccessRoles.GrantOwnerOperationallyAsync(users, unconfirmed.Id);
        Assert.True(await users.IsInRoleAsync(unconfirmed, AccessRoles.Admin));
        Assert.True(await users.IsInRoleAsync(unconfirmed, AccessRoles.Owner));
    }

    [Fact]
    public async Task BetaManagement_IgnoresRoleAndActorTampering_AndRevokesImmediately()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var client = Client(factory, "admin");
        foreach (var grant in new[] { true, true, false, false })
        {
            var response = await Post(client, "/Admin/Users", "/Admin/Users?handler=" + (grant ? "GrantBeta" : "RemoveBeta"),
                new() { ["id"] = "owner", ["Role"] = "Admin", ["UserId"] = "other" });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            Assert.Equal(grant, (await auth.AuthorizeAsync(Actor("owner"), AccessRoles.BetaAccess)).Succeeded);
            Assert.False((await auth.AuthorizeAsync(Actor("owner"), AccessRoles.AdminAccess)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/Admin/Users", "/Admin/Users?handler=RemoveBeta", new() { ["id"] = "admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/Admin/Users", "/Admin/Users?handler=GrantBeta", new() { ["id"] = "site-owner" })).StatusCode);
    }

    [Fact]
    public async Task Owner_CanManageBetaAndAdmin_WithoutPostedRoleEscalation()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var client = Client(factory, "site-owner");

        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Admin/Users",
            "/Admin/Users?handler=GrantBeta", new() { ["id"] = "other" })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var target = await users.FindByIdAsync("other");
            Assert.True(await users.IsInRoleAsync(target!, AccessRoles.Beta));
        }

        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Admin/Users",
            "/Admin/Users?handler=RemoveBeta", new() { ["id"] = "other" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Admin/Users",
            "/Admin/Users?handler=GrantAdmin", new()
            {
                ["id"] = "other",
                ["Role"] = AccessRoles.Owner,
                ["UserId"] = "site-owner"
            })).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var target = await users.FindByIdAsync("other");
            Assert.True(await users.IsInRoleAsync(target!, AccessRoles.Admin));
            Assert.False(await users.IsInRoleAsync(target!, AccessRoles.Owner));
            var auth = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            Assert.True((await auth.AuthorizeAsync(Actor("other"), AccessRoles.AdminAccess)).Succeeded);
            Assert.False((await auth.AuthorizeAsync(Actor("other"), AccessRoles.OwnerAccess)).Succeeded);
        }

        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Admin/Users",
            "/Admin/Users?handler=RemoveAdmin", new() { ["id"] = "other" })).StatusCode);
        using var verify = factory.Services.CreateScope();
        var verifyUsers = verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var verifiedTarget = await verifyUsers.FindByIdAsync("other");
        Assert.False(await verifyUsers.IsInRoleAsync(verifiedTarget!, AccessRoles.Beta));
        Assert.False(await verifyUsers.IsInRoleAsync(verifiedTarget!, AccessRoles.Admin));
        Assert.True(await verifyUsers.IsInRoleAsync(verifiedTarget!, AccessRoles.Standard));
    }

    [Fact]
    public async Task AdminAndForgedPosts_CannotManageAdminOrModifyOwner()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var admin = Client(factory, "admin");

        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, "/Admin/Users",
            "/Admin/Users?handler=GrantBeta", new() { ["id"] = "other" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, "/Admin/Users",
            "/Admin/Users?handler=RemoveBeta", new() { ["id"] = "other" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(admin, "/Admin/Users",
            "/Admin/Users?handler=GrantAdmin", new() { ["id"] = "other", ["Role"] = "Owner" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(admin, "/Admin/Users",
            "/Admin/Users?handler=RemoveAdmin", new() { ["id"] = "admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, "/Admin/Users",
            "/Admin/Users?handler=GrantBeta", new() { ["id"] = "site-owner" })).StatusCode);

        using var owner = Client(factory, "site-owner");
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(owner, "/Admin/Users",
            "/Admin/Users?handler=GrantAdmin", new() { ["id"] = "site-owner" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(owner, "/Admin/Users",
            "/Admin/Users?handler=GrantBeta", new() { ["id"] = "site-owner" })).StatusCode);

        var noToken = await owner.PostAsync("/Admin/Users?handler=GrantAdmin",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "other" }));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        foreach (var handler in new[] { "GrantOwner", "RemoveOwner" })
        {
            var response = await Post(owner, "/Admin/Users", $"/Admin/Users?handler={handler}",
                new() { ["id"] = "other", ["Role"] = AccessRoles.Owner });
            Assert.Contains(response.StatusCode,
                new[] { HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        }

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var other = await users.FindByIdAsync("other");
        var siteOwner = await users.FindByIdAsync("site-owner");
        Assert.False(await users.IsInRoleAsync(other!, AccessRoles.Admin));
        Assert.False(await users.IsInRoleAsync(other!, AccessRoles.Owner));
        Assert.True(await users.IsInRoleAsync(siteOwner!, AccessRoles.Owner));
        Assert.False(await users.IsInRoleAsync(siteOwner!, AccessRoles.Admin));
        Assert.False(await users.IsInRoleAsync(siteOwner!, AccessRoles.Beta));
    }

    [Fact]
    public async Task UsersUi_IdentifiesOwner_WithoutRoleMutationControls()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        foreach (var actor in new[] { "admin", "site-owner" })
        {
            using var client = Client(factory, actor);
            var html = await client.GetStringAsync("/Admin/Users?Query=site-owner");
            Assert.Contains("site-owner@example.test · Owner", html);
            Assert.DoesNotContain("Grant Beta access", html);
            Assert.DoesNotContain("Remove Beta access", html);
            Assert.DoesNotContain("Grant Admin access", html);
            Assert.DoesNotContain("Remove Admin access", html);
        }
    }

    [Fact]
    public async Task AdminCannotSelfDeleteUntilOperationalPrivilegeIsRemoved()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var client = Client(factory, "admin");
        var response = await Post(client, "/Identity/Account/Manage/DeletePersonalData",
            "/Identity/Account/Manage/DeletePersonalData", new() { ["Input.Password"] = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("must arrange removal of Admin access", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync("admin"));
    }

    [Fact]
    public async Task OwnerCannotSelfDeleteUntilOperationalPrivilegeIsRemoved()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var client = Client(factory, "site-owner");
        var response = await Post(client, "/Identity/Account/Manage/DeletePersonalData",
            "/Identity/Account/Manage/DeletePersonalData", new() { ["Input.Password"] = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("must arrange removal of Owner access", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync("site-owner"));
    }

    [Fact]
    public async Task Submission_IsOwnerScoped_ValidatesServerFood_AndCopiesPortionBasis()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
        var food = TestData.Food("owner", name: "Soup");
        food.ServingBasis = FoodServingBasis.Portion; food.ServingSize = 2; food.CanonicalServingSize = 2; food.PortionLabel = "bowls";
        db.Foods.Add(food); await db.SaveChangesAsync();
        Assert.Null(await service.SubmitAsync(Actor("other"), food.Id));
        Assert.Null(await service.SubmitAsync(new ClaimsPrincipal(), food.Id));
        var submitted = await service.SubmitAsync(Actor("owner"), food.Id);
        Assert.NotNull(submitted); Assert.Equal("owner", submitted.SubmitterId);
        Assert.Equal(CommunityFoodStatus.Pending, submitted.Status); Assert.Null(submitted.ReviewedUtc);
        Assert.Equal("bowls", submitted.PortionLabel); Assert.Equal(2, submitted.ServingSize);
        Assert.Equal(submitted.Id, (await service.SubmitAsync(Actor("owner"), food.Id))!.Id);
        food.Name = "Changed"; food.Calories = 999; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var snapshot = await db.CommunityFoods.SingleAsync();
        Assert.Equal("Soup", snapshot.Name); Assert.Equal(200, snapshot.Calories);
        await db.Foods.Where(x => x.Id == food.Id).ExecuteDeleteAsync(); db.ChangeTracker.Clear();
        Assert.Null((await db.CommunityFoods.SingleAsync()).SourceFoodId);
        Assert.Equal("Soup", (await db.CommunityFoods.SingleAsync()).Name);
    }

    [Fact]
    public async Task Submission_RejectsDeletedExternalAndInvalidServerFoods()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
        var deleted = TestData.Food("owner", name: "Deleted"); deleted.IsDeleted = true;
        var external = TestData.Food("owner", name: "External"); external.Source = "USDA"; external.ExternalId = "1";
        var invalid = TestData.Food("owner", name: "Invalid"); invalid.Calories = -1;
        db.AddRange(deleted, external, invalid); await db.SaveChangesAsync();
        Assert.Null(await service.SubmitAsync(Actor("owner"), deleted.Id));
        Assert.Null(await service.SubmitAsync(Actor("owner"), external.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(Actor("owner"), invalid.Id));
        Assert.Empty(await db.CommunityFoods.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Review_FirstDecisionWins_WithAuditMetadata_AndApprovedOnlySearch(bool approve)
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
        var food = TestData.Food("owner", name: "OAT 100%_meal"); db.Foods.Add(food); await db.SaveChangesAsync();
        var submitted = (await service.SubmitAsync(Actor("owner"), food.Id))!;
        Assert.Empty(await service.Search("oat").ToListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReviewAsync(Actor("beta", "Admin"), submitted.Id, true, null));
        Assert.True(await service.ReviewAsync(Actor("admin"), submitted.Id, approve, "Checked"));
        Assert.False(await service.ReviewAsync(Actor("admin"), submitted.Id, !approve, "Overwrite"));
        db.ChangeTracker.Clear();
        var reviewed = await db.CommunityFoods.SingleAsync();
        Assert.Equal(approve ? CommunityFoodStatus.Approved : CommunityFoodStatus.Rejected, reviewed.Status);
        Assert.Equal("admin", reviewed.ReviewerId); Assert.NotNull(reviewed.ReviewedUtc); Assert.Equal("Checked", reviewed.ModeratorNote);
        Assert.Equal(approve ? 1 : 0, await service.Search("oat 100%_").CountAsync());
        Assert.Empty(await service.Search("missing%").ToListAsync());
    }

    [Fact]
    public async Task SnapshotValues_RejectTrackedEditsAfterSubmission()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var food = TestData.Food("owner"); db.Add(food); await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
        var snapshot = (await service.SubmitAsync(Actor("owner"), food.Id))!;
        snapshot.Calories = 12;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SubmitEndpoint_IgnoresForgedNutritionStateAndOwner_AndBlocksIdor()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var food = TestData.Food("owner"); db.Add(food); await db.SaveChangesAsync(); id = food.Id;
        }
        using var owner = Client(factory, "owner"); using var other = Client(factory, "other");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/Foods/Submit/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(other, "/Feedback", $"/Foods/Submit/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/Foods/Submit/{id}", new FormUrlEncodedContent([]))).StatusCode);
        var response = await Post(owner, $"/Foods/Submit/{id}", $"/Foods/Submit/{id}", new()
        { ["SubmitterId"] = "admin", ["Status"] = "Approved", ["Food.Calories"] = "1", ["Name"] = "forged" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        var snapshot = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().CommunityFoods.SingleAsync();
        Assert.Equal("owner", snapshot.SubmitterId); Assert.Equal(200, snapshot.Calories); Assert.Equal(CommunityFoodStatus.Pending, snapshot.Status);
    }

    [Fact]
    public async Task AdminModerationEndpoints_ApproveReject_AndRequireAntiforgery()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var client = Client(factory, "admin");
        foreach (var handler in new[] { "Approve", "Reject" })
        {
            int id;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var food = TestData.Food("owner"); db.Add(food); await db.SaveChangesAsync();
                id = (await scope.ServiceProvider.GetRequiredService<CommunityFoodService>().SubmitAsync(Actor("owner"), food.Id))!.Id;
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Admin/CommunityFoods?handler=" + handler,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = id.ToString() }))).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Admin/CommunityFoods", "/Admin/CommunityFoods?handler=" + handler,
                new() { ["id"] = id.ToString(), ["note"] = "Reviewed", ["ReviewerId"] = "owner" })).StatusCode);
            using var verify = factory.Services.CreateScope();
            var row = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().CommunityFoods.FindAsync(id);
            Assert.Equal(handler == "Approve" ? CommunityFoodStatus.Approved : CommunityFoodStatus.Rejected, row!.Status);
            Assert.Equal("admin", row.ReviewerId);
        }

        int renameId;
        using (var scope = factory.Services.CreateScope())
        {
            renameId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .CommunityFoods.Select(x => x.Id).FirstAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Admin/CommunityFoods?handler=Rename",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = renameId.ToString(), ["name"] = "New" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Admin/CommunityFoods",
            "/Admin/CommunityFoods?handler=Rename", new()
            {
                ["id"] = renameId.ToString(), ["name"] = "  Renamed in Admin  ",
                ["Calories"] = "999", ["Status"] = "Approved", ["SubmitterId"] = "admin"
            })).StatusCode);
        using (var verify = factory.Services.CreateScope())
        {
            var renamed = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().CommunityFoods.FindAsync(renameId);
            Assert.Equal("Renamed in Admin", renamed!.Name); Assert.Equal(200, renamed.Calories);
        }
    }

    [Fact]
    public async Task ApprovedFood_ImportsPerUser_Idempotently_AndHistorySurvivesDeletion()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
        var source = TestData.Food("owner"); db.Add(source); await db.SaveChangesAsync();
        var submission = (await service.SubmitAsync(Actor("owner"), source.Id))!;
        Assert.Null(await service.SelectAsync(Actor("other"), submission.Id));
        await service.ReviewAsync(Actor("admin"), submission.Id, true, null);
        var copy = (await service.SelectAsync(Actor("other"), submission.Id))!;
        Assert.Equal(copy.Id, (await service.SelectAsync(Actor("other"), submission.Id))!.Id);
        Assert.NotEqual(copy.Id, (await service.SelectAsync(Actor("beta"), submission.Id))!.Id);
        Assert.Equal("other", copy.UserId); Assert.Equal("Community", copy.Source); Assert.Equal(200, copy.Calories);
        var diary = TestData.DiaryEntry("other", copy, 100); db.Add(diary); await db.SaveChangesAsync();
        var historical = diary.CaloriesSnapshot;
        source.Calories = 999; await db.SaveChangesAsync();
        await db.Foods.Where(x => x.UserId == "owner").ExecuteDeleteAsync();
        await db.Users.Where(x => x.Id == "owner").ExecuteDeleteAsync(); db.ChangeTracker.Clear();
        var remaining = await db.CommunityFoods.SingleAsync();
        Assert.Null(remaining.SubmitterId); Assert.Null(remaining.SourceFoodId); Assert.Equal(200, remaining.Calories);
        Assert.Equal(historical, (await db.DiaryEntries.SingleAsync()).CaloriesSnapshot);
        Assert.NotNull(await service.SelectAsync(Actor("other"), submission.Id));
    }

    [Fact]
    public async Task CommunityImportAndDiaryPost_UseOnlyPersistedServerNutrition()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        int approvedId, pendingId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var approved = TestData.Food("owner", name: "Authoritative food");
            var pending = TestData.Food("owner", name: "Not approved");
            db.AddRange(approved, pending); await db.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
            approvedId = (await service.SubmitAsync(Actor("owner"), approved.Id))!.Id;
            pendingId = (await service.SubmitAsync(Actor("owner"), pending.Id))!.Id;
            await service.ReviewAsync(Actor("admin"), approvedId, true, null);
        }
        using var client = Client(factory, "other");
        var route = "/Foods?handler=SelectCommunity&source=community";
        Assert.Equal(HttpStatusCode.NotFound, (await Post(client, "/Foods?source=community", route,
            new() { ["id"] = pendingId.ToString() })).StatusCode);
        var selected = await Post(client, "/Foods?source=community", route, new()
        {
            ["id"] = approvedId.ToString(), ["Food.UserId"] = "owner",
            ["Food.Calories"] = "9999", ["Food.Name"] = "Forged"
        });
        Assert.Equal(HttpStatusCode.Redirect, selected.StatusCode);
        var foodId = int.Parse(Regex.Match(selected.Headers.Location!.OriginalString, "foodId=(\\d+)").Groups[1].Value);
        var diary = await Post(client, $"/Diary/Create?foodId={foodId}", "/Diary/Create", new()
        {
            ["DiaryEntry.Date"] = "2026-09-09", ["DiaryEntry.MealType"] = "Lunch",
            ["DiaryEntry.FoodId"] = foodId.ToString(), ["DiaryEntry.Quantity"] = "100",
            ["DiaryEntry.CaloriesSnapshot"] = "9999", ["MeasurementMode"] = "Exact",
            ["ApproximationSize"] = "Medium"
        });
        Assert.Equal(HttpStatusCode.Redirect, diary.StatusCode);
        using var verify = factory.Services.CreateScope();
        var dbVerify = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var privateCopy = await dbVerify.Foods.SingleAsync(x => x.Id == foodId);
        Assert.Equal("other", privateCopy.UserId); Assert.Equal("Authoritative food", privateCopy.Name);
        Assert.Equal(200, privateCopy.Calories);
        var entry = await dbVerify.DiaryEntries.SingleAsync(x => x.UserId == "other");
        Assert.Equal(200, entry.CaloriesSnapshot); Assert.Equal("Authoritative food", entry.FoodNameSnapshot);
    }

    [Fact]
    public async Task DiaryCommunitySearch_IsApprovedOnly_AndSelectionUsesPrivateCopy()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        int approvedId, pendingId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var approved = TestData.Food("owner", name: "Approved diary soup");
            var pending = TestData.Food("owner", name: "Pending diary soup");
            db.AddRange(approved, pending); await db.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
            approvedId = (await service.SubmitAsync(Actor("owner"), approved.Id))!.Id;
            pendingId = (await service.SubmitAsync(Actor("owner"), pending.Id))!.Id;
            await service.ReviewAsync(Actor("admin"), approvedId, true, null);
        }

        using var client = Client(factory, "other");
        const string searchPage = "/Diary/Create?foodSource=community&foodSearchTerm=diary%20soup&date=2026-09-07&meal=Lunch";
        var html = await client.GetStringAsync(searchPage);

        Assert.Contains("Approved diary soup", html);
        Assert.DoesNotContain("Pending diary soup", html);
        Assert.Contains("placeholder=\"Search Community foods...\"", html);
        Assert.Matches("<input[^>]*name=\"date\"[^>]*value=\"2026-09-07\"[^>]*>", html);
        Assert.Matches("<input[^>]*name=\"meal\"[^>]*value=\"Lunch\"[^>]*>", html);

        var handler = "/Diary/Create?handler=SelectCommunity";
        var pendingResponse = await Post(client, searchPage, handler, new()
        {
            ["communityFoodId"] = pendingId.ToString(),
            ["DiaryEntry.Date"] = "2026-09-07",
            ["DiaryEntry.MealType"] = "Lunch",
            ["FoodSearchTerm"] = "diary soup",
            ["Food.UserId"] = "owner",
            ["Food.Calories"] = "9999"
        });
        Assert.Equal(HttpStatusCode.NotFound, pendingResponse.StatusCode);

        var approvedResponse = await Post(client, searchPage, handler, new()
        {
            ["communityFoodId"] = approvedId.ToString(),
            ["DiaryEntry.Date"] = "2026-09-07",
            ["DiaryEntry.MealType"] = "Lunch",
            ["FoodSearchTerm"] = "diary soup",
            ["Food.UserId"] = "owner",
            ["Food.Calories"] = "9999"
        });

        Assert.Equal(HttpStatusCode.Redirect, approvedResponse.StatusCode);
        var location = approvedResponse.Headers.Location!.OriginalString;
        Assert.Contains("date=2026-09-07", location);
        Assert.Contains("meal=Lunch", location);
        Assert.Contains("foodSearchSource=community", location);

        using var verify = factory.Services.CreateScope();
        var privateCopy = await verify.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Foods.SingleAsync(food =>
                food.UserId == "other" &&
                food.Source == "Community" &&
                food.ExternalId == approvedId.ToString());
        Assert.Equal("Approved diary soup", privateCopy.Name);
        Assert.Equal(200, privateCopy.Calories);
    }

    [Fact]
    public async Task AdminRename_ValidatesAuthorization_AndDoesNotMutatePrivateOrHistoricalCopies()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
        var source = TestData.Food("owner", name: "Original private name");
        db.Add(source); await db.SaveChangesAsync();
        var submission = (await service.SubmitAsync(Actor("owner"), source.Id))!;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RenameAsync(Actor("owner"), submission.Id, "Forged"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RenameAsync(Actor("beta", "Admin"), submission.Id, "Forged"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RenameAsync(new ClaimsPrincipal(), submission.Id, "Forged"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RenameAsync(Actor("admin"), submission.Id, " \r\n "));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RenameAsync(Actor("admin"), submission.Id, new string('x', 201)));

        Assert.True(await service.RenameAsync(Actor("admin"), submission.Id, "  Clear pending name  "));
        Assert.True(await service.ReviewAsync(Actor("admin"), submission.Id, true, null));
        var imported = (await service.SelectAsync(Actor("other"), submission.Id))!;
        var diary = TestData.DiaryEntry("other", imported, 100); db.Add(diary); await db.SaveChangesAsync();

        Assert.True(await service.RenameAsync(Actor("admin"), submission.Id, "Approved canonical name"));
        db.ChangeTracker.Clear();
        Assert.Equal("Original private name", (await db.Foods.FindAsync(source.Id))!.Name);
        Assert.Equal("Clear pending name", (await db.Foods.FindAsync(imported.Id))!.Name);
        Assert.Equal("Clear pending name", (await db.DiaryEntries.SingleAsync()).FoodNameSnapshot);
        Assert.Equal(200, (await db.DiaryEntries.SingleAsync()).CaloriesSnapshot);
        Assert.Single(await service.Search("Approved canonical").ToListAsync());
        Assert.Empty(await service.Search("Clear pending").ToListAsync());

        var rejectedSource = TestData.Food("owner", name: "Rejected"); db.Add(rejectedSource); await db.SaveChangesAsync();
        var rejected = (await service.SubmitAsync(Actor("owner"), rejectedSource.Id))!;
        await service.ReviewAsync(Actor("admin"), rejected.Id, false, null);
        Assert.True(await service.RenameAsync(Actor("admin"), rejected.Id, "Renamed rejection"));
        Assert.Empty(await service.Search("Renamed rejection").ToListAsync());
    }

    [Fact]
    public async Task Voting_ImplementsCompleteToggleSwitchStateMachine_AndApprovedOnlyRules()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
        var foods = new[] { "Burger A", "Burger B", "Burger C", "Burger D", "Burger pending", "Burger rejected" }
            .Select(name => TestData.Food("owner", name: name)).ToArray();
        db.AddRange(foods); await db.SaveChangesAsync();
        var rows = new List<CommunityFood>();
        foreach (var food in foods) rows.Add((await service.SubmitAsync(Actor("owner"), food.Id))!);
        foreach (var row in rows.Take(4)) await service.ReviewAsync(Actor("admin"), row.Id, true, null);
        await service.ReviewAsync(Actor("admin"), rows[5].Id, false, null);

        Assert.Equal(1, await service.VoteAsync(Actor("owner"), rows[0].Id, 1));
        Assert.Equal(0, await service.VoteAsync(Actor("owner"), rows[0].Id, 1));
        Assert.Equal(-1, await service.VoteAsync(Actor("owner"), rows[0].Id, -1));
        Assert.Equal(1, await service.VoteAsync(Actor("owner"), rows[0].Id, 1));
        Assert.Equal(-1, await service.VoteAsync(Actor("owner"), rows[0].Id, -1));
        Assert.Equal(0, await service.VoteAsync(Actor("owner"), rows[0].Id, -1));
        Assert.Equal(1, await service.VoteAsync(Actor("owner"), rows[0].Id, 1));
        Assert.Equal(2, await service.VoteAsync(Actor("other", "owner"), rows[0].Id, 1));
        Assert.Equal(-1, await service.VoteAsync(Actor("beta"), rows[2].Id, -1));
        Assert.Equal(1, await service.VoteAsync(Actor("admin"), rows[1].Id, 1));
        Assert.Equal(0, await service.VoteAsync(Actor("admin"), rows[1].Id, 1));

        Assert.Null(await service.VoteAsync(new ClaimsPrincipal(), rows[0].Id, 1));
        Assert.Null(await service.VoteAsync(Actor("owner"), rows[4].Id, 1));
        Assert.Null(await service.VoteAsync(Actor("owner"), rows[5].Id, -1));
        Assert.Null(await service.VoteAsync(Actor("owner"), 999999, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.VoteAsync(Actor("owner"), rows[0].Id, 0));

        // Even forged database rows cannot make non-approved records public.
        db.CommunityFoodVotes.AddRange(
            new CommunityFoodVote { CommunityFoodId = rows[4].Id, UserId = "owner", Value = 1 },
            new CommunityFoodVote { CommunityFoodId = rows[5].Id, UserId = "other", Value = 1 });
        await db.SaveChangesAsync();
        var ranked = await service.Search("Burger", Actor("owner")).ToListAsync();
        Assert.Equal(new[] { rows[0].Id, rows[1].Id, rows[3].Id, rows[2].Id }, ranked.Select(x => x.Food.Id));
        Assert.Equal(new[] { 2, 0, 0, -1 }, ranked.Select(x => x.Score));
        Assert.Equal(1, ranked[0].CurrentUserVote);
        Assert.Equal(0, ranked[1].CurrentUserVote);
        Assert.Equal(5, await db.CommunityFoodVotes.CountAsync());
    }

    [Fact]
    public async Task VoteEndpoint_RequiresAntiforgery_UsesAuthenticatedUser_AndRendersAccessibleState()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        int approvedId, pendingId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var approvedFood = TestData.Food("owner", name: "Vote target");
            var pendingFood = TestData.Food("owner", name: "Private queue item");
            db.AddRange(approvedFood, pendingFood); await db.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
            approvedId = (await service.SubmitAsync(Actor("owner"), approvedFood.Id))!.Id;
            pendingId = (await service.SubmitAsync(Actor("owner"), pendingFood.Id))!.Id;
            await service.ReviewAsync(Actor("admin"), approvedId, true, null);
        }
        using var client = Client(factory, "owner");
        var route = "/Foods?handler=VoteCommunity&source=community";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(route,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = approvedId.ToString(), ["value"] = "1" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "/Foods?source=community", route,
            new() { ["id"] = approvedId.ToString(), ["value"] = "1", ["UserId"] = "other", ["score"] = "999" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(client, "/Foods?source=community", route,
            new() { ["id"] = pendingId.ToString(), ["value"] = "1" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(client, "/Foods?source=community", route,
            new() { ["id"] = "999999", ["value"] = "-1" })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var vote = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().CommunityFoodVotes.SingleAsync();
            Assert.Equal("owner", vote.UserId); Assert.Equal(1, vote.Value);
        }
        var html = await client.GetStringAsync("/Foods?source=community&searchTerm=Vote");
        Assert.Contains("aria-label=\"Upvote Vote target\"", html);
        Assert.Contains("aria-pressed=\"true\"", html);
        Assert.Contains("aria-label=\"Score 1\"", html);
        Assert.DoesNotContain("Private queue item", html);
    }

    [Fact]
    public async Task VoteDatabaseConstraints_EnforceOneValidVotePerUserAndCascadeDeletion()
    {
        using var factory = new IntegrationTestFactory(); await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var food = TestData.Food("owner"); db.Add(food); await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<CommunityFoodService>();
        var row = (await service.SubmitAsync(Actor("owner"), food.Id))!;
        await service.ReviewAsync(Actor("admin"), row.Id, true, null);
        await service.VoteAsync(Actor("other"), row.Id, 1);
        db.ChangeTracker.Clear();
        db.CommunityFoodVotes.Add(new() { CommunityFoodId = row.Id, UserId = "other", Value = -1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"AspNetUsers\" WHERE \"Id\" = 'other'");
        Assert.Empty(await db.CommunityFoodVotes.ToListAsync());
        Assert.NotNull(await db.CommunityFoods.FindAsync(row.Id));
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"CommunityFoods\" WHERE \"Id\" = {0}", row.Id);
        Assert.Empty(await db.CommunityFoodVotes.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentDuplicateVoteRequests_PreserveCompositeKeyUniqueness()
    {
        var path = Path.Combine(Path.GetTempPath(), $"community-vote-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={path};Default Timeout=30").Options;
        try
        {
            int id;
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.Users.Add(new ApplicationUser
                {
                    Id = "voter", UserName = "voter", NormalizedUserName = "VOTER",
                    Email = "voter@example.test", NormalizedEmail = "VOTER@EXAMPLE.TEST",
                    SecurityStamp = Guid.NewGuid().ToString(), FirstName = "Voter"
                });
                var food = new CommunityFood
                {
                    Name = "Concurrent", SubmittedUtc = DateTime.UtcNow,
                    ReviewedUtc = DateTime.UtcNow, Status = CommunityFoodStatus.Approved,
                    Calories = 1, ServingSize = 1, CanonicalServingSize = 1, ServingUnit = "g"
                };
                setup.CommunityFoods.Add(food); await setup.SaveChangesAsync(); id = food.Id;
            }
            await using (var first = new ApplicationDbContext(options))
            await using (var second = new ApplicationDbContext(options))
            {
                var firstService = new CommunityFoodService(first, null!, TimeProvider.System);
                var secondService = new CommunityFoodService(second, null!, TimeProvider.System);
                var results = await Task.WhenAll(
                    firstService.VoteAsync(Actor("voter"), id, 1),
                    secondService.VoteAsync(Actor("voter"), id, 1));
                Assert.All(results, result => Assert.NotNull(result));
                await using var verify = new ApplicationDbContext(options);
                Assert.True(await verify.CommunityFoodVotes.CountAsync() <= 1);
                Assert.All(await verify.CommunityFoodVotes.ToListAsync(), vote => Assert.Equal(1, vote.Value));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
