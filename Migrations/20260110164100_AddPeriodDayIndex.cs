using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodDayIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftDays_RecruitmentPeriodId",
                table: "ShiftDays");

            migrationBuilder.CreateIndex(
                name: "IX_SubmitBackups_RecruitmentPeriodId_ShiftDayId",
                table: "SubmitBackups",
                columns: new[] { "RecruitmentPeriodId", "ShiftDayId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDays_RecruitmentPeriodId_Id",
                table: "ShiftDays",
                columns: new[] { "RecruitmentPeriodId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubmitBackups_RecruitmentPeriodId_ShiftDayId",
                table: "SubmitBackups");

            migrationBuilder.DropIndex(
                name: "IX_ShiftDays_RecruitmentPeriodId_Id",
                table: "ShiftDays");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDays_RecruitmentPeriodId",
                table: "ShiftDays",
                column: "RecruitmentPeriodId");
        }
    }
}
