using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sumile.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtToShiftExchange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_AspNetUsers_AcceptedByUserId",
                table: "ShiftExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_ShiftAssignmentId",
                table: "ShiftExchanges");

            migrationBuilder.DropIndex(
                name: "IX_ShiftExchanges_ShiftAssignmentId",
                table: "ShiftExchanges");

            migrationBuilder.DropColumn(
                name: "ShiftAssignmentId",
                table: "ShiftExchanges");

            migrationBuilder.AlterColumn<string>(
                name: "AcceptedByUserId",
                table: "ShiftExchanges",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "ShiftExchanges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ShiftExchanges",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftExchanges_UserId",
                table: "ShiftExchanges",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_AspNetUsers_AcceptedByUserId",
                table: "ShiftExchanges",
                column: "AcceptedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_AspNetUsers_UserId",
                table: "ShiftExchanges",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_AspNetUsers_AcceptedByUserId",
                table: "ShiftExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftExchanges_AspNetUsers_UserId",
                table: "ShiftExchanges");

            migrationBuilder.DropIndex(
                name: "IX_ShiftExchanges_UserId",
                table: "ShiftExchanges");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "ShiftExchanges");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ShiftExchanges");

            migrationBuilder.AlterColumn<string>(
                name: "AcceptedByUserId",
                table: "ShiftExchanges",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShiftAssignmentId",
                table: "ShiftExchanges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftExchanges_ShiftAssignmentId",
                table: "ShiftExchanges",
                column: "ShiftAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_AspNetUsers_AcceptedByUserId",
                table: "ShiftExchanges",
                column: "AcceptedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftExchanges_ShiftAssignments_ShiftAssignmentId",
                table: "ShiftExchanges",
                column: "ShiftAssignmentId",
                principalTable: "ShiftAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
