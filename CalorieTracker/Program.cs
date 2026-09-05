using CalorieTracker.Data;
using CalorieTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CalorieTracker.Security;
using Resend;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeAreaFolder(
        "Identity",
        "/Account/Manage");
});
builder.Services.AddControllers();
builder.Services.AddAntiforgery(options =>
    options.HeaderName = "X-CSRF-TOKEN");

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

        return NoLimit();
    });

    options.AddPolicy(RateLimitPolicies.FoodSearch, context =>
        FixedWindow(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? RemoteAddress(context),
            60,
            TimeSpan.FromMinutes(1)));
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));

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

builder.Services.AddHttpClient<IFoodSearchService, UsdaFoodService>();
builder.Services.AddScoped<ExternalFoodResolver>();
builder.Services.AddScoped<CapyProvisioningService>();

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

app.UseStatusCodePagesWithReExecute("/StatusCode/{0}");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");

    // The default HSTS value is 30 days.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();

app.MapRazorPages()
    .WithStaticAssets()
    .RequireRateLimiting(RateLimitPolicies.IdentityOperations);

app.MapControllers();

app.Run();
