using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CalorieTracker.Services;

public sealed class ProgressionActivityPageFilter : IAsyncPageFilter
{
    private static readonly HashSet<string> ExcludedPages = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "/Privacy",
        "/Help",
        "/Feedback",
        "/Status",
        "/StatusCode",
        "/Error",
        "/Progress",
        "/Foods/ApiFood"
    };

    private readonly ProgressionService _progressionService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProgressionActivityPageFilter> _logger;

    public ProgressionActivityPageFilter(
        ProgressionService progressionService,
        TimeProvider timeProvider,
        ILogger<ProgressionActivityPageFilter> logger)
    {
        _progressionService = progressionService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task OnPageHandlerSelectionAsync(
        PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var activity = TryCreateActivity(context);
        var executedContext = await next();

        if (activity == null ||
            executedContext.Exception != null &&
            !executedContext.ExceptionHandled)
        {
            return;
        }

        var cancellationToken = context.HttpContext.RequestAborted;

        try
        {
            await _progressionService.RecordDailyActivityAsync(
                activity.UserId,
                activity.LocalDate,
                activity.TimeZoneId,
                cancellationToken);
        }
        catch (Exception exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "Optional progression activity failed for user {UserId} " +
                "on {LocalDate} in time zone {TimeZoneId}; the Razor Page " +
                "request will continue.",
                activity.UserId,
                activity.LocalDate,
                activity.TimeZoneId);
        }

        try
        {
            await _progressionService.ReconcileAchievementsAsync(
                activity.UserId,
                cancellationToken);
        }
        catch (Exception exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "Optional progression reconciliation failed for user " +
                "{UserId}; the Razor Page request will continue.",
                activity.UserId);
        }
    }

    private ActivityContext? TryCreateActivity(
        PageHandlerExecutingContext context)
    {
        if (context.ActionDescriptor is not PageActionDescriptor page ||
            !string.IsNullOrEmpty(page.AreaName) ||
            ExcludedPages.Contains(page.ViewEnginePath) ||
            context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = context.HttpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId) ||
            !UserLocalTimeProvider.TryGetExplicitTimeZone(
                context.HttpContext.Request,
                out var timeZone))
        {
            return null;
        }

        var localNow = TimeZoneInfo.ConvertTime(
            _timeProvider.GetUtcNow(),
            timeZone);

        return new ActivityContext(
            userId,
            DateOnly.FromDateTime(localNow.DateTime),
            timeZone.Id);
    }

    private sealed record ActivityContext(
        string UserId,
        DateOnly LocalDate,
        string TimeZoneId);
}
