using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditConsumptionPlanCharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_credit_consumptions_voucher_detail_id",
                schema: "public",
                table: "credit_consumptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "voucher_detail_id",
                schema: "public",
                table: "credit_consumptions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "batch_id",
                schema: "public",
                table: "credit_consumptions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "plan_id",
                schema: "public",
                table: "credit_consumptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quantity",
                schema: "public",
                table: "credit_consumptions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_credit_consumptions_plan_id",
                schema: "public",
                table: "credit_consumptions",
                column: "plan_id",
                unique: true,
                filter: "\"plan_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_credit_consumptions_voucher_detail_id",
                schema: "public",
                table: "credit_consumptions",
                column: "voucher_detail_id",
                unique: true,
                filter: "\"voucher_detail_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_credit_consumptions_plan_id",
                schema: "public",
                table: "credit_consumptions");

            migrationBuilder.DropIndex(
                name: "IX_credit_consumptions_voucher_detail_id",
                schema: "public",
                table: "credit_consumptions");

            migrationBuilder.DropColumn(
                name: "plan_id",
                schema: "public",
                table: "credit_consumptions");

            migrationBuilder.DropColumn(
                name: "quantity",
                schema: "public",
                table: "credit_consumptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "voucher_detail_id",
                schema: "public",
                table: "credit_consumptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "batch_id",
                schema: "public",
                table: "credit_consumptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_consumptions_voucher_detail_id",
                schema: "public",
                table: "credit_consumptions",
                column: "voucher_detail_id",
                unique: true);
        }
    }
}
