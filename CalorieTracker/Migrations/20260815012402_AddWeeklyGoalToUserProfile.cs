using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyGoalToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WeeklyGoalKg",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeeklyGoalKg",
                table: "UserProfiles");
        }
    }
}
