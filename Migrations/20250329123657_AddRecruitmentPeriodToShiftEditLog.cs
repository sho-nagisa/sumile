using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitmentPeriodToShiftEditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecruitmentPeriodId",
                table: "ShiftSubmissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RecruitmentPeriodId",
                table: "ShiftEditLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSubmissions_RecruitmentPeriodId",
                table: "ShiftSubmissions",
                column: "RecruitmentPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftEditLogs_RecruitmentPeriodId",
                table: "ShiftEditLogs",
                column: "RecruitmentPeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftEditLogs_RecruitmentPeriods_RecruitmentPeriodId",
                table: "ShiftEditLogs",
                column: "RecruitmentPeriodId",
                principalTable: "RecruitmentPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSubmissions_RecruitmentPeriods_RecruitmentPeriodId",
                table: "ShiftSubmissions",
                column: "RecruitmentPeriodId",
                principalTable: "RecruitmentPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftEditLogs_RecruitmentPeriods_RecruitmentPeriodId",
                table: "ShiftEditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSubmissions_RecruitmentPeriods_RecruitmentPeriodId",
                table: "ShiftSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ShiftSubmissions_RecruitmentPeriodId",
                table: "ShiftSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ShiftEditLogs_RecruitmentPeriodId",
                table: "ShiftEditLogs");

            migrationBuilder.DropColumn(
                name: "RecruitmentPeriodId",
                table: "ShiftSubmissions");

            migrationBuilder.DropColumn(
                name: "RecruitmentPeriodId",
                table: "ShiftEditLogs");
        }
    }
}
