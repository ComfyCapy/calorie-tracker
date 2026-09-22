using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CalorieTracker.Tests.TestSupport;

namespace CalorieTracker.Tests.Theme;

public sealed class StylesheetOrderTests
{
    [Fact]
    public async Task OrderedStylesheets_MatchReviewedDeclarationBaseline()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        string[] paths = ["/css/site.css", "/css/shell-scenic.css", "/css/dashboard-profile.css"];
        var previous = html.IndexOf("/lib/bootstrap/dist/css/bootstrap.min.", StringComparison.Ordinal);
        Assert.True(previous >= 0);
        var styles = new List<string>();
        foreach (var path in paths)
        {
            var link = Regex.Match(html, Regex.Escape(path[..^4]) + @"(?:\.[a-z0-9]+)?\.css(?:\?v=[^"" ]+)?");
            Assert.True(link.Success && link.Index > previous);
            previous = link.Index;
            styles.Add((await client.GetStringAsync(link.Value)).Replace("\r", "").TrimEnd());
        }
        Assert.True(html.IndexOf("/CalorieTracker.", StringComparison.Ordinal) > previous);
        // Golden declaration stream for the reviewed stylesheet order. Update only
        // for intentional style changes, never to conceal cascade reordering.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n\n", styles))));
        Assert.Equal("198F513B437350FF835C874AE7FF6C7FB4A46375F2CA95D62CE0C5D0B04325D3", hash);
    }
}
