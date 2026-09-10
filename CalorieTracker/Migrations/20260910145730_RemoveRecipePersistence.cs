using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRecipePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "Recipes");

            // Preserve serving-food rows for any historical Diary/Saved Meal
            // references, but remove them from active food pickers.
            migrationBuilder.Sql(
                "UPDATE \"Foods\" SET \"IsDeleted\" = 1 WHERE \"Source\" = 'Recipe';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FoodId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    YieldServings = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recipes_Foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "Foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FoodId = table.Column<int>(type: "INTEGER", nullable: false),
                    FoodPortionId = table.Column<int>(type: "INTEGER", nullable: true),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApproximationLabel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CaloriesSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    CanonicalServingSizeSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    CarbohydratesSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    FatSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    FoodNameSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    IsApproximate = table.Column<bool>(type: "INTEGER", nullable: false),
                    PortionLabelSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    PortionNameSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    PortionQuantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    ProteinSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    ServingBasisSnapshot = table.Column<int>(type: "INTEGER", nullable: false),
                    ServingSizeSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    ServingUnitSnapshot = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_FoodPortions_FoodPortionId",
                        column: x => x.FoodPortionId,
                        principalTable: "FoodPortions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "Foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_FoodId",
                table: "RecipeIngredients",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_FoodPortionId",
                table: "RecipeIngredients",
                column: "FoodPortionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId",
                table: "RecipeIngredients",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_FoodId",
                table: "Recipes",
                column: "FoodId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_UserId_Name",
                table: "Recipes",
                columns: new[] { "UserId", "Name" });
        }
    }
}
