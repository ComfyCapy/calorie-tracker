using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CalorieTracker.Models
{
    public class UserProfile
    {
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Range(50, 300)]
        public decimal HeightCm { get; set; }

        [Range(20, 500)]
        public decimal WeightKg { get; set; }

        [Required]
        public string CalculationSex { get; set; } = string.Empty;

        [Required]
        public string ActivityLevel { get; set; } = string.Empty;

        [Required]
        public string Goal { get; set; } = string.Empty;

        [Range(20, 500)]
        public decimal? GoalWeightKg { get; set; }

        [Range(0.25, 1.0)]
        public decimal? WeeklyGoalKg { get; set; }

        [NotMapped]
        public int Age
        {
            get
            {
                var today = DateTime.Today;
                var age = today.Year - DateOfBirth.Year;

                if (DateOfBirth.Date > today.AddYears(-age))
                {
                    age--;
                }

                return age;
            }
        }

        [NotMapped]
        public decimal BMI
        {
            get
            {
                if (HeightCm <= 0)
                {
                    return 0;
                }

                var heightMetres = HeightCm / 100;

                return WeightKg / (heightMetres * heightMetres);
            }
        }

        [NotMapped]
        public decimal BMR
        {
            get
            {
                var baseBmr =
                    (10 * WeightKg) +
                    (6.25m * HeightCm) -
                    (5 * Age);

                return CalculationSex switch
                {
                    "Male" => baseBmr + 5,
                    "Female" => baseBmr - 161,
                    _ => 0
                };
            }
        }

        [NotMapped]
        public decimal TDEE
        {
            get
            {
                var activityMultiplier = ActivityLevel switch
                {
                    "Sedentary" => 1.2m,
                    "LightlyActive" => 1.375m,
                    "ModeratelyActive" => 1.55m,
                    "VeryActive" => 1.725m,
                    "ExtraActive" => 1.9m,
                    _ => 0
                };

                return BMR * activityMultiplier;
            }
        }
        [NotMapped]
        public decimal DailyCalorieTarget
        {
            get
            {
                if (TDEE <= 0)
                {
                    return 0;
                }

                if (Goal == "Maintain")
                {
                    return TDEE;
                }

                if (WeeklyGoalKg == null)
                {
                    return TDEE;
                }

                var dailyAdjustment = (WeeklyGoalKg.Value * 7700) / 7;

                return Goal switch
                {
                    "Lose" => TDEE - dailyAdjustment,
                    "Gain" => TDEE + dailyAdjustment,
                    _ => TDEE
                };
            }
        }
    }
}