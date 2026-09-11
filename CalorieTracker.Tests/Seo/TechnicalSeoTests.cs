using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CalorieTracker.Tests.TestSupport;

namespace CalorieTracker.Tests.Seo;

public class TechnicalSeoTests
{
    [Fact]
    public async Task RobotsTxt_AllowsCrawlingByDefaultAndExcludesPrivateAreas()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/robots.txt");
        var robots = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("User-agent: *", robots);
        Assert.Contains("Allow: /", robots);
        Assert.Contains("Disallow: /api", robots);
        Assert.Contains("Disallow: /Diary", robots);
        Assert.Contains("Disallow: /Foods", robots);
        Assert.Contains("Disallow: /Profile", robots);
        Assert.Contains("Disallow: /Customisation", robots);
        Assert.Contains("Disallow: /Identity/Account/Manage", robots);
        Assert.Contains(
            "Sitemap: https://calories.comfycapy.com/sitemap.xml",
            robots);
        Assert.DoesNotContain(
            "Disallow: /Identity",
            robots.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim()));
    }

    [Fact]
    public async Task SitemapXml_ContainsExactlyApprovedCanonicalUrls()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/sitemap.xml");
        var sitemap = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            response.Content.Headers.ContentType?.MediaType,
            new[] { "application/xml", "text/xml" });

        var document = XDocument.Parse(sitemap);
        XNamespace sitemapNamespace =
            "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urls = document
            .Descendants(sitemapNamespace + "loc")
            .Select(element => element.Value)
            .ToArray();

        Assert.Equal(
        [
            "https://calories.comfycapy.com/",
            "https://calories.comfycapy.com/About",
            "https://calories.comfycapy.com/Help",
            "https://calories.comfycapy.com/Privacy"
        ],
            urls);
        Assert.DoesNotContain("Feedback", sitemap);
        Assert.DoesNotContain("Identity", sitemap);
        Assert.DoesNotContain("/api", sitemap);
        Assert.All(urls, url => Assert.DoesNotContain("?", url));
    }

    [Theory]
    [InlineData(
        "/",
        "Calorie & Macro Tracker · Comfy Capy Calories",
        "Track calories, macros and daily food with a customisable Comfy Capy.",
        "https://calories.comfycapy.com/")]
    [InlineData(
        "/About",
        "About · Comfy Capy Calories",
        "Meet Comfy Capy Calories, a friendly calorie and nutrition tracker built to make food logging easier.",
        "https://calories.comfycapy.com/About")]
    [InlineData(
        "/Help",
        "Help & FAQ · Comfy Capy Calories",
        "Find answers about calorie targets, food logging, portions, saved meals, themes and Comfy Capy customisation.",
        "https://calories.comfycapy.com/Help")]
    [InlineData(
        "/Privacy",
        "Privacy · Comfy Capy Calories",
        "Learn what information Comfy Capy Calories stores, why we use it and the choices you have.",
        "https://calories.comfycapy.com/Privacy")]
    public async Task ApprovedPublicPage_EmitsIndexableBrandedMetadata(
        string route,
        string expectedTitle,
        string expectedDescription,
        string expectedCanonical)
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(route);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedTitle, ReadTitle(html));
        Assert.Equal(expectedDescription, ReadMetaContent(html, "description"));
        Assert.Equal("index,follow", ReadMetaContent(html, "robots"));
        Assert.Equal(expectedCanonical, ReadCanonical(html));
    }

    [Theory]
    [InlineData("/Feedback")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/Register")]
    [InlineData("/Identity/Account/ForgotPassword")]
    [InlineData("/Error")]
    [InlineData("/StatusCode/404")]
    public async Task PublicUtilityPage_DefaultsToNoIndex(string route)
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(route);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("noindex,follow", ReadMetaContent(html, "robots"));
    }

    [Theory]
    [InlineData("/", "https://calories.comfycapy.com/")]
    [InlineData("/Index", "https://calories.comfycapy.com/")]
    [InlineData("/index", "https://calories.comfycapy.com/")]
    [InlineData("/?utm_source=audit", "https://calories.comfycapy.com/")]
    [InlineData("/About/", "https://calories.comfycapy.com/About")]
    [InlineData("/about?ref=audit", "https://calories.comfycapy.com/About")]
    [InlineData("/Help/?section=food", "https://calories.comfycapy.com/Help")]
    [InlineData("/Privacy?source=footer", "https://calories.comfycapy.com/Privacy")]
    public async Task PublicRouteVariants_EmitCleanCanonical(
        string route,
        string expectedCanonical)
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(route);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedCanonical, ReadCanonical(html));
    }

    private static string ReadTitle(string html)
    {
        var match = Regex.Match(html, "<title>(.*?)</title>");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string ReadMetaContent(string html, string name)
    {
        var match = Regex.Match(
            html,
            $"<meta name=\\\"{Regex.Escape(name)}\\\" content=\\\"([^\\\"]*)\\\"",
            RegexOptions.IgnoreCase);
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string ReadCanonical(string html)
    {
        var match = Regex.Match(
            html,
            "<link rel=\\\"canonical\\\" href=\\\"([^\\\"]+)\\\"",
            RegexOptions.IgnoreCase);
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
