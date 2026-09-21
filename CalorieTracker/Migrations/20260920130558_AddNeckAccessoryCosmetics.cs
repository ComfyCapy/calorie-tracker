using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddNeckAccessoryCosmetics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CapyItems",
                columns: new[] { "Id", "Category", "ImagePath", "IsActive", "IsDefault", "IsStarter", "Name" },
                values: new object[,]
                {
                    { 35, "NeckAccessory", "/images/capy/neck-accessories/capy-flower-lei.png", true, false, true, "Flower Lei" },
                    { 36, "NeckAccessory", "/images/capy/neck-accessories/capy-goggles.png", true, false, true, "Neck Goggles" },
                    { 37, "NeckAccessory", "/images/capy/neck-accessories/capy-moon-pendant.png", true, false, true, "Moon Pendant" },
                    { 38, "NeckAccessory", "/images/capy/neck-accessories/capy-ribbon.png", true, false, true, "Ribbon Tie" },
                    { 39, "NeckAccessory", "/images/capy/neck-accessories/capy-royal-cloak.png", true, false, true, "Royal Cloak" },
                    { 40, "NeckAccessory", "/images/capy/neck-accessories/capy-star-pendant.png", true, false, true, "Star Pendant" },
                    { 41, "NeckAccessory", "/images/capy/neck-accessories/capy-sun-pendant.png", true, false, true, "Sun Pendant" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 41);
        }
    }
}
