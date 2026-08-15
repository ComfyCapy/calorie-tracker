using System.ComponentModel.DataAnnotations;
using CalorieTracker.Data;

namespace CalorieTracker.Models
{
    public class DiaryEntry
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required]
        public string MealType { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Please select a food.")]
        public int FoodId { get; set; }

        public Food? Food { get; set; }

        [Range(0.01, 100000, ErrorMessage = "Quantity must be between 0.01 and 100,000.")]
        public decimal Quantity { get; set; }

        public decimal CaloriesConsumed
        {
            get
            {
                if (Food == null || Food.ServingSize == 0)
                {
                    return 0;
                }

                return (Quantity / Food.ServingSize) * Food.Calories;
            }
        }

        public decimal ProteinConsumed
        {
            get
            {
                if (Food == null || Food.ServingSize == 0)
                {
                    return 0;
                }

                return (Quantity / Food.ServingSize) * Food.Protein;
            }
        }

        public decimal CarbohydratesConsumed
        {
            get
            {
                if (Food == null || Food.ServingSize == 0)
                {
                    return 0;
                }

                return (Quantity / Food.ServingSize) * Food.Carbohydrates;
            }
        }

        public decimal FatConsumed
        {
            get
            {
                if (Food == null || Food.ServingSize == 0)
                {
                    return 0;
                }

                return (Quantity / Food.ServingSize) * Food.Fat;
            }
        }
    }
}