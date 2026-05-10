using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftUniquenessConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "ShiftSubmissions"
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM (
                        SELECT
                            "Id",
                            ROW_NUMBER() OVER (
                                PARTITION BY "UserId", "ShiftDayId", "ShiftType"
                                ORDER BY COALESCE("SubmittedAt", '-infinity'::timestamp with time zone) DESC, "Id" DESC
                            ) AS row_number
                        FROM "ShiftSubmissions"
                    ) ranked
                    WHERE ranked.row_number > 1
                );
                """);

            migrationBuilder.DropIndex(
                name: "IX_ShiftSubmissions_UserId",
                table: "ShiftSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ShiftDays_RecruitmentPeriodId_Id",
                table: "ShiftDays");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSubmissions_UserId_ShiftDayId_ShiftType",
                table: "ShiftSubmissions",
                columns: new[] { "UserId", "ShiftDayId", "ShiftType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDays_RecruitmentPeriodId_Date",
                table: "ShiftDays",
                columns: new[] { "RecruitmentPeriodId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftSubmissions_UserId_ShiftDayId_ShiftType",
                table: "ShiftSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ShiftDays_RecruitmentPeriodId_Date",
                table: "ShiftDays");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSubmissions_UserId",
                table: "ShiftSubmissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDays_RecruitmentPeriodId_Id",
                table: "ShiftDays",
                columns: new[] { "RecruitmentPeriodId", "Id" });
        }
    }
}
