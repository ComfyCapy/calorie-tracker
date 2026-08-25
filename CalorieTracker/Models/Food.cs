using CalorieTracker.Data;
using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Models
{
    public class Food
    {
        public int Id { get; set; }

        public string? UserId { get; set; }

        public ApplicationUser? User { get; set; }

        public string? Source { get; set; }

        public string? ExternalId { get; set; }

        public bool IsDeleted { get; set; } = false;

        public bool IsFavourite { get; set; } = false;

        public string Name { get; set; } = string.Empty;

        public int Calories { get; set; }

        public decimal Protein { get; set; }

        public decimal Carbohydrates { get; set; }

        public decimal Fat { get; set; }

        [Display(Name = "Amount")]
        public decimal ServingSize { get; set; } = 100;

        [Display(Name = "Unit")]
        public string ServingUnit { get; set; } = "g";

        public List<FoodPortion> Portions { get; set; } = [];
    }
}