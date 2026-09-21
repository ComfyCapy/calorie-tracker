using CalorieTracker.Models;
using CalorieTracker.Services;

namespace CalorieTracker.Data;

internal static class CapyItemCatalogue
{
    // IDs and metadata are persisted by EF migrations; changing them is a data change.
    public static CapyItem[] CreateSeedItems() =>
    [
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
        new CapyItem { Id = 15, Name = "Brown T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-Brown.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 16, Name = "Charcoal T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-Charcoal.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 17, Name = "Comfy Capy T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-ComfyCapy.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 18, Name = "Cream T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-Cream.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 19, Name = "Lavender T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-Lavender.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 20, Name = "Lime T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-Lime.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 21, Name = "Mustard T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-Mustard.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 22, Name = "Navy T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-Navy.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 23, Name = "Sage T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-Sage.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 24, Name = "Sky Blue T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-SkyBlue.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 25, Name = "White T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-White.png", IsActive = true, IsStarter = true },
        new CapyItem { Id = 26, Name = "ZZZ T-Shirt", Category = CapyCategories.Clothes, ImagePath = "/images/capy/clothes/TShirt-ZZZ.png", IsActive = true, IsStarter = true },

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
        new CapyItem
        {
            Id = 42,
            Name = "Orange",
            Category = CapyCategories.HatHair,
            ImagePath = "/images/capy/hats-hair/capy-orange.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 43,
            Name = "Rain Hat",
            Category = CapyCategories.HatHair,
            ImagePath = "/images/capy/hats-hair/capy-rain-hat.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 44,
            Name = "Frog Hat",
            Category = CapyCategories.HatHair,
            ImagePath = "/images/capy/hats-hair/capy-frog-hat.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 45,
            Name = "Witch Hat",
            Category = CapyCategories.HatHair,
            ImagePath = "/images/capy/hats-hair/capy-witch-hut.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 46,
            Name = "Wizard Hat",
            Category = CapyCategories.HatHair,
            ImagePath = "/images/capy/hats-hair/capy-wizard-hat.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 47,
            Name = "White Lily",
            Category = CapyCategories.HatHair,
            ImagePath = "/images/capy/hats-hair/capy-white-lily.png",
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
        new CapyItem
        {
            Id = 27,
            Name = "Bandage",
            Category = CapyCategories.FaceAccessory,
            ImagePath = "/images/capy/face-accessories/capy-bandaid.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 28,
            Name = "Duck Bill",
            Category = CapyCategories.FaceAccessory,
            ImagePath = "/images/capy/face-accessories/capy-duckbill.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 29,
            Name = "Eye Patch",
            Category = CapyCategories.FaceAccessory,
            ImagePath = "/images/capy/face-accessories/capy-eyepatch.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 30,
            Name = "Handlebar Moustache",
            Category = CapyCategories.FaceAccessory,
            ImagePath = "/images/capy/face-accessories/capy-handlebar-mustache.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 31,
            Name = "Heart Glasses",
            Category = CapyCategories.FaceAccessory,
            ImagePath = "/images/capy/face-accessories/capy-heart-glasses.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 32,
            Name = "Heart Sticker",
            Category = CapyCategories.FaceAccessory,
            ImagePath = "/images/capy/face-accessories/capy-heart-sticker.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 33,
            Name = "Hypno Glasses",
            Category = CapyCategories.FaceAccessory,
            ImagePath = "/images/capy/face-accessories/capy-hypnoglasses.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 34,
            Name = "Pixel Glasses",
            Category = CapyCategories.FaceAccessory,
            ImagePath = "/images/capy/face-accessories/capy-pixel-glasses.png",
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
        new CapyItem
        {
            Id = 35,
            Name = "Flower Lei",
            Category = CapyCategories.NeckAccessory,
            ImagePath = "/images/capy/neck-accessories/capy-flower-lei.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 36,
            Name = "Neck Goggles",
            Category = CapyCategories.NeckAccessory,
            ImagePath = "/images/capy/neck-accessories/capy-goggles.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 37,
            Name = "Moon Pendant",
            Category = CapyCategories.NeckAccessory,
            ImagePath = "/images/capy/neck-accessories/capy-moon-pendant.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 38,
            Name = "Ribbon Tie",
            Category = CapyCategories.NeckAccessory,
            ImagePath = "/images/capy/neck-accessories/capy-ribbon.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 39,
            Name = "Royal Cloak",
            Category = CapyCategories.NeckAccessory,
            ImagePath = "/images/capy/neck-accessories/capy-royal-cloak.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 40,
            Name = "Star Pendant",
            Category = CapyCategories.NeckAccessory,
            ImagePath = "/images/capy/neck-accessories/capy-star-pendant.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 41,
            Name = "Sun Pendant",
            Category = CapyCategories.NeckAccessory,
            ImagePath = "/images/capy/neck-accessories/capy-sun-pendant.png",
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
            Id = 48,
            Name = "Peach",
            Category = CapyCategories.Background,
            ImagePath = "/images/capy/backgrounds/BG-Peach.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 49,
            Name = "Sage",
            Category = CapyCategories.Background,
            ImagePath = "/images/capy/backgrounds/BG-Sage.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 50,
            Name = "Powder Blue",
            Category = CapyCategories.Background,
            ImagePath = "/images/capy/backgrounds/BG-PowderBlue.png",
            IsDefault = false,
            IsActive = true,
            IsStarter = true
        },
        new CapyItem
        {
            Id = 51,
            Name = "Mocha",
            Category = CapyCategories.Background,
            ImagePath = "/images/capy/backgrounds/BG-Mocha.png",
            IsDefault = false,
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
    ];
}
