using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CalorieTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyMaintenanceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyMaintenanceSnapshots",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MaintenanceCalories = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMaintenanceSnapshots", x => new { x.UserId, x.Date });
                    table.ForeignKey(
                        name: "FK_DailyMaintenanceSnapshots_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Existing profile history is unavailable, so backfill each distinct
            // diary date using the user's current maintenance inputs and their age
            // on that date. Goal and calorie-target settings are intentionally absent.
            migrationBuilder.Sql(
                """
                WITH "DiaryDates" AS
                (
                    SELECT DISTINCT
                        "UserId",
                        date("Date") AS "SnapshotDate"
                    FROM "DiaryEntries"
                    WHERE date("Date") IS NOT NULL
                ),
                "HistoricalInputs" AS
                (
                    SELECT
                        diary."UserId",
                        diary."SnapshotDate",
                        profile."HeightCm",
                        profile."WeightKg",
                        profile."CalculationSex",
                        profile."ActivityLevel",
                        CAST(strftime('%Y', diary."SnapshotDate") AS INTEGER) -
                        CAST(strftime('%Y', profile."DateOfBirth") AS INTEGER) -
                        CASE
                            WHEN diary."SnapshotDate" <
                                CASE
                                    WHEN strftime('%m-%d', profile."DateOfBirth") = '02-29'
                                         AND
                                         (
                                             CAST(strftime('%Y', diary."SnapshotDate") AS INTEGER) % 4 != 0
                                             OR
                                             (
                                                 CAST(strftime('%Y', diary."SnapshotDate") AS INTEGER) % 100 = 0
                                                 AND CAST(strftime('%Y', diary."SnapshotDate") AS INTEGER) % 400 != 0
                                             )
                                         )
                                        THEN printf(
                                            '%04d-03-01',
                                            CAST(strftime('%Y', diary."SnapshotDate") AS INTEGER))
                                    ELSE printf(
                                        '%04d-%s',
                                        CAST(strftime('%Y', diary."SnapshotDate") AS INTEGER),
                                        strftime('%m-%d', profile."DateOfBirth"))
                                END
                                THEN 1
                            ELSE 0
                        END AS "Age"
                    FROM "DiaryDates" AS diary
                    INNER JOIN "UserProfiles" AS profile
                        ON profile."UserId" = diary."UserId"
                    WHERE profile."DateOfBirth" IS NOT NULL
                ),
                "ValidInputs" AS
                (
                    SELECT
                        *,
                        10 * CAST("WeightKg" AS REAL) +
                        6.25 * CAST("HeightCm" AS REAL) -
                        5 * "Age" +
                        CASE "CalculationSex"
                            WHEN 'Male' THEN 5
                            WHEN 'Female' THEN -161
                            ELSE 0
                        END AS "Bmr",
                        CASE "ActivityLevel"
                            WHEN 'Sedentary' THEN 1.2
                            WHEN 'LightlyActive' THEN 1.375
                            WHEN 'ModeratelyActive' THEN 1.55
                            WHEN 'VeryActive' THEN 1.725
                            WHEN 'ExtraActive' THEN 1.9
                            ELSE 0
                        END AS "ActivityMultiplier"
                    FROM "HistoricalInputs"
                    WHERE "Age" BETWEEN 18 AND 120
                        AND CAST("HeightCm" AS REAL) BETWEEN 50 AND 300
                        AND CAST("WeightKg" AS REAL) BETWEEN 20 AND 500
                        AND "CalculationSex" IN ('Male', 'Female')
                        AND "ActivityLevel" IN
                            ('Sedentary', 'LightlyActive', 'ModeratelyActive', 'VeryActive', 'ExtraActive')
                )
                INSERT INTO "DailyMaintenanceSnapshots"
                    ("UserId", "Date", "MaintenanceCalories")
                SELECT
                    "UserId",
                    "SnapshotDate",
                    "Bmr" * "ActivityMultiplier"
                FROM "ValidInputs"
                WHERE "Bmr" > 0
                    AND "ActivityMultiplier" > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyMaintenanceSnapshots");
        }
    }
}
