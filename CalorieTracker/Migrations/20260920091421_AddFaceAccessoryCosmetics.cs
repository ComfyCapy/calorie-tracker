using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceAccessoryCosmetics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CapyItems",
                columns: new[] { "Id", "Category", "ImagePath", "IsActive", "IsDefault", "IsStarter", "Name" },
                values: new object[,]
                {
                    { 27, "FaceAccessory", "/images/capy/face-accessories/capy-bandaid.png", true, false, true, "Bandage" },
                    { 28, "FaceAccessory", "/images/capy/face-accessories/capy-duckbill.png", true, false, true, "Duck Bill" },
                    { 29, "FaceAccessory", "/images/capy/face-accessories/capy-eyepatch.png", true, false, true, "Eye Patch" },
                    { 30, "FaceAccessory", "/images/capy/face-accessories/capy-handlebar-mustache.png", true, false, true, "Handlebar Moustache" },
                    { 31, "FaceAccessory", "/images/capy/face-accessories/capy-heart-glasses.png", true, false, true, "Heart Glasses" },
                    { 32, "FaceAccessory", "/images/capy/face-accessories/capy-heart-sticker.png", true, false, true, "Heart Sticker" },
                    { 33, "FaceAccessory", "/images/capy/face-accessories/capy-hypnoglasses.png", true, false, true, "Hypno Glasses" },
                    { 34, "FaceAccessory", "/images/capy/face-accessories/capy-pixel-glasses.png", true, false, true, "Pixel Glasses" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 34);
        }
    }
}
