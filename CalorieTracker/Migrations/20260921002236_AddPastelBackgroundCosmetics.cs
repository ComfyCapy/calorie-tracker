using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPastelBackgroundCosmetics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CapyItems",
                columns: new[] { "Id", "Category", "ImagePath", "IsActive", "IsDefault", "IsStarter", "Name" },
                values: new object[,]
                {
                    { 48, "Background", "/images/capy/backgrounds/BG-Peach.png", true, false, true, "Peach" },
                    { 49, "Background", "/images/capy/backgrounds/BG-Sage.png", true, false, true, "Sage" },
                    { 50, "Background", "/images/capy/backgrounds/BG-PowderBlue.png", true, false, true, "Powder Blue" },
                    { 51, "Background", "/images/capy/backgrounds/BG-Mocha.png", true, false, true, "Mocha" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 51);
        }
    }
}
