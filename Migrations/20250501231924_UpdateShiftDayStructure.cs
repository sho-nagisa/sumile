using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class UpdateShiftDayStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyWorkloads_RecruitmentPeriods_RecruitmentPeriodId",
                table: "DailyWorkloads");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftEditLogs_RecruitmentPeriods_RecruitmentPeriodId",
                table: "ShiftEditLogs");

            migrationBuilder.DropIndex(
                name: "IX_DailyWorkloads_RecruitmentPeriodId",
                table: "DailyWorkloads");

            migrationBuilder.DropColumn(
                name: "ShiftDate",
                table: "ShiftEditLogs");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "DailyWorkloads");

            migrationBuilder.RenameColumn(
                name: "RecruitmentPeriodId",
                table: "ShiftEditLogs",
                newName: "ShiftDayId");

            migrationBuilder.RenameIndex(
                name: "IX_ShiftEditLogs_RecruitmentPeriodId",
                table: "ShiftEditLogs",
                newName: "IX_ShiftEditLogs_ShiftDayId");

            migrationBuilder.RenameColumn(
                name: "RecruitmentPeriodId",
                table: "DailyWorkloads",
                newName: "ShiftDayId");

            migrationBuilder.AddColumn<int>(
                name: "ShiftDayId",
                table: "ShiftSubmissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ShiftDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecruitmentPeriodId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftDays_RecruitmentPeriods_RecruitmentPeriodId",
                        column: x => x.RecruitmentPeriodId,
                        principalTable: "RecruitmentPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSubmissions_ShiftDayId",
                table: "ShiftSubmissions",
                column: "ShiftDayId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyWorkloads_ShiftDayId",
                table: "DailyWorkloads",
                column: "ShiftDayId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDays_RecruitmentPeriodId",
                table: "ShiftDays",
                column: "RecruitmentPeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyWorkloads_ShiftDays_ShiftDayId",
                table: "DailyWorkloads",
                column: "ShiftDayId",
                principalTable: "ShiftDays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftEditLogs_ShiftDays_ShiftDayId",
                table: "ShiftEditLogs",
                column: "ShiftDayId",
                principalTable: "ShiftDays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftSubmissions_ShiftDays_ShiftDayId",
                table: "ShiftSubmissions",
                column: "ShiftDayId",
                principalTable: "ShiftDays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyWorkloads_ShiftDays_ShiftDayId",
                table: "DailyWorkloads");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftEditLogs_ShiftDays_ShiftDayId",
                table: "ShiftEditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftSubmissions_ShiftDays_ShiftDayId",
                table: "ShiftSubmissions");

            migrationBuilder.DropTable(
                name: "ShiftDays");

            migrationBuilder.DropIndex(
                name: "IX_ShiftSubmissions_ShiftDayId",
                table: "ShiftSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_DailyWorkloads_ShiftDayId",
                table: "DailyWorkloads");

            migrationBuilder.DropColumn(
                name: "ShiftDayId",
                table: "ShiftSubmissions");

            migrationBuilder.RenameColumn(
                name: "ShiftDayId",
                table: "ShiftEditLogs",
                newName: "RecruitmentPeriodId");

            migrationBuilder.RenameIndex(
                name: "IX_ShiftEditLogs_ShiftDayId",
                table: "ShiftEditLogs",
                newName: "IX_ShiftEditLogs_RecruitmentPeriodId");

            migrationBuilder.RenameColumn(
                name: "ShiftDayId",
                table: "DailyWorkloads",
                newName: "RecruitmentPeriodId");

            migrationBuilder.AddColumn<DateTime>(
                name: "ShiftDate",
                table: "ShiftEditLogs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "DailyWorkloads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_DailyWorkloads_RecruitmentPeriodId",
                table: "DailyWorkloads",
                column: "RecruitmentPeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyWorkloads_RecruitmentPeriods_RecruitmentPeriodId",
                table: "DailyWorkloads",
                column: "RecruitmentPeriodId",
                principalTable: "RecruitmentPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftEditLogs_RecruitmentPeriods_RecruitmentPeriodId",
                table: "ShiftEditLogs",
                column: "RecruitmentPeriodId",
                principalTable: "RecruitmentPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
