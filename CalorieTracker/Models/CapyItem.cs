using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Models
{
    public class CapyItem
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public string ImagePath { get; set; } = string.Empty;

        public bool IsDefault { get; set; }
        public bool IsStarter { get; set; }

        public bool IsActive { get; set; } = true;
    }
}