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
        var request = _httpContextAccessor.HttpContext?.Request;

        return request != null &&
            TryGetExplicitTimeZone(request, out var timeZone)
                ? timeZone
                : TimeZoneInfo.Utc;
    }

    public static bool TryGetExplicitTimeZone(
        HttpRequest request,
        out TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cookieValue = request.Cookies[TimeZoneCookieName];

        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }

        string timeZoneId;

        try
        {
            timeZoneId = Uri.UnescapeDataString(cookieValue);
        }
        catch (UriFormatException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }

        if (string.IsNullOrWhiteSpace(timeZoneId) ||
            timeZoneId.Length > MaximumTimeZoneIdLength)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
