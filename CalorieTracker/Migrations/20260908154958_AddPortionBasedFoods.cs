using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPortionBasedFoods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PortionLabel",
                table: "Foods",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServingBasis",
                table: "Foods",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PortionLabelSnapshot",
                table: "DiaryEntries",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServingBasisSnapshot",
                table: "DiaryEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortionLabel",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "ServingBasis",
                table: "Foods");

            migrationBuilder.DropColumn(
                name: "PortionLabelSnapshot",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "ServingBasisSnapshot",
                table: "DiaryEntries");
        }
    }
}
