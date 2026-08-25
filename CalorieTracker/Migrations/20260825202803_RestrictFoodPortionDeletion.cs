using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class RestrictFoodPortionDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoodPortions_Foods_FoodId",
                table: "FoodPortions");

            migrationBuilder.AddForeignKey(
                name: "FK_FoodPortions_Foods_FoodId",
                table: "FoodPortions",
                column: "FoodId",
                principalTable: "Foods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FoodPortions_Foods_FoodId",
                table: "FoodPortions");

            migrationBuilder.AddForeignKey(
                name: "FK_FoodPortions_Foods_FoodId",
                table: "FoodPortions",
                column: "FoodId",
                principalTable: "Foods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
