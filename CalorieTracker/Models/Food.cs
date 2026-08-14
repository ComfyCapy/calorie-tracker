using System.ComponentModel.DataAnnotations;
namespace CalorieTracker.Models
{
    public class Food
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Calories { get; set; }
        public decimal Protein { get; set; }
        public decimal Carbohydrates { get; set; }
        public decimal Fat { get; set; }
        [Display(Name = "Serving Size")]
        public decimal ServingSize { get; set; } = 100;
        [Display(Name = "Serving Unit")]
        public string ServingUnit { get; set; } = "g";
    }
}
