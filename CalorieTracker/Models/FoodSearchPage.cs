namespace CalorieTracker.Models
{
    public class FoodSearchPage
    {
        public List<FoodSearchResult> Foods { get; set; } = [];

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 20;

        public int TotalResults { get; set; }

        public int TotalPages { get; set; }
    }
}
