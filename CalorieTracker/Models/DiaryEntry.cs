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
        public DateTime Date { get; set; } = DateTime.Today;

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

        [BindNever]
        public string FoodNameSnapshot { get; set; } = string.Empty;

        [BindNever]
        public decimal ServingSizeSnapshot { get; set; }

        [BindNever]
        public string ServingUnitSnapshot { get; set; } = "g";

        [BindNever]
        public decimal CanonicalServingSizeSnapshot { get; set; }

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
