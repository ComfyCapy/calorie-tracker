using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddHatCosmetics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CapyItems",
                columns: new[] { "Id", "Category", "ImagePath", "IsActive", "IsDefault", "IsStarter", "Name" },
                values: new object[,]
                {
                    { 42, "HatHair", "/images/capy/hats-hair/capy-orange.png", true, false, true, "Orange" },
                    { 43, "HatHair", "/images/capy/hats-hair/capy-rain-hat.png", true, false, true, "Rain Hat" },
                    { 44, "HatHair", "/images/capy/hats-hair/capy-frog-hat.png", true, false, true, "Frog Hat" },
                    { 45, "HatHair", "/images/capy/hats-hair/capy-witch-hut.png", true, false, true, "Witch Hat" },
                    { 46, "HatHair", "/images/capy/hats-hair/capy-wizard-hat.png", true, false, true, "Wizard Hat" },
                    { 47, "HatHair", "/images/capy/hats-hair/capy-white-lily.png", true, false, true, "White Lily" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 47);
        }
    }
}
