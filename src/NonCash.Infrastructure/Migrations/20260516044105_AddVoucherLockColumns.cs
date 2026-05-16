using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherLockColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bill_number",
                schema: "public",
                table: "voucher_plan_details",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lock_id",
                schema: "public",
                table: "voucher_plan_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "locked_at",
                schema: "public",
                table: "voucher_plan_details",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "locked_outlet_id",
                schema: "public",
                table: "voucher_plan_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_details_lock_id",
                schema: "public",
                table: "voucher_plan_details",
                column: "lock_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_voucher_plan_details_lock_id",
                schema: "public",
                table: "voucher_plan_details");

            migrationBuilder.DropColumn(
                name: "bill_number",
                schema: "public",
                table: "voucher_plan_details");

            migrationBuilder.DropColumn(
                name: "lock_id",
                schema: "public",
                table: "voucher_plan_details");

            migrationBuilder.DropColumn(
                name: "locked_at",
                schema: "public",
                table: "voucher_plan_details");

            migrationBuilder.DropColumn(
                name: "locked_outlet_id",
                schema: "public",
                table: "voucher_plan_details");
        }
    }
}
