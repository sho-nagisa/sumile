using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class UpdateShiftExchangeModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcceptedShiftSubmissionId",
                table: "ShiftExchanges",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfferedShiftSubmissionId",
                table: "ShiftExchanges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftExchanges_AcceptedShiftSubmissionId",
                table: "ShiftExchanges",
                column: "AcceptedShiftSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftExchanges_OfferedShiftSubmissionId",
                table: "ShiftExchanges",
                column: "OfferedShiftSubmissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_ShiftSubmissions_AcceptedShiftSubmissionId",
                table: "ShiftExchanges",
                column: "AcceptedShiftSubmissionId",
                principalTable: "ShiftSubmissions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_ShiftSubmissions_OfferedShiftSubmissionId",
                table: "ShiftExchanges",
                column: "OfferedShiftSubmissionId",
                principalTable: "ShiftSubmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_ShiftSubmissions_AcceptedShiftSubmissionId",
                table: "ShiftExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_ShiftSubmissions_OfferedShiftSubmissionId",
                table: "ShiftExchanges");

            migrationBuilder.DropIndex(
                name: "IX_ShiftExchanges_AcceptedShiftSubmissionId",
                table: "ShiftExchanges");

            migrationBuilder.DropIndex(
                name: "IX_ShiftExchanges_OfferedShiftSubmissionId",
                table: "ShiftExchanges");

            migrationBuilder.DropColumn(
                name: "AcceptedShiftSubmissionId",
                table: "ShiftExchanges");

            migrationBuilder.DropColumn(
                name: "OfferedShiftSubmissionId",
                table: "ShiftExchanges");
        }
    }
}
