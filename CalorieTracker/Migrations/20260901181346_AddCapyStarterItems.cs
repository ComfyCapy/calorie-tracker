using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddCapyStarterItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStarter",
                table: "CapyItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 5,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 6,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 7,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 8,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 9,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 10,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 12,
                column: "IsStarter",
                value: true);

            migrationBuilder.UpdateData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 13,
                column: "IsStarter",
                value: true);

            migrationBuilder.InsertData(
                table: "CapyItems",
                columns: new[] { "Id", "Category", "ImagePath", "IsActive", "IsDefault", "IsStarter", "Name" },
                values: new object[] { 14, "HatHair", "/images/capy/hats-hair/Capy-Crown-Gold.png", true, false, false, "Gold Crown" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CapyItems",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DropColumn(
                name: "IsStarter",
                table: "CapyItems");
        }
    }
}
