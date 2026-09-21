using CalorieTracker.Data;
using CalorieTracker.Services;
using CalorieTracker;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CalorieTracker.Security;
using Resend;

var builder = WebApplication.CreateBuilder(args);

ProductionConfiguration.Validate(builder.Configuration, builder.Environment);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", AccessRoles.AdminAccess);
    options.Conventions.ConfigureFilter(
        new ServiceFilterAttribute(
            typeof(ProgressionActivityPageFilter)));
    options.Conventions.AuthorizeAreaFolder(
        "Identity",
        "/Account/Manage");
});
builder.Services.AddControllers();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AccessRoles.OwnerAccess, policy => policy.RequireAuthenticatedUser()
        .AddRequirements(new AccessRoleRequirement(AccessLevel.Owner)));
    options.AddPolicy(AccessRoles.AdminAccess, policy => policy.RequireAuthenticatedUser()
        .AddRequirements(new AccessRoleRequirement(AccessLevel.Admin)));
    options.AddPolicy(AccessRoles.BetaAccess, policy => policy.RequireAuthenticatedUser()
        .AddRequirements(new AccessRoleRequirement(AccessLevel.Beta)));
});
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, AccessRoleHandler>();
builder.Services.AddScoped<CommunityFoodService>();
ProductionConfiguration.ConfigureAntiforgery(
    builder.Services,
    builder.Environment);

ProductionConfiguration.ConfigureDataProtection(
    builder.Services,
    builder.Configuration,
    builder.Environment);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    // Only explicitly configured proxies may supply the client address used
    // by anonymous rate limits; arbitrary forwarded headers remain untrusted.
    foreach (var address in builder.Configuration
        .GetSection("ReverseProxy:KnownProxies")
        .Get<string[]>() ?? [])
    {
        options.KnownProxies.Add(IPAddress.Parse(address));
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/plain";
        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.",
            cancellationToken);
    };

    static string RemoteAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    static RateLimitPartition<string> NoLimit() =>
        RateLimitPartition.GetNoLimiter("safe-method");

    static RateLimitPartition<string> FixedWindow(
        string key,
        int permitLimit,
        TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });

    options.AddPolicy(RateLimitPolicies.IdentityOperations, context =>
    {
        if (!HttpMethods.IsPost(context.Request.Method))
            return NoLimit();

        var remoteAddress = RemoteAddress(context);
        var path = context.Request.Path;

        if (path.Equals(
                "/Identity/Account/Register",
                StringComparison.OrdinalIgnoreCase))
        {
            return FixedWindow(
                $"registration:{remoteAddress}",
                5,
                TimeSpan.FromHours(1));
        }

        if (path.Equals(
                "/Identity/Account/Login",
                StringComparison.OrdinalIgnoreCase))
        {
            return FixedWindow(
                $"login:{remoteAddress}",
                30,
                TimeSpan.FromMinutes(5));
        }

        if (path.Equals(
                "/Identity/Account/ForgotPassword",
                StringComparison.OrdinalIgnoreCase))
        {
            return FixedWindow(
                $"forgot-password:{remoteAddress}",
                5,
                TimeSpan.FromMinutes(15));
        }

        if (path.Equals(
                "/Identity/Account/ResendEmailConfirmation",
                StringComparison.OrdinalIgnoreCase))
        {
            return FixedWindow(
                $"resend-confirmation:{remoteAddress}",
                5,
                TimeSpan.FromMinutes(15));
        }

        if (path.Equals(
                "/Feedback",
                StringComparison.OrdinalIgnoreCase))
        {
            return FixedWindow(
                $"feedback:{remoteAddress}",
                5,
                TimeSpan.FromMinutes(15));
        }

        return NoLimit();
    });

    options.AddPolicy(RateLimitPolicies.FoodSearch, context =>
        FixedWindow(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? RemoteAddress(context),
            60,
            TimeSpan.FromMinutes(1)));
});

builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
{
    var connectionString = services
        .GetRequiredService<IConfiguration>()
        .GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Connection string 'DefaultConnection' is not configured.");
    }

    options.UseSqlite(connectionString);
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;


    options.SignIn.RequireConfirmedAccount = true;

    // Account lockout protection.
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

ProductionConfiguration.ConfigureIdentityCookie(
    builder.Services,
    builder.Environment);

builder.Services.AddHttpClient<IFoodSearchService, UsdaFoodService>();
builder.Services.AddTransient<IFoodCatalogueProvider, UsdaFoodCatalogueProvider>();
builder.Services.AddSingleton(_ => new CofidFoodCatalogueProvider());
builder.Services.AddSingleton<IFoodCatalogueProvider>(provider =>
    provider.GetRequiredService<CofidFoodCatalogueProvider>());
builder.Services.AddScoped<FoodCatalogue>();
builder.Services.AddScoped<ExternalFoodResolver>();
builder.Services.AddScoped<CapyProvisioningService>();
builder.Services.AddScoped<CapyWardrobeService>();
builder.Services.AddScoped<DailyMaintenanceSnapshotService>();
builder.Services.AddScoped<DiaryCopyService>();
builder.Services.AddScoped<ReusableMealService>();
builder.Services.AddScoped<CalorieBalanceYearService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<IUserLocalTimeProvider, UserLocalTimeProvider>();
builder.Services.AddSingleton<ProgressionLevelCalculator>();
builder.Services.AddSingleton<ActivityStreakCalculator>();
builder.Services.AddScoped<ProgressionService>();
builder.Services.AddScoped<ProgressionAchievementHooks>();
builder.Services.AddScoped<ProgressionActivityPageFilter>();
builder.Services.AddSingleton<GoalTimelineCalculator>();
builder.Services.AddSingleton<MacroTargetCalculator>();

// Resend email service.
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();

builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken =
        builder.Configuration["Resend:ApiKey"]
        ?? throw new InvalidOperationException(
            "Resend API key is not configured.");
});

builder.Services.AddTransient<IResend, ResendClient>();
builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

// Deliberate operator-only commands; never exposed over HTTP. Apply migrations first.
var grantAdminPosition = Array.IndexOf(args, "--grant-admin");
var grantOwnerPosition = Array.IndexOf(args, "--grant-owner");
if (grantAdminPosition >= 0 || grantOwnerPosition >= 0)
{
    if (grantAdminPosition >= 0 && grantOwnerPosition >= 0)
        throw new ArgumentException("Supply only one operational role grant.");
    var position = Math.Max(grantAdminPosition, grantOwnerPosition);
    if (position + 1 >= args.Length) throw new ArgumentException("Supply an existing confirmed user ID.");
    using var roleScope = app.Services.CreateScope();
    var users = roleScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var role = grantOwnerPosition >= 0 ? AccessRoles.Owner : AccessRoles.Admin;
    if (role == AccessRoles.Owner)
        await AccessRoles.GrantOwnerOperationallyAsync(users, args[position + 1]);
    else
        await AccessRoles.GrantAdminOperationallyAsync(users, args[position + 1]);
    Console.WriteLine($"{role} membership confirmed for the specified account. Web server was not started.");
    return;
}

// Resolve the context once so a missing production connection string fails
// during startup, before the application begins accepting requests.
using (var scope = app.Services.CreateScope())
{
    _ = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    _ = scope.ServiceProvider.GetRequiredService<CofidFoodCatalogueProvider>();
}

// Establish the original scheme and client address before any middleware
// makes HTTPS-sensitive decisions such as applying HSTS or redirecting.
app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] =
        "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] =
        "camera=(), geolocation=(), microphone=()";

    await next();
});

app.UseStatusCodePagesWithReExecute("/StatusCode/{0}");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");

    // The default HSTS value is 30 days.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();

app.MapRazorPages()
    .WithStaticAssets()
    .RequireRateLimiting(RateLimitPolicies.IdentityOperations)
    .Add(endpointBuilder =>
    {
        var page = endpointBuilder.Metadata
            .OfType<PageActionDescriptor>()
            .LastOrDefault();

        if (string.Equals(
                page?.ViewEnginePath,
                "/Foods/ApiFood",
                StringComparison.OrdinalIgnoreCase))
        {
            // ApiFood's GET, handoff and favourite handlers can all resolve USDA
            // data, so its endpoint must override the general page policy.
            endpointBuilder.Metadata.Add(
                new EnableRateLimitingAttribute(
                    RateLimitPolicies.FoodSearch));
        }
    });

app.MapControllers();

app.Run();

public partial class Program;
