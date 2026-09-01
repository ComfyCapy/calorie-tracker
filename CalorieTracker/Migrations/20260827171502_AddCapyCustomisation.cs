using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddCapyCustomisation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapyItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapyItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserCapyAppearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ExpressionId = table.Column<int>(type: "INTEGER", nullable: true),
                    HatHairId = table.Column<int>(type: "INTEGER", nullable: true),
                    FaceAccessoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    NeckAccessoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClothesId = table.Column<int>(type: "INTEGER", nullable: true),
                    BackgroundId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCapyAppearances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCapyAppearances_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCapyAppearances_CapyItems_BackgroundId",
                        column: x => x.BackgroundId,
                        principalTable: "CapyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCapyAppearances_CapyItems_ClothesId",
                        column: x => x.ClothesId,
                        principalTable: "CapyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCapyAppearances_CapyItems_ExpressionId",
                        column: x => x.ExpressionId,
                        principalTable: "CapyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCapyAppearances_CapyItems_FaceAccessoryId",
                        column: x => x.FaceAccessoryId,
                        principalTable: "CapyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCapyAppearances_CapyItems_HatHairId",
                        column: x => x.HatHairId,
                        principalTable: "CapyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCapyAppearances_CapyItems_NeckAccessoryId",
                        column: x => x.NeckAccessoryId,
                        principalTable: "CapyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserCapyItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CapyItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCapyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCapyItems_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCapyItems_CapyItems_CapyItemId",
                        column: x => x.CapyItemId,
                        principalTable: "CapyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCapyAppearances_BackgroundId",
                table: "UserCapyAppearances",
                column: "BackgroundId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapyAppearances_ClothesId",
                table: "UserCapyAppearances",
                column: "ClothesId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapyAppearances_ExpressionId",
                table: "UserCapyAppearances",
                column: "ExpressionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapyAppearances_FaceAccessoryId",
                table: "UserCapyAppearances",
                column: "FaceAccessoryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapyAppearances_HatHairId",
                table: "UserCapyAppearances",
                column: "HatHairId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapyAppearances_NeckAccessoryId",
                table: "UserCapyAppearances",
                column: "NeckAccessoryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapyAppearances_UserId",
                table: "UserCapyAppearances",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCapyItems_CapyItemId",
                table: "UserCapyItems",
                column: "CapyItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapyItems_UserId_CapyItemId",
                table: "UserCapyItems",
                columns: new[] { "UserId", "CapyItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCapyAppearances");

            migrationBuilder.DropTable(
                name: "UserCapyItems");

            migrationBuilder.DropTable(
                name: "CapyItems");
        }
    }
}
