using CalorieTracker.Data;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Models
{
    public enum FoodServingBasis
    {
        Measured = 0,
        Portion = 1
    }

    public class Food
    {
        public const int MaxPortionLabelLength = 40;

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

        [Display(Name = "Serving basis")]
        public FoodServingBasis ServingBasis { get; set; } =
            FoodServingBasis.Measured;

        [Display(Name = "Portion name")]
        public string? PortionLabel { get; set; }

        // Measured foods store canonical grams or millilitres. Direct-portion
        // foods store the portion amount itself because no measured equivalent
        // is required or inferred.
        [BindNever]
        public decimal CanonicalServingSize { get; set; } = 100;

        [Display(Name = "Unit")]
        public string ServingUnit { get; set; } = "g";

        [BindNever]
        public List<FoodPortion> Portions { get; set; } = [];

        public string DisplayServingUnit =>
            ServingBasis == FoodServingBasis.Portion
                ? PortionLabel ?? "portion"
                : ServingUnit;
    }
}
