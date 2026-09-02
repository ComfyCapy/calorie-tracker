namespace CalorieTracker.Models
{
    public class FoodSearchResult
    {
        public string ExternalId { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public bool IsFavourite { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Calories { get; set; }

        public decimal Protein { get; set; }

        public decimal Carbohydrates { get; set; }

        public decimal Fat { get; set; }

        public decimal ServingSize { get; set; } = 100;

        public string ServingUnit { get; set; } = "g";
    }
}
