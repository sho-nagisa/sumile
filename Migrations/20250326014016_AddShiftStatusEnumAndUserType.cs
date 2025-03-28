using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftStatusEnumAndUserType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ShiftStatus 列を一旦削除
            migrationBuilder.DropColumn(
                name: "ShiftStatus",
                table: "ShiftSubmissions");

            // ShiftStatus を int型(enum用)として再追加（default: 2 = NotAccepted）
            migrationBuilder.AddColumn<int>(
                name: "ShiftStatus",
                table: "ShiftSubmissions",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            // UserType を新規追加
            migrationBuilder.AddColumn<string>(
                name: "UserType",
                table: "ShiftSubmissions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // UserType を削除
            migrationBuilder.DropColumn(
                name: "UserType",
                table: "ShiftSubmissions");

            // ShiftStatus を int → string に戻すため、一度削除して再作成
            migrationBuilder.DropColumn(
                name: "ShiftStatus",
                table: "ShiftSubmissions");

            migrationBuilder.AddColumn<string>(
                name: "ShiftStatus",
                table: "ShiftSubmissions",
                type: "text",
                nullable: false,
                defaultValue: "NotAccepted");
        }
    }
}
