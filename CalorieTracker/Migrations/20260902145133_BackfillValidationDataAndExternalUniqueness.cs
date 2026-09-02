using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class BackfillValidationDataAndExternalUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Foods_UserId",
                table: "Foods");

            // Preserve preferred display units while converting calculation
            // amounts to canonical grams or millilitres. Unsupported legacy
            // units are treated as grams; future writes use the whitelist.
            migrationBuilder.Sql(
                """
                UPDATE "Foods"
                SET "CanonicalServingSize" =
                    "ServingSize" * CASE lower(trim("ServingUnit"))
                        WHEN 'kg' THEN 1000.0
                        WHEN 'oz' THEN 28.349523125
                        WHEN 'lb' THEN 453.59237
                        WHEN 'l' THEN 1000.0
                        WHEN 'fl oz' THEN 29.5735295625
                        ELSE 1.0
                    END,
                    "ServingUnit" = CASE lower(trim("ServingUnit"))
                        WHEN 'g' THEN 'g'
                        WHEN 'kg' THEN 'kg'
                        WHEN 'oz' THEN 'oz'
                        WHEN 'lb' THEN 'lb'
                        WHEN 'ml' THEN 'ml'
                        WHEN 'l' THEN 'L'
                        WHEN 'fl oz' THEN 'fl oz'
                        ELSE 'g'
                    END;

                UPDATE "FoodPortions"
                SET "Amount" = "Amount" * CASE (
                    SELECT "ServingUnit" FROM "Foods"
                    WHERE "Foods"."Id" = "FoodPortions"."FoodId")
                        WHEN 'kg' THEN 1000.0
                        WHEN 'oz' THEN 28.349523125
                        WHEN 'lb' THEN 453.59237
                        WHEN 'L' THEN 1000.0
                        WHEN 'fl oz' THEN 29.5735295625
                        ELSE 1.0
                    END;

                UPDATE "DiaryEntries"
                SET "Quantity" = "Quantity" * CASE (
                    SELECT "ServingUnit" FROM "Foods"
                    WHERE "Foods"."Id" = "DiaryEntries"."FoodId")
                        WHEN 'kg' THEN 1000.0
                        WHEN 'oz' THEN 28.349523125
                        WHEN 'lb' THEN 453.59237
                        WHEN 'L' THEN 1000.0
                        WHEN 'fl oz' THEN 29.5735295625
                        ELSE 1.0
                    END;

                UPDATE "DiaryEntries"
                SET "FoodNameSnapshot" = coalesce((
                        SELECT "Name" FROM "Foods"
                        WHERE "Foods"."Id" = "DiaryEntries"."FoodId"), ''),
                    "ServingSizeSnapshot" = coalesce((
                        SELECT "ServingSize" FROM "Foods"
                        WHERE "Foods"."Id" = "DiaryEntries"."FoodId"), 0),
                    "ServingUnitSnapshot" = coalesce((
                        SELECT "ServingUnit" FROM "Foods"
                        WHERE "Foods"."Id" = "DiaryEntries"."FoodId"), 'g'),
                    "CanonicalServingSizeSnapshot" = coalesce((
                        SELECT "CanonicalServingSize" FROM "Foods"
                        WHERE "Foods"."Id" = "DiaryEntries"."FoodId"), 0),
                    "CaloriesSnapshot" = coalesce((
                        SELECT "Calories" FROM "Foods"
                        WHERE "Foods"."Id" = "DiaryEntries"."FoodId"), 0),
                    "ProteinSnapshot" = coalesce((
                        SELECT "Protein" FROM "Foods"
                        WHERE "Foods"."Id" = "DiaryEntries"."FoodId"), 0),
                    "CarbohydratesSnapshot" = coalesce((
                        SELECT "Carbohydrates" FROM "Foods"
                        WHERE "Foods"."Id" = "DiaryEntries"."FoodId"), 0),
                    "FatSnapshot" = coalesce((
                        SELECT "Fat" FROM "Foods"
                        WHERE "Foods"."Id" = "DiaryEntries"."FoodId"), 0),
                    "PortionNameSnapshot" = CASE
                        WHEN "FoodPortionId" IS NULL THEN NULL
                        ELSE (SELECT "Name" FROM "FoodPortions"
                              WHERE "FoodPortions"."Id" = "DiaryEntries"."FoodPortionId")
                    END;
                """);

            // Normalize valid USDA IDs and merge pre-existing cached
            // duplicates before enforcing one reusable copy per user.
            migrationBuilder.Sql(
                """
                UPDATE "Foods"
                SET "ExternalId" = CAST(CAST("ExternalId" AS INTEGER) AS TEXT)
                WHERE "Source" = 'USDA'
                  AND "ExternalId" <> ''
                  AND "ExternalId" NOT GLOB '*[^0-9]*'
                  AND CAST("ExternalId" AS INTEGER) > 0;

                UPDATE "Foods" AS keeper
                SET "IsFavourite" = 1
                WHERE keeper."Id" = (
                    SELECT min(candidate."Id") FROM "Foods" AS candidate
                    WHERE candidate."UserId" = keeper."UserId"
                      AND candidate."Source" = keeper."Source"
                      AND candidate."ExternalId" = keeper."ExternalId")
                  AND EXISTS (
                    SELECT 1 FROM "Foods" AS duplicate
                    WHERE duplicate."UserId" = keeper."UserId"
                      AND duplicate."Source" = keeper."Source"
                      AND duplicate."ExternalId" = keeper."ExternalId"
                      AND duplicate."IsFavourite" = 1);

                UPDATE "DiaryEntries"
                SET "FoodId" = (
                    SELECT min(keeper."Id")
                    FROM "Foods" AS duplicate
                    JOIN "Foods" AS keeper
                      ON keeper."UserId" = duplicate."UserId"
                     AND keeper."Source" = duplicate."Source"
                     AND keeper."ExternalId" = duplicate."ExternalId"
                    WHERE duplicate."Id" = "DiaryEntries"."FoodId")
                WHERE "FoodId" IN (
                    SELECT duplicate."Id" FROM "Foods" AS duplicate
                    WHERE duplicate."UserId" IS NOT NULL
                      AND duplicate."Source" IS NOT NULL
                      AND duplicate."ExternalId" IS NOT NULL
                      AND duplicate."Id" <> (
                          SELECT min(keeper."Id") FROM "Foods" AS keeper
                          WHERE keeper."UserId" = duplicate."UserId"
                            AND keeper."Source" = duplicate."Source"
                            AND keeper."ExternalId" = duplicate."ExternalId"));

                UPDATE "FoodPortions"
                SET "FoodId" = (
                    SELECT min(keeper."Id")
                    FROM "Foods" AS duplicate
                    JOIN "Foods" AS keeper
                      ON keeper."UserId" = duplicate."UserId"
                     AND keeper."Source" = duplicate."Source"
                     AND keeper."ExternalId" = duplicate."ExternalId"
                    WHERE duplicate."Id" = "FoodPortions"."FoodId")
                WHERE "FoodId" IN (
                    SELECT duplicate."Id" FROM "Foods" AS duplicate
                    WHERE duplicate."UserId" IS NOT NULL
                      AND duplicate."Source" IS NOT NULL
                      AND duplicate."ExternalId" IS NOT NULL
                      AND duplicate."Id" <> (
                          SELECT min(keeper."Id") FROM "Foods" AS keeper
                          WHERE keeper."UserId" = duplicate."UserId"
                            AND keeper."Source" = duplicate."Source"
                            AND keeper."ExternalId" = duplicate."ExternalId"));

                DELETE FROM "Foods"
                WHERE "UserId" IS NOT NULL
                  AND "Source" IS NOT NULL
                  AND "ExternalId" IS NOT NULL
                  AND "Id" <> (
                      SELECT min(keeper."Id") FROM "Foods" AS keeper
                      WHERE keeper."UserId" = "Foods"."UserId"
                        AND keeper."Source" = "Foods"."Source"
                        AND keeper."ExternalId" = "Foods"."ExternalId");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Foods_UserId_Source_ExternalId",
                table: "Foods",
                columns: new[] { "UserId", "Source", "ExternalId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL AND \"Source\" IS NOT NULL AND \"ExternalId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Foods_UserId_Source_ExternalId",
                table: "Foods");

            migrationBuilder.CreateIndex(
                name: "IX_Foods_UserId",
                table: "Foods",
                column: "UserId");
        }
    }
}
