namespace CalorieTracker.Models
{
    public class DiaryEntry
    {
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;
        public string MealType { get; set; } = string.Empty;
        public int FoodId { get; set; }
        public Food? Food { get; set; }
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