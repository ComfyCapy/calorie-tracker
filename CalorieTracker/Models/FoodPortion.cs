using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CalorieTracker.Models
{
    public class FoodPortion
    {
        [BindNever]
        public int Id { get; set; }

        [BindNever]
        public int FoodId { get; set; }

        [BindNever]
        public Food? Food { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        // Portion amounts are stored in the food's canonical grams or millilitres.
        public decimal Amount { get; set; }

        [BindNever]
        public bool IsDeleted { get; set; }
    }
}
