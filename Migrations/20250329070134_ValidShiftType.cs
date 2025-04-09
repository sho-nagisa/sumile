using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class ValidShiftType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ShiftSubmissions: 文字列を整数に変換
            migrationBuilder.Sql(@"
                UPDATE ""ShiftSubmissions""
                SET ""ShiftType"" = CASE ""ShiftType""
                    WHEN 'Morning' THEN '0'
                    WHEN 'Night' THEN '1'
                    ELSE '0'
                END;
            ");

            // ShiftEditLogs: 同様に変換
            migrationBuilder.Sql(@"
                UPDATE ""ShiftEditLogs""
                SET ""ShiftType"" = CASE ""ShiftType""
                    WHEN 'Morning' THEN '0'
                    WHEN 'Night' THEN '1'
                    ELSE '0'
                END;
            ");

            // 型変換（USING句を使用）
            migrationBuilder.Sql(@"
                ALTER TABLE ""ShiftSubmissions"" 
                ALTER COLUMN ""ShiftType"" TYPE integer 
                USING ""ShiftType""::integer;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""ShiftEditLogs"" 
                ALTER COLUMN ""ShiftType"" TYPE integer 
                USING ""ShiftType""::integer;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ShiftType",
                table: "ShiftSubmissions",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ShiftType",
                table: "ShiftEditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
