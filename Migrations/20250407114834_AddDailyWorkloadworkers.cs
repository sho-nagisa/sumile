using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyWorkloadworkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyWorkloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false),
                    RequiredWorkers = table.Column<int>(type: "integer", nullable: false),
                    RecruitmentPeriodId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyWorkloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyWorkloads_RecruitmentPeriods_RecruitmentPeriodId",
                        column: x => x.RecruitmentPeriodId,
                        principalTable: "RecruitmentPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyWorkloads_RecruitmentPeriodId",
                table: "DailyWorkloads",
                column: "RecruitmentPeriodId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyWorkloads");
        }
    }
}
