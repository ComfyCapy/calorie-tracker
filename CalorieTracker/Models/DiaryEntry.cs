using System.ComponentModel.DataAnnotations;
using CalorieTracker.Data;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CalorieTracker.Models
{
    public class DiaryEntry
    {
        [BindNever]
        public int Id { get; set; }

        [BindNever]
        public string UserId { get; set; } = string.Empty;

        [BindNever]
        public ApplicationUser? User { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public string MealType { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Please select a food.")]
        public int FoodId { get; set; }

        [BindNever]
        public Food? Food { get; set; }

        [BindNever]
        public int? FoodPortionId { get; set; }

        [BindNever]
        public FoodPortion? FoodPortion { get; set; }

        [BindNever]
        public decimal? PortionQuantity { get; set; }

        public decimal Quantity { get; set; }

        // Quantity is canonical grams/millilitres for measured foods and a
        // direct count for portion foods. The immutable serving-basis snapshot
        // preserves which interpretation applies to historical entries.
        public void CaptureSnapshot(Food food, FoodPortion? portion)
        {
            FoodNameSnapshot = food.Name;
            ServingSizeSnapshot = food.ServingSize;
            ServingUnitSnapshot = food.ServingUnit;
            CanonicalServingSizeSnapshot = food.CanonicalServingSize;
            ServingBasisSnapshot = food.ServingBasis;
            PortionLabelSnapshot = food.PortionLabel;
            CaloriesSnapshot = food.Calories;
            ProteinSnapshot = food.Protein;
            CarbohydratesSnapshot = food.Carbohydrates;
            FatSnapshot = food.Fat;
            PortionNameSnapshot = portion?.Name;
        }

        [BindNever]
        public string FoodNameSnapshot { get; set; } = string.Empty;

        [BindNever]
        public decimal ServingSizeSnapshot { get; set; }

        [BindNever]
        public string ServingUnitSnapshot { get; set; } = "g";

        [BindNever]
        public decimal CanonicalServingSizeSnapshot { get; set; }

        [BindNever]
        public FoodServingBasis ServingBasisSnapshot { get; set; } =
            FoodServingBasis.Measured;

        [BindNever]
        [StringLength(Food.MaxPortionLabelLength)]
        public string? PortionLabelSnapshot { get; set; }

        [BindNever]
        public decimal CaloriesSnapshot { get; set; }

        [BindNever]
        public decimal ProteinSnapshot { get; set; }

        [BindNever]
        public decimal CarbohydratesSnapshot { get; set; }

        [BindNever]
        public decimal FatSnapshot { get; set; }

        [BindNever]
        public string? PortionNameSnapshot { get; set; }

        public decimal CaloriesConsumed
        {
            get
            {
                if (CanonicalServingSizeSnapshot <= 0)
                {
                    return 0;
                }

                return (Quantity / CanonicalServingSizeSnapshot) * CaloriesSnapshot;
            }
        }

        public decimal ProteinConsumed
        {
            get
            {
                if (CanonicalServingSizeSnapshot <= 0)
                {
                    return 0;
                }

                return (Quantity / CanonicalServingSizeSnapshot) * ProteinSnapshot;
            }
        }

        public decimal CarbohydratesConsumed
        {
            get
            {
                if (CanonicalServingSizeSnapshot <= 0)
                {
                    return 0;
                }

                return (Quantity / CanonicalServingSizeSnapshot) * CarbohydratesSnapshot;
            }
        }

        public decimal FatConsumed
        {
            get
            {
                if (CanonicalServingSizeSnapshot <= 0)
                {
                    return 0;
                }

                return (Quantity / CanonicalServingSizeSnapshot) * FatSnapshot;
            }
        }
    }
}
