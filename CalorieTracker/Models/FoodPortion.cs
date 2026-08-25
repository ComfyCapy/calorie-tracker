using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Models
{
    public class FoodPortion
    {
        public int Id { get; set; }

        public int FoodId { get; set; }

        public Food? Food { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 100000)]
        public decimal Amount { get; set; }
    }
}