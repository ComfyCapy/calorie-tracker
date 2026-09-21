using System.Net;
using System.Text.RegularExpressions;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.Tests.Calculations;

public sealed class ProfileHttpBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_UsesCurrentUserAndPreservesExistingTheme(bool existing)
    {
        using var factory = new IntegrationTestFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new ApplicationUser { Id = "profile-http", UserName = "profile-http", SecurityStamp = "test" });
            if (existing) db.UserProfiles.Add(new UserProfile { UserId = "profile-http", ThemePreference = "Dark", DateOfBirth = new DateTime(1990, 1, 1) });
            await db.SaveChangesAsync();
        }
        using var client = Client(factory);
        var fields = Fields();
        fields["UserProfile.Id"] = "99999";
        fields["UserProfile.UserId"] = "someone-else";
        fields["UserProfile.User.Id"] = "someone-else";
        fields["UserProfile.ThemePreference"] = "Light";
        var response = await Post(client, fields);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, string.Join("; ", Regex.Matches(await response.Content.ReadAsStringAsync(), "<span[^>]*field-validation-error[^>]*>(.*?)</span>").Select(m => m.Value)));
        using var savedScope = factory.Services.CreateScope();
        var saved = await savedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserProfiles.SingleAsync();
        Assert.Equal("profile-http", saved.UserId);
        Assert.NotEqual(99999, saved.Id);
        Assert.Equal(existing ? "Dark" : "Light", saved.ThemePreference);
        Assert.Equal(180, saved.HeightCm);
        Assert.Equal(80, saved.WeightKg);
    }

    [Theory]
    [InlineData("UserProfile.HeightCm", "49", "Please enter a height between 50 cm and 300 cm.")]
    [InlineData("UserProfile.WeightKg", "501", "Please enter a weight between 20 kg and 500 kg.")]
    [InlineData("UserProfile.DateOfBirth", "", "Please enter your date of birth.")]
    public async Task Post_RendersOriginalValidationKeysAndMessages(string field, string value, string message)
    {
        using var factory = new IntegrationTestFactory();
        using var client = Client(factory);
        var fields = Fields();
        fields[field] = value;
        var response = await Post(client, fields);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains($"data-valmsg-for=\"{field}\"", html);
        Assert.Contains(message, html);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserProfiles.ToListAsync());
    }

    private static HttpClient Client(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Test-User", "profile-http");
        return client;
    }

    private static Dictionary<string, string> Fields() => new()
    {
        ["UserProfile.MeasurementSystem"] = "Metric",
        ["UserProfile.DateOfBirth"] = "1990-01-01",
        ["UserProfile.HeightCm"] = "180", ["UserProfile.WeightKg"] = "80",
        ["UserProfile.CalculationSex"] = "Male", ["UserProfile.ActivityLevel"] = "Sedentary",
        ["UserProfile.Goal"] = "Maintain"
    };

    private static async Task<HttpResponseMessage> Post(HttpClient client, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync("/Profile");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return await client.PostAsync("/Profile", new FormUrlEncodedContent(fields));
    }
}
