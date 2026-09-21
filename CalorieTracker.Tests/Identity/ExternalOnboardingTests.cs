using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CalorieTracker.Tests.Identity;

public sealed class ExternalOnboardingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmation_OnboardsOnlyAfterSuccessfulLoginLink(bool loginAlreadyUsed)
    {
        using var factory = new IntegrationTestFactory();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (loginAlreadyUsed)
        {
            var existing = new ApplicationUser { UserName = "existing", Email = "existing@example.test" };
            Assert.True((await users.CreateAsync(existing)).Succeeded);
            Assert.True((await users.AddLoginAsync(existing, new UserLoginInfo("TestProvider", "key", "Test"))).Succeeded);
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ExternalScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "key"), new Claim(ClaimTypes.Email, "external@example.test")], "TestProvider"));
        var properties = new AuthenticationProperties();
        properties.Items["LoginProvider"] = "TestProvider";
        var ticket = new AuthenticationTicket(principal, properties, IdentityConstants.ExternalScheme);
        client.DefaultRequestHeaders.Add("Cookie", $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}");
        var html = await client.GetStringAsync("/Identity/Account/Register");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var response = await client.PostAsync("/Identity/Account/ExternalLogin?handler=Confirmation", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "external@example.test", ["Input.Role"] = "Owner",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        var user = await users.FindByEmailAsync("external@example.test");
        Assert.NotNull(user);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (loginAlreadyUsed)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await users.GetRolesAsync(user));
            Assert.False(await db.UserCapyAppearances.AnyAsync(x => x.UserId == user.Id));
            Assert.Empty(factory.EmailSender.SentEmails);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(new[] { "Standard" }, await users.GetRolesAsync(user));
            Assert.Single(await users.GetLoginsAsync(user));
            Assert.True(await db.UserCapyAppearances.AnyAsync(x => x.UserId == user.Id));
            Assert.Equal(await db.CapyItems.CountAsync(x => x.IsActive && x.IsStarter),
                await db.UserCapyItems.CountAsync(x => x.UserId == user.Id));
            var email = Assert.Single(factory.EmailSender.SentEmails);
            var url = WebUtility.HtmlDecode(Regex.Match(email.HtmlBody, "href=\"([^\"]+)\"").Groups[1].Value);
            var query = QueryHelpers.ParseQuery(new Uri(url).Query);
            var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(query["code"]!));
            Assert.True((await users.ConfirmEmailAsync(user, code)).Succeeded);
        }
    }
}
