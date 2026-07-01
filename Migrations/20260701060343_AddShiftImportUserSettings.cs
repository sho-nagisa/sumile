using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftImportUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShiftImportApiKey",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShiftPdfSearchName",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShiftPdfStaffRowNumber",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ShiftImportApiKey",
                table: "AspNetUsers",
                column: "ShiftImportApiKey",
                unique: true,
                filter: "\"ShiftImportApiKey\" IS NOT NULL AND \"ShiftImportApiKey\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ShiftImportApiKey",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ShiftImportApiKey",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ShiftPdfSearchName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ShiftPdfStaffRowNumber",
                table: "AspNetUsers");
        }
    }
}
