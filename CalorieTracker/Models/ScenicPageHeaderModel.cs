namespace CalorieTracker.Models;

public sealed record ScenicPageHeaderModel(
    string Title,
    string? Eyebrow = null,
    string? ArtworkPath = null,
    string? ArtworkAlt = null,
    bool ArtworkIsDecorative = true,
    string? ArtworkPlaceholderText = null,
    string? CssClass = null,
    bool UseFullBleedArtwork = false);
