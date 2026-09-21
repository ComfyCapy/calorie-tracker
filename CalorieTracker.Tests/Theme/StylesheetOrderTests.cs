using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CalorieTracker.Tests.TestSupport;

namespace CalorieTracker.Tests.Theme;

public sealed class StylesheetOrderTests
{
    [Fact]
    public async Task OrderedStylesheets_PreservePreSplitDeclarationsExactly()
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
        // Golden declaration stream from the pre-split stylesheet. Update only for
        // intentional future style changes, never to conceal cascade reordering.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n\n", styles))));
        Assert.Equal("19F36FA12F15ADF52CB7B5402047E7FC46283CBFDAF3E738AA58DB8214687BCB", hash);
    }
}
