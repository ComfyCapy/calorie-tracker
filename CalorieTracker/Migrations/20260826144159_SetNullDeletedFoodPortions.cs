using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class SetNullDeletedFoodPortions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries",
                column: "FoodPortionId",
                principalTable: "FoodPortions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries",
                column: "FoodPortionId",
                principalTable: "FoodPortions",
                principalColumn: "Id");
        }
    }
}
