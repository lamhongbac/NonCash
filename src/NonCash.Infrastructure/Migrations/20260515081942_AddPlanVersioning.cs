using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "previous_version_id",
                schema: "public",
                table: "voucher_plan_headers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version_number",
                schema: "public",
                table: "voucher_plan_headers",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_headers_previous_version_id",
                schema: "public",
                table: "voucher_plan_headers",
                column: "previous_version_id");

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_plan_headers_voucher_plan_headers_previous_version_~",
                schema: "public",
                table: "voucher_plan_headers",
                column: "previous_version_id",
                principalSchema: "public",
                principalTable: "voucher_plan_headers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_voucher_plan_headers_voucher_plan_headers_previous_version_~",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropIndex(
                name: "IX_voucher_plan_headers_previous_version_id",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "previous_version_id",
                schema: "public",
                table: "voucher_plan_headers");

            migrationBuilder.DropColumn(
                name: "version_number",
                schema: "public",
                table: "voucher_plan_headers");
        }
    }
}
