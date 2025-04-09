using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftRoleToSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShiftRole",
                table: "ShiftSubmissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShiftRole",
                table: "ShiftSubmissions");
        }
    }
}
