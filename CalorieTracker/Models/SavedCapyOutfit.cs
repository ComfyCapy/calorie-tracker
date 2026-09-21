using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Models;

public class SavedCapyOutfit
{
    public const int MaxNameLength = 60;

    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    public int? ExpressionId { get; set; }
    public int? HatHairId { get; set; }
    public int? FaceAccessoryId { get; set; }
    public int? NeckAccessoryId { get; set; }
    public int? ClothesId { get; set; }
    public int? BackgroundId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
