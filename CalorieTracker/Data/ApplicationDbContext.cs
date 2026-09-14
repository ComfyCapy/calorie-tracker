using Microsoft.EntityFrameworkCore;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using CalorieTracker.Services;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CalorieTracker.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Food> Foods { get; set; }
        public DbSet<CommunityFood> CommunityFoods { get; set; }
        public DbSet<CommunityFoodVote> CommunityFoodVotes { get; set; }
        public DbSet<DiaryEntry> DiaryEntries { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<DailyMaintenanceSnapshot> DailyMaintenanceSnapshots { get; set; }
        public DbSet<FoodPortion> FoodPortions { get; set; }
        public DbSet<SavedMeal> SavedMeals { get; set; }
        public DbSet<SavedMealItem> SavedMealItems { get; set; }

        public DbSet<CapyItem> CapyItems { get; set; }
        public DbSet<UserCapyItem> UserCapyItems { get; set; }
        public DbSet<UserCapyAppearance> UserCapyAppearances { get; set; }
        public DbSet<UserDailyActivity> UserDailyActivities { get; set; }
        public DbSet<UserAchievement> UserAchievements { get; set; }
        public DbSet<UserXpEvent> UserXpEvents { get; set; }
        public DbSet<UserProgressionState> UserProgressionStates { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>()
                .HasData(CalorieTracker.Security.AccessRoles.SeedRoles());
            var community = builder.Entity<CommunityFood>();
            community.HasOne<Food>().WithMany().HasForeignKey(x => x.SourceFoodId).OnDelete(DeleteBehavior.SetNull);
            community.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SubmitterId).OnDelete(DeleteBehavior.SetNull);
            community.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.SetNull);
            community.HasIndex(x => x.SourceFoodId).IsUnique().HasFilter("\"SourceFoodId\" IS NOT NULL");
            community.HasIndex(x => new { x.Status, x.Name, x.Id });
            community.Property(x => x.Status).IsConcurrencyToken();
            var communityVote = builder.Entity<CommunityFoodVote>();
            communityVote.HasKey(x => new { x.CommunityFoodId, x.UserId });
            communityVote.HasOne(x => x.CommunityFood).WithMany(x => x.Votes)
                .HasForeignKey(x => x.CommunityFoodId).OnDelete(DeleteBehavior.Cascade);
            communityVote.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            communityVote.ToTable("CommunityFoodVotes", table =>
                table.HasCheckConstraint("CK_CommunityFoodVotes_Value", "\"Value\" IN (-1, 1)"));
            community.ToTable("CommunityFoods", table =>
            {
                table.HasCheckConstraint("CK_CommunityFoods_Status", "\"Status\" IN (0, 1, 2)");
                table.HasCheckConstraint("CK_CommunityFoods_Review", "(\"Status\" = 0 AND \"ReviewedUtc\" IS NULL) OR (\"Status\" IN (1, 2) AND \"ReviewedUtc\" IS NOT NULL)");
                table.HasCheckConstraint("CK_CommunityFoods_Nutrition", "\"Calories\" >= 0 AND CAST(\"Protein\" AS REAL) >= 0 AND CAST(\"Carbohydrates\" AS REAL) >= 0 AND CAST(\"Fat\" AS REAL) >= 0 AND CAST(\"ServingSize\" AS REAL) > 0 AND CAST(\"CanonicalServingSize\" AS REAL) > 0");
                table.HasCheckConstraint("CK_CommunityFoods_Name", "length(trim(\"Name\")) BETWEEN 1 AND 200");
                table.HasCheckConstraint("CK_CommunityFoods_Basis", "\"ServingBasis\" IN (0, 1)");
            });
            foreach (var name in new[] { nameof(CommunityFood.Calories), nameof(CommunityFood.Protein), nameof(CommunityFood.Carbohydrates), nameof(CommunityFood.Fat), nameof(CommunityFood.ServingSize), nameof(CommunityFood.CanonicalServingSize), nameof(CommunityFood.ServingUnit), nameof(CommunityFood.ServingBasis), nameof(CommunityFood.PortionLabel), nameof(CommunityFood.SubmittedUtc) })
                community.Property(name).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            builder.Entity<UserProfile>()
                .HasOne(profile => profile.User)
                .WithOne(user => user.UserProfile)
                .HasForeignKey<UserProfile>(
                    profile => profile.UserId);

            builder.Entity<DailyMaintenanceSnapshot>()
                .HasKey(snapshot => new
                {
                    snapshot.UserId,
                    snapshot.Date
                });

            builder.Entity<DailyMaintenanceSnapshot>()
                .HasOne(snapshot => snapshot.User)
                .WithMany()
                .HasForeignKey(snapshot => snapshot.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DailyMaintenanceSnapshot>()
                .Property(snapshot => snapshot.MaintenanceCalories)
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            builder.Entity<UserDailyActivity>()
                .HasKey(activity => new
                {
                    activity.UserId,
                    activity.LocalDate
                });

            builder.Entity<UserDailyActivity>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(activity => activity.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserDailyActivity>()
                .Property(activity => activity.TimeZoneId)
                .IsRequired()
                .HasMaxLength(UserDailyActivity.MaxTimeZoneIdLength);

            builder.Entity<UserAchievement>()
                .HasKey(achievement => new
                {
                    achievement.UserId,
                    achievement.AchievementKey
                });

            builder.Entity<UserAchievement>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(achievement => achievement.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserAchievement>()
                .Property(achievement => achievement.AchievementKey)
                .IsRequired()
                .HasMaxLength(UserAchievement.MaxAchievementKeyLength);

            builder.Entity<UserXpEvent>()
                .HasKey(xpEvent => new
                {
                    xpEvent.UserId,
                    xpEvent.EventKey
                });

            builder.Entity<UserXpEvent>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(xpEvent => xpEvent.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserXpEvent>()
                .Property(xpEvent => xpEvent.EventKey)
                .IsRequired()
                .HasMaxLength(UserXpEvent.MaxEventKeyLength);

            builder.Entity<UserXpEvent>()
                .ToTable(
                    "UserXpEvents",
                    table => table.HasCheckConstraint(
                        "CK_UserXpEvents_Amount_Positive",
                        "\"Amount\" > 0"));

            builder.Entity<UserProgressionState>()
                .HasKey(state => state.UserId);

            builder.Entity<UserProgressionState>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(state => state.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserProgressionState>()
                .Property(state => state.AchievementBackfillVersion)
                .HasDefaultValue(0);

            builder.Entity<UserProgressionState>()
                .ToTable(
                    "UserProgressionStates",
                    table => table.HasCheckConstraint(
                        "CK_UserProgressionStates_BackfillVersion_NonNegative",
                        "\"AchievementBackfillVersion\" >= 0"));

            // Restrict physical deletes for food/portion references so historical diary
            // foreign keys remain resolvable; the UI soft-deletes instead.
            builder.Entity<FoodPortion>()
                .HasOne(portion => portion.Food)
                .WithMany(food => food.Portions)
                .HasForeignKey(portion => portion.FoodId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DiaryEntry>()
                .HasOne(entry => entry.Food)
                .WithMany()
                .HasForeignKey(entry => entry.FoodId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DiaryEntry>()
                .HasOne(entry => entry.FoodPortion)
                .WithMany()
                .HasForeignKey(entry => entry.FoodPortionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DiaryEntry>()
                .HasIndex(entry => new
                {
                    entry.UserId,
                    entry.Date,
                    entry.Id
                });

            builder.Entity<DiaryEntry>()
                .HasIndex(entry => new
                {
                    entry.UserId,
                    entry.FoodId
                });

            builder.Entity<SavedMeal>()
                .HasOne(meal => meal.User)
                .WithMany()
                .HasForeignKey(meal => meal.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SavedMeal>()
                .HasIndex(meal => new { meal.UserId, meal.Name });

            builder.Entity<SavedMealItem>()
                .HasOne(item => item.SavedMeal)
                .WithMany(meal => meal.Items)
                .HasForeignKey(item => item.SavedMealId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SavedMealItem>()
                .HasOne(item => item.Food)
                .WithMany()
                .HasForeignKey(item => item.FoodId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SavedMealItem>()
                .HasOne(item => item.FoodPortion)
                .WithMany()
                .HasForeignKey(item => item.FoodPortionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<SavedMealItem>()
                .Property(item => item.ApproximationLabel)
                .HasMaxLength(20);

            builder.Entity<Food>()
                .HasIndex(food => new
                {
                    food.UserId,
                    food.Source,
                    food.ExternalId
                })
                .IsUnique()
                .HasFilter(
                    "\"UserId\" IS NOT NULL AND " +
                    "\"Source\" IS NOT NULL AND " +
                    "\"ExternalId\" IS NOT NULL");

            builder.Entity<Food>()
                .Property(food => food.PortionLabel)
                .HasMaxLength(Food.MaxPortionLabelLength);

            // A user should only own each Capy item once.
            builder.Entity<UserCapyItem>()
                .HasIndex(userItem => new
                {
                    userItem.UserId,
                    userItem.CapyItemId
                })
                .IsUnique();

            builder.Entity<UserCapyItem>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(userItem => userItem.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserCapyItem>()
                .HasOne(userItem => userItem.CapyItem)
                .WithMany()
                .HasForeignKey(userItem => userItem.CapyItemId)
                .OnDelete(DeleteBehavior.Restrict);


            // Each user has one equipped Capy appearance.
            builder.Entity<UserCapyAppearance>()
                .HasIndex(appearance => appearance.UserId)
                .IsUnique();

            builder.Entity<UserCapyAppearance>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(appearance => appearance.UserId)
                .OnDelete(DeleteBehavior.Cascade);


            // Equipped item slots.
            builder.Entity<UserCapyAppearance>()
                .HasOne(appearance => appearance.Expression)
                .WithMany()
                .HasForeignKey(appearance => appearance.ExpressionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserCapyAppearance>()
                .HasOne(appearance => appearance.HatHair)
                .WithMany()
                .HasForeignKey(appearance => appearance.HatHairId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserCapyAppearance>()
                .HasOne(appearance => appearance.FaceAccessory)
                .WithMany()
                .HasForeignKey(appearance => appearance.FaceAccessoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserCapyAppearance>()
                .HasOne(appearance => appearance.NeckAccessory)
                .WithMany()
                .HasForeignKey(appearance => appearance.NeckAccessoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserCapyAppearance>()
                .HasOne(appearance => appearance.Clothes)
                .WithMany()
                .HasForeignKey(appearance => appearance.ClothesId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserCapyAppearance>()
                .HasOne(appearance => appearance.Background)
                .WithMany()
                .HasForeignKey(appearance => appearance.BackgroundId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CapyItem>().HasData(
    // Expressions
    new CapyItem
    {
        Id = 1,
        Name = "Base Capy",
        Category = CapyCategories.Expression,
        ImagePath = "/images/capy/expressions/Capy-Base.png",
        IsDefault = true,
        IsActive = true,
        IsStarter = true
    },

    // Hats / Hair
    new CapyItem
    {
        Id = 2,
        Name = "Cowboy Hat",
        Category = CapyCategories.HatHair,
        ImagePath = "/images/capy/hats-hair/Capy-CowboyHat.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 3,
        Name = "Blue & Yellow Party Hat",
        Category = CapyCategories.HatHair,
        ImagePath = "/images/capy/hats-hair/PartyHat-BlueYellow.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },

    // Face Accessories
    new CapyItem
    {
        Id = 4,
        Name = "Cool Sunglasses",
        Category = CapyCategories.FaceAccessory,
        ImagePath = "/images/capy/face-accessories/Sunglasses-Cool.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },

    // Neck Accessories
    new CapyItem
    {
        Id = 5,
        Name = "Green & Red Scarf",
        Category = CapyCategories.NeckAccessory,
        ImagePath = "/images/capy/neck-accessories/Scarf-GreenRed.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 6,
        Name = "Red & White Tie",
        Category = CapyCategories.NeckAccessory,
        ImagePath = "/images/capy/neck-accessories/Tie-RedAndWhite.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },

    // Clothes
    new CapyItem
    {
        Id = 7,
        Name = "Pink T-Shirt",
        Category = CapyCategories.Clothes,
        ImagePath = "/images/capy/clothes/TShirt-Pink.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },

    // Backgrounds
    new CapyItem
    {
        Id = 8,
        Name = "Banana",
        Category = CapyCategories.Background,
        ImagePath = "/images/capy/backgrounds/BG-Banana.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 9,
        Name = "Fields",
        Category = CapyCategories.Background,
        ImagePath = "/images/capy/backgrounds/BG-Fields.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 10,
        Name = "Pale Pink",
        Category = CapyCategories.Background,
        ImagePath = "/images/capy/backgrounds/BG-PalePink.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 11,
        Name = "Pale Purple",
        Category = CapyCategories.Background,
        ImagePath = "/images/capy/backgrounds/BG-PalePurple.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 12,
        Name = "Sky",
        Category = CapyCategories.Background,
        ImagePath = "/images/capy/backgrounds/BG-Sky.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 13,
        Name = "White",
        Category = CapyCategories.Background,
        ImagePath = "/images/capy/backgrounds/BG-White.png",
        IsDefault = true,
        IsActive = true,
        IsStarter = true
    },

    new CapyItem
    {
        Id = 14,
        Name = "Gold Crown",
        Category = CapyCategories.HatHair,
        ImagePath = "/images/capy/hats-hair/Capy-Crown-Gold.png",
        IsDefault = false,
        IsStarter = false,
        IsActive = true
    }
);
        }
    }
}
