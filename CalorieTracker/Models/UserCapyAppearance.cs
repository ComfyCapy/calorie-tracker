using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Models
{
    public class UserCapyAppearance
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public int? ExpressionId { get; set; }
        public CapyItem? Expression { get; set; }

        public int? HatHairId { get; set; }
        public CapyItem? HatHair { get; set; }

        public int? FaceAccessoryId { get; set; }
        public CapyItem? FaceAccessory { get; set; }

        public int? NeckAccessoryId { get; set; }
        public CapyItem? NeckAccessory { get; set; }

        public int? ClothesId { get; set; }
        public CapyItem? Clothes { get; set; }

        public int? BackgroundId { get; set; }
        public CapyItem? Background { get; set; }
    }
}