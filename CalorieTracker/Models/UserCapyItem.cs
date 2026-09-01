using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Models
{
    public class UserCapyItem
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public int CapyItemId { get; set; }

        public CapyItem CapyItem { get; set; } = null!;

        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    }
}