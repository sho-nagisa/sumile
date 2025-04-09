using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    public partial class ValidUserType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ① デフォルト値を削除（←エラー回避）
            migrationBuilder.Sql("ALTER TABLE \"ShiftSubmissions\" ALTER COLUMN \"UserType\" DROP DEFAULT;");

            // ② 文字列 → 整数値へ変換
            migrationBuilder.Sql("UPDATE \"ShiftSubmissions\" SET \"UserType\" = '0' WHERE \"UserType\" = 'Normal';");
            migrationBuilder.Sql("UPDATE \"ShiftSubmissions\" SET \"UserType\" = '1' WHERE \"UserType\" = 'Admin';");
            migrationBuilder.Sql("UPDATE \"ShiftSubmissions\" SET \"UserType\" = '2' WHERE \"UserType\" = 'AdminUpdated';");

            // ③ 型を text → int に変換
            migrationBuilder.Sql("ALTER TABLE \"ShiftSubmissions\" ALTER COLUMN \"UserType\" TYPE integer USING \"UserType\"::integer;");

            // ④ 新しいデフォルト（整数値）を必要なら追加（例：Normal = 0）
            migrationBuilder.Sql("ALTER TABLE \"ShiftSubmissions\" ALTER COLUMN \"UserType\" SET DEFAULT 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 巻き戻し処理
            migrationBuilder.Sql("ALTER TABLE \"ShiftSubmissions\" ALTER COLUMN \"UserType\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"ShiftSubmissions\" ALTER COLUMN \"UserType\" TYPE text USING \"UserType\"::text;");

            migrationBuilder.Sql("UPDATE \"ShiftSubmissions\" SET \"UserType\" = 'Normal' WHERE \"UserType\" = '0';");
            migrationBuilder.Sql("UPDATE \"ShiftSubmissions\" SET \"UserType\" = 'Admin' WHERE \"UserType\" = '1';");
            migrationBuilder.Sql("UPDATE \"ShiftSubmissions\" SET \"UserType\" = 'AdminUpdated' WHERE \"UserType\" = '2';");

            migrationBuilder.Sql("ALTER TABLE \"ShiftSubmissions\" ALTER COLUMN \"UserType\" SET DEFAULT 'Normal';");
        }
    }
}
