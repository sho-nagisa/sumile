using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sumile.Data;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260510000000_AddApplicationUserCustomIdUniqueIndex")]
    public partial class AddApplicationUserCustomIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CustomId",
                table: "AspNetUsers",
                column: "CustomId",
                unique: true,
                filter: "\"CustomId\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CustomId",
                table: "AspNetUsers");
        }
    }
}
