using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT OR IGNORE INTO "AspNetRoles" ("Id", "ConcurrencyStamp", "Name", "NormalizedName")
                VALUES ('role-owner', 'role-owner', 'Owner', 'OWNER');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Role membership is operational security data. A downgrade leaves
            // the harmless extra role intact rather than deleting memberships.
        }
    }
}
