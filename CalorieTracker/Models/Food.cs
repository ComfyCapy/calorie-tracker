using CalorieTracker.Data;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Models
{
    public class Food
    {
        [BindNever]
        public int Id { get; set; }

        [BindNever]
        public string? UserId { get; set; }

        [BindNever]
        public ApplicationUser? User { get; set; }

        [BindNever]
        public string? Source { get; set; }

        [BindNever]
        public string? ExternalId { get; set; }

        [BindNever]
        public bool IsDeleted { get; set; } = false;

        [BindNever]
        public bool IsFavourite { get; set; } = false;

        [Required(ErrorMessage = "Please enter a food name.")]
        public string Name { get; set; } = string.Empty;

        public int Calories { get; set; }

        public decimal Protein { get; set; }

        public decimal Carbohydrates { get; set; }

        public decimal Fat { get; set; }

        [Display(Name = "Amount")]
        public decimal ServingSize { get; set; } = 100;

        // Nutrition calculations use canonical grams or millilitres;
        // ServingSize/ServingUnit retain the user's preferred display basis.
        [BindNever]
        public decimal CanonicalServingSize { get; set; } = 100;

        [Display(Name = "Unit")]
        public string ServingUnit { get; set; } = "g";

        [BindNever]
        public List<FoodPortion> Portions { get; set; } = [];
    }
}
