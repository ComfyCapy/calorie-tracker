using CalorieTracker;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CalorieTracker.Tests.Configuration;

public sealed class ProductionConfigurationTests
{
    public static IEnumerable<object[]> RequiredSettings() =>
    [
        ["ConnectionStrings:DefaultConnection"],
        ["DataProtection:KeysPath"],
        ["FoodDataCentral:ApiKey"],
        ["Resend:ApiKey"],
        ["Resend:FromAddress"],
        ["Feedback:RecipientAddress"],
        ["AllowedHosts"]
    ];

    [Theory]
    [MemberData(nameof(RequiredSettings))]
    public void MissingRequiredProductionSetting_IsRejected(string key)
    {
        using var directory = new TemporaryDirectory();
        var settings = ValidProductionSettings(directory.Path);
        settings.Remove(key);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfiguration.Validate(
                BuildConfiguration(settings),
                CreateEnvironment(Environments.Production)));

        Assert.Contains(key, exception.Message);
        Assert.DoesNotContain(directory.Path, exception.Message);
    }

    [Fact]
    public void WildcardAllowedHosts_IsRejectedInProduction()
    {
        using var directory = new TemporaryDirectory();
        var settings = ValidProductionSettings(directory.Path);
        settings["AllowedHosts"] = "*";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfiguration.Validate(
                BuildConfiguration(settings),
                CreateEnvironment(Environments.Production)));

        Assert.Contains("AllowedHosts", exception.Message);
    }

    [Fact]
    public void MissingDataProtectionDirectory_IsRejectedWithoutCreatingIt()
    {
        var missingPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "calorietracker-tests",
            Guid.NewGuid().ToString("N"));
        var settings = ValidProductionSettings(missingPath);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfiguration.Validate(
                BuildConfiguration(settings),
                CreateEnvironment(Environments.Production)));

        Assert.Contains("Data Protection key directory", exception.Message);
        Assert.DoesNotContain(missingPath, exception.Message);
        Assert.False(Directory.Exists(missingPath));
    }

    [Fact]
    public void NonProductionEnvironment_DoesNotRequireProductionSettings()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        ProductionConfiguration.Validate(
            configuration,
            CreateEnvironment(Environments.Development));
    }

    [Fact]
    public void DataProtectionKeys_AreReusableAcrossProviderInstances()
    {
        using var directory = new TemporaryDirectory();

        using var firstProvider = BuildDataProtectionProvider(directory.Path);
        var protector = firstProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("production-test");
        var protectedValue = protector.Protect("persistent-value");

        using var secondProvider = BuildDataProtectionProvider(directory.Path);
        var restoredValue = secondProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("production-test")
            .Unprotect(protectedValue);

        Assert.Equal("persistent-value", restoredValue);
        Assert.NotEmpty(Directory.GetFiles(directory.Path, "*.xml"));
    }

    [Fact]
    public void ProductionCookiePolicies_AreSecure_WhileTestingKeepsDefaults()
    {
        var productionServices = new ServiceCollection();
        ProductionConfiguration.ConfigureAntiforgery(
            productionServices,
            CreateEnvironment(Environments.Production));
        productionServices
            .AddAuthentication()
            .AddCookie(IdentityConstants.ApplicationScheme);
        ProductionConfiguration.ConfigureIdentityCookie(
            productionServices,
            CreateEnvironment(Environments.Production));
        using var production = productionServices.BuildServiceProvider();

        var productionAntiforgery = production
            .GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        var productionIdentityCookie = production
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(CookieSecurePolicy.Always, productionAntiforgery.Cookie.SecurePolicy);
        Assert.Equal(CookieSecurePolicy.Always, productionIdentityCookie.Cookie.SecurePolicy);

        var testingServices = new ServiceCollection();
        ProductionConfiguration.ConfigureAntiforgery(
            testingServices,
            CreateEnvironment(Environments.Development));
        testingServices
            .AddAuthentication()
            .AddCookie(IdentityConstants.ApplicationScheme);
        ProductionConfiguration.ConfigureIdentityCookie(
            testingServices,
            CreateEnvironment(Environments.Development));
        using var testing = testingServices.BuildServiceProvider();

        var testingAntiforgery = testing
            .GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        var testingIdentityCookie = testing
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(CookieSecurePolicy.None, testingAntiforgery.Cookie.SecurePolicy);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, testingIdentityCookie.Cookie.SecurePolicy);
    }

    private static ServiceProvider BuildDataProtectionProvider(string path)
    {
        var services = new ServiceCollection();
        ProductionConfiguration.ConfigureDataProtection(
            services,
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = path
            }),
            CreateEnvironment(Environments.Production));
        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfiguration(
        IDictionary<string, string?> settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

    private static Dictionary<string, string?> ValidProductionSettings(string keysPath) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=calorietracker.db",
            ["DataProtection:KeysPath"] = keysPath,
            ["FoodDataCentral:ApiKey"] = "test-food-key",
            ["Resend:ApiKey"] = "test-resend-key",
            ["Resend:FromAddress"] = "Comfy Capy Calories <noreply@example.test>",
            ["Feedback:RecipientAddress"] = "owner@example.test",
            ["AllowedHosts"] = "localhost"
        };

    private static TestHostEnvironment CreateEnvironment(string environmentName) =>
        new() { EnvironmentName = environmentName };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "calorietracker-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "CalorieTracker.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
