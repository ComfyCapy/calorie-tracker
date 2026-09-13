namespace CalorieTracker.Models;

public sealed class UserXpEvent
{
    public const int MaxEventKeyLength = 128;

    public string UserId { get; set; } = string.Empty;
    public string EventKey { get; set; } = string.Empty;
    public int Amount { get; set; }
    public DateTime AwardedAtUtc { get; set; }
}
