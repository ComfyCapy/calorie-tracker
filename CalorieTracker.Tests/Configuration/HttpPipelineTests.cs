using System.Net;
using CalorieTracker.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CalorieTracker.Tests.Configuration;

public sealed class HttpPipelineTests
{
    [Fact]
    public async Task ResponsesIncludeSafeBrowserHeaders()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory, "https://localhost");

        using var response = await client.GetAsync("/");

        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "SAMEORIGIN");
        AssertHeader(
            response,
            "Referrer-Policy",
            "strict-origin-when-cross-origin");
        AssertHeader(
            response,
            "Permissions-Policy",
            "camera=(), geolocation=(), microphone=()");
    }

    [Fact]
    public async Task ForwardedHttpsIsAppliedBeforeHstsAndRedirection()
    {
        using var factory = new IntegrationTestFactory();
        using var client = CreateClient(factory, "http://example.test");
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    private static HttpClient CreateClient(
        IntegrationTestFactory factory,
        string baseAddress) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(baseAddress)
        });

    private static void AssertHeader(
        HttpResponseMessage response,
        string name,
        string expectedValue)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values));
        Assert.Equal(expectedValue, Assert.Single(values));
    }
}
