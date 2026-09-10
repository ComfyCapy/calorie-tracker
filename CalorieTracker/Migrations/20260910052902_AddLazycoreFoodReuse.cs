using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddLazycoreFoodReuse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_UserId",
                table: "DiaryEntries");

            migrationBuilder.AddColumn<string>(
                name: "ApproximationLabel",
                table: "DiaryEntries",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproximate",
                table: "DiaryEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    YieldServings = table.Column<decimal>(type: "TEXT", nullable: false),
                    FoodId = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "SavedMeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedMeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedMeals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    FoodId = table.Column<int>(type: "INTEGER", nullable: false),
                    FoodPortionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    PortionQuantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    IsApproximate = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApproximationLabel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    FoodNameSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ServingSizeSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    ServingUnitSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalServingSizeSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    ServingBasisSnapshot = table.Column<int>(type: "INTEGER", nullable: false),
                    PortionLabelSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    CaloriesSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProteinSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    CarbohydratesSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    FatSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    PortionNameSnapshot = table.Column<string>(type: "TEXT", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "SavedMealItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SavedMealId = table.Column<int>(type: "INTEGER", nullable: false),
                    FoodId = table.Column<int>(type: "INTEGER", nullable: false),
                    FoodPortionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    PortionQuantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    IsApproximate = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApproximationLabel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    FoodNameSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ServingSizeSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    ServingUnitSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalServingSizeSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    ServingBasisSnapshot = table.Column<int>(type: "INTEGER", nullable: false),
                    PortionLabelSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    CaloriesSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProteinSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    CarbohydratesSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    FatSnapshot = table.Column<decimal>(type: "TEXT", nullable: false),
                    PortionNameSnapshot = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedMealItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedMealItems_FoodPortions_FoodPortionId",
                        column: x => x.FoodPortionId,
                        principalTable: "FoodPortions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedMealItems_Foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "Foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedMealItems_SavedMeals_SavedMealId",
                        column: x => x.SavedMealId,
                        principalTable: "SavedMeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId_Date_Id",
                table: "DiaryEntries",
                columns: new[] { "UserId", "Date", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId_FoodId",
                table: "DiaryEntries",
                columns: new[] { "UserId", "FoodId" });

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

            migrationBuilder.CreateIndex(
                name: "IX_SavedMealItems_FoodId",
                table: "SavedMealItems",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedMealItems_FoodPortionId",
                table: "SavedMealItems",
                column: "FoodPortionId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedMealItems_SavedMealId",
                table: "SavedMealItems",
                column: "SavedMealId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedMeals_UserId_Name",
                table: "SavedMeals",
                columns: new[] { "UserId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "SavedMealItems");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "SavedMeals");

            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_UserId_Date_Id",
                table: "DiaryEntries");

            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_UserId_FoodId",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "ApproximationLabel",
                table: "DiaryEntries");

            migrationBuilder.DropColumn(
                name: "IsApproximate",
                table: "DiaryEntries");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_UserId",
                table: "DiaryEntries",
                column: "UserId");
        }
    }
}
