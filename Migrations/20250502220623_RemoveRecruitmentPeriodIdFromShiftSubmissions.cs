using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRecruitmentPeriodIdFromShiftSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSubmissions_RecruitmentPeriods_RecruitmentPeriodId",
                table: "ShiftSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ShiftSubmissions_RecruitmentPeriodId",
                table: "ShiftSubmissions");

            migrationBuilder.DropColumn(
                name: "RecruitmentPeriodId",
                table: "ShiftSubmissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecruitmentPeriodId",
                table: "ShiftSubmissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSubmissions_RecruitmentPeriodId",
                table: "ShiftSubmissions",
                column: "RecruitmentPeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSubmissions_RecruitmentPeriods_RecruitmentPeriodId",
                table: "ShiftSubmissions",
                column: "RecruitmentPeriodId",
                principalTable: "RecruitmentPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
