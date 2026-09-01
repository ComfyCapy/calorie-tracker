using Microsoft.EntityFrameworkCore;
using CalorieTracker.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

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
        public DbSet<DiaryEntry> DiaryEntries { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<FoodPortion> FoodPortions { get; set; }

        public DbSet<CapyItem> CapyItems { get; set; }
        public DbSet<UserCapyItem> UserCapyItems { get; set; }
        public DbSet<UserCapyAppearance> UserCapyAppearances { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<UserProfile>()
                .HasOne(profile => profile.User)
                .WithOne(user => user.UserProfile)
                .HasForeignKey<UserProfile>(
                    profile => profile.UserId);

            builder.Entity<FoodPortion>()
                .HasOne(portion => portion.Food)
                .WithMany(food => food.Portions)
                .HasForeignKey(portion => portion.FoodId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DiaryEntry>()
                .HasOne(entry => entry.FoodPortion)
                .WithMany()
                .HasForeignKey(entry => entry.FoodPortionId)
                .OnDelete(DeleteBehavior.SetNull);


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
        Category = "Expression",
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
        Category = "HatHair",
        ImagePath = "/images/capy/hats-hair/Capy-CowboyHat.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 3,
        Name = "Blue & Yellow Party Hat",
        Category = "HatHair",
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
        Category = "FaceAccessory",
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
        Category = "NeckAccessory",
        ImagePath = "/images/capy/neck-accessories/Scarf-GreenRed.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 6,
        Name = "Red & White Tie",
        Category = "NeckAccessory",
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
        Category = "Clothes",
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
        Category = "Background",
        ImagePath = "/images/capy/backgrounds/BG-Banana.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 9,
        Name = "Fields",
        Category = "Background",
        ImagePath = "/images/capy/backgrounds/BG-Fields.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 10,
        Name = "Pale Pink",
        Category = "Background",
        ImagePath = "/images/capy/backgrounds/BG-PalePink.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 11,
        Name = "Pale Purple",
        Category = "Background",
        ImagePath = "/images/capy/backgrounds/BG-PalePurple.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 12,
        Name = "Sky",
        Category = "Background",
        ImagePath = "/images/capy/backgrounds/BG-Sky.png",
        IsDefault = false,
        IsActive = true,
        IsStarter = true
    },
    new CapyItem
    {
        Id = 13,
        Name = "White",
        Category = "Background",
        ImagePath = "/images/capy/backgrounds/BG-White.png",
        IsDefault = true,
        IsActive = true,
        IsStarter = true
    },

    new CapyItem
    {
        Id = 14,
        Name = "Gold Crown",
        Category = "HatHair",
        ImagePath = "/images/capy/hats-hair/Capy-Crown-Gold.png",
        IsDefault = false,
        IsStarter = false,
        IsActive = true
    }
);
        }
    }
}