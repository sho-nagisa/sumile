using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    public partial class FixUserShiftRole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ 「AspNetUsers」テーブルへの ShiftRole 追加のみ残す
            migrationBuilder.AddColumn<int>(
                name: "ShiftRole",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 元に戻すときは ShiftRole を削除
            migrationBuilder.DropColumn(
                name: "ShiftRole",
                table: "AspNetUsers");
        }
    }
}
