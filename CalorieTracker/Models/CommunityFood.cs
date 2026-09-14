using System.ComponentModel.DataAnnotations;
using CalorieTracker.Data;

namespace CalorieTracker.Models;

public enum CommunityFoodStatus { Pending = 0, Approved = 1, Rejected = 2 }

// This snapshot is also the catalogue record once approved. No public editable Food row.
public sealed class CommunityFood
{
    public int Id { get; set; }
    public int? SourceFoodId { get; set; }
    public string? SubmitterId { get; set; }
    public string? ReviewerId { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public DateTime? ReviewedUtc { get; set; }
    public CommunityFoodStatus Status { get; set; }
    [MaxLength(500)] public string? ModeratorNote { get; set; }
    [MaxLength(200)] public string Name { get; set; } = "";
    public int Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbohydrates { get; set; }
    public decimal Fat { get; set; }
    public decimal ServingSize { get; set; }
    public decimal CanonicalServingSize { get; set; }
    [MaxLength(20)] public string ServingUnit { get; set; } = "g";
    public FoodServingBasis ServingBasis { get; set; }
    [MaxLength(Food.MaxPortionLabelLength)] public string? PortionLabel { get; set; }
    public string DisplayServingUnit => ServingBasis == FoodServingBasis.Portion ? PortionLabel! : ServingUnit;
    public List<CommunityFoodVote> Votes { get; set; } = [];

    public Food ToPrivateFood(string userId) => new()
    {
        UserId = userId, Source = "Community", ExternalId = Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Name = Name, Calories = Calories, Protein = Protein, Carbohydrates = Carbohydrates,
        Fat = Fat, ServingSize = ServingSize, CanonicalServingSize = CanonicalServingSize,
        ServingUnit = ServingUnit, ServingBasis = ServingBasis, PortionLabel = PortionLabel
    };
}

public sealed class CommunityFoodVote
{
    public int CommunityFoodId { get; set; }
    public CommunityFood CommunityFood { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int Value { get; set; }
}

public sealed record CommunityFoodSearchResult(
    CommunityFood Food,
    int Score,
    int CurrentUserVote);
