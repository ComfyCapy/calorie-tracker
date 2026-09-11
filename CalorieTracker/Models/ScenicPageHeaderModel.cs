namespace CalorieTracker.Models;

public sealed record ScenicPageHeaderModel(
    string Title,
    string? Subtitle = null,
    string? Eyebrow = null,
    string? ArtworkPath = null,
    string? ArtworkAlt = null,
    bool ArtworkIsDecorative = true,
    string? ArtworkPlaceholderText = null,
    string? CssClass = null);
