namespace CalorieTracker.Models
{
    public class Food
    {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Calories { get; set; }
            public decimal Protein { get; set; }
            public decimal Carbohydrates { get; set; }
            public decimal Fat { get; set; }
        }
    }
