using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityFoodsAndAccessRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityFoods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceFoodId = table.Column<int>(type: "INTEGER", nullable: true),
                    SubmitterId = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewerId = table.Column<string>(type: "TEXT", nullable: true),
                    SubmittedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ModeratorNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Calories = table.Column<int>(type: "INTEGER", nullable: false),
                    Protein = table.Column<decimal>(type: "TEXT", nullable: false),
                    Carbohydrates = table.Column<decimal>(type: "TEXT", nullable: false),
                    Fat = table.Column<decimal>(type: "TEXT", nullable: false),
                    ServingSize = table.Column<decimal>(type: "TEXT", nullable: false),
                    CanonicalServingSize = table.Column<decimal>(type: "TEXT", nullable: false),
                    ServingUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ServingBasis = table.Column<int>(type: "INTEGER", nullable: false),
                    PortionLabel = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityFoods", x => x.Id);
                    table.CheckConstraint("CK_CommunityFoods_Basis", "\"ServingBasis\" IN (0, 1)");
                    table.CheckConstraint("CK_CommunityFoods_Name", "length(trim(\"Name\")) BETWEEN 1 AND 200");
                    table.CheckConstraint("CK_CommunityFoods_Nutrition", "\"Calories\" >= 0 AND CAST(\"Protein\" AS REAL) >= 0 AND CAST(\"Carbohydrates\" AS REAL) >= 0 AND CAST(\"Fat\" AS REAL) >= 0 AND CAST(\"ServingSize\" AS REAL) > 0 AND CAST(\"CanonicalServingSize\" AS REAL) > 0");
                    table.CheckConstraint("CK_CommunityFoods_Review", "(\"Status\" = 0 AND \"ReviewedUtc\" IS NULL) OR (\"Status\" IN (1, 2) AND \"ReviewedUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_CommunityFoods_Status", "\"Status\" IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_CommunityFoods_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CommunityFoods_AspNetUsers_SubmitterId",
                        column: x => x.SubmitterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CommunityFoods_Foods_SourceFoodId",
                        column: x => x.SourceFoodId,
                        principalTable: "Foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CommunityFoodVotes",
                columns: table => new
                {
                    CommunityFoodId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityFoodVotes", x => new { x.CommunityFoodId, x.UserId });
                    table.CheckConstraint("CK_CommunityFoodVotes_Value", "\"Value\" IN (-1, 1)");
                    table.ForeignKey(
                        name: "FK_CommunityFoodVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityFoodVotes_CommunityFoods_CommunityFoodId",
                        column: x => x.CommunityFoodId,
                        principalTable: "CommunityFoods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Production may already contain an operator-created role with one
            // of these normalized names. Preserve it rather than failing the
            // deployment on the unique normalized-name index.
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO "AspNetRoles" ("Id", "ConcurrencyStamp", "Name", "NormalizedName") VALUES
                    ('role-admin', 'role-admin', 'Admin', 'ADMIN'),
                    ('role-beta', 'role-beta', 'Beta', 'BETA'),
                    ('role-standard', 'role-standard', 'Standard', 'STANDARD');

                INSERT OR IGNORE INTO "AspNetUserRoles" ("UserId", "RoleId")
                SELECT user."Id", role."Id"
                FROM "AspNetUsers" AS user
                JOIN "AspNetRoles" AS role ON role."NormalizedName" = 'STANDARD';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityFoods_ReviewerId",
                table: "CommunityFoods",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityFoods_SourceFoodId",
                table: "CommunityFoods",
                column: "SourceFoodId",
                unique: true,
                filter: "\"SourceFoodId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityFoods_Status_Name_Id",
                table: "CommunityFoods",
                columns: new[] { "Status", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityFoods_SubmitterId",
                table: "CommunityFoods",
                column: "SubmitterId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityFoodVotes_UserId",
                table: "CommunityFoodVotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityFoodVotes");

            migrationBuilder.DropTable(
                name: "CommunityFoods");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "role-admin");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "role-beta");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "role-standard");
        }
    }
}
