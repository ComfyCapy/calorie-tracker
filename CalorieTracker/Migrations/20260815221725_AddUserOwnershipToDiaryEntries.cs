using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOwnershipToDiaryEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM DiaryEntries;");
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "DiaryEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId",
                table: "DiaryEntries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryEntries_AspNetUsers_UserId",
                table: "DiaryEntries",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiaryEntries_AspNetUsers_UserId",
                table: "DiaryEntries");

            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_UserId",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "DiaryEntries");
        }
    }
}
