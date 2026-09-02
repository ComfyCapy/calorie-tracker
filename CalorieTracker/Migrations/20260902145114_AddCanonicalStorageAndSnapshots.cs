using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalStorageAndSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_DiaryEntries_Foods_FoodId",
                table: "DiaryEntries");

            migrationBuilder.AddColumn<decimal>(
                name: "CanonicalServingSize",
                table: "Foods",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "FoodPortions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CaloriesSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CanonicalServingSizeSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CarbohydratesSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FatSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FoodNameSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PortionNameSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProteinSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServingSizeSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ServingUnitSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries",
                column: "FoodPortionId",
                principalTable: "FoodPortions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryEntries_Foods_FoodId",
                table: "DiaryEntries",
                column: "FoodId",
                principalTable: "Foods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_DiaryEntries_Foods_FoodId",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "CanonicalServingSize",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "FoodPortions");

            migrationBuilder.DropColumn(
                name: "CaloriesSnapshot",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "CanonicalServingSizeSnapshot",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "CarbohydratesSnapshot",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "FatSnapshot",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "FoodNameSnapshot",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "PortionNameSnapshot",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "ProteinSnapshot",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "ServingSizeSnapshot",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "ServingUnitSnapshot",
                table: "DiaryEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryEntries_FoodPortions_FoodPortionId",
                table: "DiaryEntries",
                column: "FoodPortionId",
                principalTable: "FoodPortions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryEntries_Foods_FoodId",
                table: "DiaryEntries",
                column: "FoodId",
                principalTable: "Foods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
