using System.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker;

/// <summary>
/// Validates configuration that is required when the application runs in
/// Production. Development and test environments intentionally keep their
/// existing configuration requirements.
/// </summary>
public static class ProductionConfiguration
{
    public const string DataProtectionApplicationName = "ComfyCapyCalories";

    public static void ConfigureDataProtection(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var dataProtection = services.AddDataProtection();

        if (!environment.IsProduction())
            return;

        var keysPath = configuration["DataProtection:KeysPath"];

        if (string.IsNullOrWhiteSpace(keysPath))
        {
            throw new InvalidOperationException(
                "Production Data Protection key directory is required.");
        }

        ValidateDataProtectionDirectory(keysPath);

        dataProtection
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }

    public static void ConfigureAntiforgery(
        IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";

            if (environment.IsProduction())
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
    }

    public static void ConfigureIdentityCookie(
        IServiceCollection services,
        IHostEnvironment environment)
    {
        services.ConfigureApplicationCookie(options =>
        {
            if (environment.IsProduction())
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
    }

    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        Require(
            configuration.GetConnectionString("DefaultConnection"),
            "ConnectionStrings:DefaultConnection");

        var keysPath = Require(
            configuration["DataProtection:KeysPath"],
            "DataProtection:KeysPath");

        ValidateDataProtectionDirectory(keysPath);

        Require(configuration["FoodDataCentral:ApiKey"], "FoodDataCentral:ApiKey");
        Require(configuration["Resend:ApiKey"], "Resend:ApiKey");
        Require(configuration["Resend:FromAddress"], "Resend:FromAddress");
        Require(
            configuration["Feedback:RecipientAddress"],
            "Feedback:RecipientAddress");

        var allowedHosts = configuration["AllowedHosts"];
        Require(allowedHosts, "AllowedHosts");

        var hosts = allowedHosts!
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (hosts.Length == 0 || hosts.Any(host => host == "*"))
        {
            throw new InvalidOperationException(
                "Production configuration 'AllowedHosts' must specify concrete hosts.");
        }
    }

    private static string Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Production configuration '{key}' is required.");
        }

        return value;
    }

    private static void ValidateDataProtectionDirectory(string path)
    {
        var usable = false;

        try
        {
            var directory = new DirectoryInfo(path);

            if (directory.Exists)
            {
                // Enumerating verifies that the configured directory is
                // accessible without creating it or exposing its path.
                using var entries = directory.EnumerateFileSystemInfos().GetEnumerator();
                _ = entries.MoveNext();
                usable = true;
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException
            or SecurityException)
        {
            // Report one safe configuration error below without including a
            // potentially sensitive machine-specific path.
        }

        if (!usable)
        {
            throw new InvalidOperationException(
                "Production Data Protection key directory must already exist and be accessible.");
        }
    }
}
