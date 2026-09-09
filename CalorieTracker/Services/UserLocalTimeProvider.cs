using Microsoft.AspNetCore.Http;

namespace CalorieTracker.Services;

public interface IUserLocalTimeProvider
{
    DateTimeOffset UtcNow { get; }
    TimeZoneInfo TimeZone { get; }
    DateTimeOffset LocalNow { get; }
    DateOnly Today { get; }
}

public sealed class UserLocalTimeProvider : IUserLocalTimeProvider
{
    public const string TimeZoneCookieName = "ComfyCapy.TimeZone";

    private const int MaximumTimeZoneIdLength = 100;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset? _utcNow;
    private DateTimeOffset? _localNow;
    private TimeZoneInfo? _timeZone;

    public UserLocalTimeProvider(
        IHttpContextAccessor httpContextAccessor,
        TimeProvider timeProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
    }

    public DateTimeOffset UtcNow =>
        _utcNow ??= _timeProvider.GetUtcNow();

    public TimeZoneInfo TimeZone =>
        _timeZone ??= ResolveTimeZone();

    public DateTimeOffset LocalNow =>
        _localNow ??= TimeZoneInfo.ConvertTime(UtcNow, TimeZone);

    public DateOnly Today =>
        DateOnly.FromDateTime(LocalNow.DateTime);

    private TimeZoneInfo ResolveTimeZone()
    {
        var cookieValue = _httpContextAccessor.HttpContext?
            .Request.Cookies[TimeZoneCookieName];

        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            return TimeZoneInfo.Utc;
        }

        string timeZoneId;

        try
        {
            timeZoneId = Uri.UnescapeDataString(cookieValue);
        }
        catch (UriFormatException)
        {
            return TimeZoneInfo.Utc;
        }

        if (string.IsNullOrWhiteSpace(timeZoneId) ||
            timeZoneId.Length > MaximumTimeZoneIdLength)
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
