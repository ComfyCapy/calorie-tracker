using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CalorieTracker.Models
{
    public class FoodPortion
    {
        public const int MaxNameLength = 50;

        [BindNever]
        public int Id { get; set; }

        [BindNever]
        public int FoodId { get; set; }

        [BindNever]
        public Food? Food { get; set; }

        [Required]
        [StringLength(MaxNameLength)]
        public string Name { get; set; } = string.Empty;

        // Portion amounts are stored in the food's canonical grams or millilitres.
        public decimal Amount { get; set; }

        [BindNever]
        public bool IsDeleted { get; set; }
    }
}
