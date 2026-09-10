using CalorieTracker.Data;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace CalorieTracker.Models;

public sealed class SavedMeal
{
    public const int MaxNameLength = 80;

    [BindNever]
    public int Id { get; set; }

    [BindNever]
    public string UserId { get; set; } = string.Empty;

    [BindNever]
    public ApplicationUser? User { get; set; }

    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [BindNever]
    public List<SavedMealItem> Items { get; set; } = [];
}

public sealed class SavedMealItem
{
    [BindNever]
    public int Id { get; set; }

    [BindNever]
    public int SavedMealId { get; set; }

    [BindNever]
    public SavedMeal? SavedMeal { get; set; }

    [BindNever]
    public int FoodId { get; set; }

    [BindNever]
    public Food? Food { get; set; }

    [BindNever]
    public int? FoodPortionId { get; set; }

    [BindNever]
    public FoodPortion? FoodPortion { get; set; }

    public decimal Quantity { get; set; }
    public decimal? PortionQuantity { get; set; }
    public bool IsApproximate { get; set; }
    public string? ApproximationLabel { get; set; }
    public string FoodNameSnapshot { get; set; } = string.Empty;
    public decimal ServingSizeSnapshot { get; set; }
    public string ServingUnitSnapshot { get; set; } = "g";
    public decimal CanonicalServingSizeSnapshot { get; set; }
    public FoodServingBasis ServingBasisSnapshot { get; set; }
    public string? PortionLabelSnapshot { get; set; }
    public decimal CaloriesSnapshot { get; set; }
    public decimal ProteinSnapshot { get; set; }
    public decimal CarbohydratesSnapshot { get; set; }
    public decimal FatSnapshot { get; set; }
    public string? PortionNameSnapshot { get; set; }
}
