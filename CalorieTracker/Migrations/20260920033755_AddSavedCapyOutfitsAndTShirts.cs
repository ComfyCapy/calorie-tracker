using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedCapyOutfitsAndTShirts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedCapyOutfits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ExpressionId = table.Column<int>(type: "INTEGER", nullable: true),
                    HatHairId = table.Column<int>(type: "INTEGER", nullable: true),
                    FaceAccessoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    NeckAccessoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClothesId = table.Column<int>(type: "INTEGER", nullable: true),
                    BackgroundId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedCapyOutfits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedCapyOutfits_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CapyItems",
                columns: new[] { "Id", "Category", "ImagePath", "IsActive", "IsDefault", "IsStarter", "Name" },
                values: new object[,]
                {
                    { 15, "Clothes", "/images/capy/clothes/TShirt-Brown.png", true, false, true, "Brown T-Shirt" },
                    { 16, "Clothes", "/images/capy/clothes/TShirt-Charcoal.png", true, false, true, "Charcoal T-Shirt" },
                    { 17, "Clothes", "/images/capy/clothes/TShirt-ComfyCapy.png", true, false, true, "Comfy Capy T-Shirt" },
                    { 18, "Clothes", "/images/capy/clothes/TShirt-Cream.png", true, false, true, "Cream T-Shirt" },
                    { 19, "Clothes", "/images/capy/clothes/TShirt-Lavender.png", true, false, true, "Lavender T-Shirt" },
                    { 20, "Clothes", "/images/capy/clothes/TShirt-Lime.png", true, false, true, "Lime T-Shirt" },
                    { 21, "Clothes", "/images/capy/clothes/TShirt-Mustard.png", true, false, true, "Mustard T-Shirt" },
                    { 22, "Clothes", "/images/capy/clothes/TShirt-Navy.png", true, false, true, "Navy T-Shirt" },
                    { 23, "Clothes", "/images/capy/clothes/TShirt-Sage.png", true, false, true, "Sage T-Shirt" },
                    { 24, "Clothes", "/images/capy/clothes/TShirt-SkyBlue.png", true, false, true, "Sky Blue T-Shirt" },
                    { 25, "Clothes", "/images/capy/clothes/TShirt-White.png", true, false, true, "White T-Shirt" },
                    { 26, "Clothes", "/images/capy/clothes/TShirt-ZZZ.png", true, false, true, "ZZZ T-Shirt" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedCapyOutfits_UserId_Name",
                table: "SavedCapyOutfits",
                columns: new[] { "UserId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedCapyOutfits");

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 26);
        }
    }
}
