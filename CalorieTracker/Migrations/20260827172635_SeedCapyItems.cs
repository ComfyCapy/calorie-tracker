using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class SeedCapyItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CapyItems",
                columns: new[] { "Id", "Category", "ImagePath", "IsActive", "IsDefault", "Name" },
                values: new object[,]
                {
                    { 1, "Expression", "/images/capy/expressions/Capy-Base.png", true, true, "Base Capy" },
                    { 2, "HatHair", "/images/capy/hats-hair/Capy-CowboyHat.png", true, false, "Cowboy Hat" },
                    { 3, "HatHair", "/images/capy/hats-hair/PartyHat-BlueYellow.png", true, false, "Blue & Yellow Party Hat" },
                    { 4, "FaceAccessory", "/images/capy/face-accessories/Sunglasses-Cool.png", true, false, "Cool Sunglasses" },
                    { 5, "NeckAccessory", "/images/capy/neck-accessories/Scarf-GreenRed.png", true, false, "Green & Red Scarf" },
                    { 6, "NeckAccessory", "/images/capy/neck-accessories/Tie-RedAndWhite.png", true, false, "Red & White Tie" },
                    { 7, "Clothes", "/images/capy/clothes/TShirt-Pink.png", true, false, "Pink T-Shirt" },
                    { 8, "Background", "/images/capy/backgrounds/BG-Banana.png", true, false, "Banana" },
                    { 9, "Background", "/images/capy/backgrounds/BG-Fields.png", true, false, "Fields" },
                    { 10, "Background", "/images/capy/backgrounds/BG-PalePink.png", true, false, "Pale Pink" },
                    { 11, "Background", "/images/capy/backgrounds/BG-PalePurple.png", true, false, "Pale Purple" },
                    { 12, "Background", "/images/capy/backgrounds/BG-Sky.png", true, false, "Sky" },
                    { 13, "Background", "/images/capy/backgrounds/BG-White.png", true, true, "White" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 13);
        }
    }
}
