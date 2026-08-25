using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPortionTrackingToDiaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FoodPortionId",
                table: "DiaryEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PortionQuantity",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_FoodPortionId",
                table: "DiaryEntries",
                column: "FoodPortionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries",
                column: "FoodPortionId",
                principalTable: "FoodPortions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries");

            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_FoodPortionId",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "FoodPortionId",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "PortionQuantity",
                table: "DiaryEntries");
        }
    }
}
