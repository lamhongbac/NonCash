using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixVoucherTransferFkToUserAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_voucher_transfers_customers_recipient_id",
                schema: "public",
                table: "voucher_transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_transfers_customers_sender_id",
                schema: "public",
                table: "voucher_transfers");

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_transfers_user_accounts_recipient_id",
                schema: "public",
                table: "voucher_transfers",
                column: "recipient_id",
                principalSchema: "public",
                principalTable: "user_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_transfers_user_accounts_sender_id",
                schema: "public",
                table: "voucher_transfers",
                column: "sender_id",
                principalSchema: "public",
                principalTable: "user_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_voucher_transfers_user_accounts_recipient_id",
                schema: "public",
                table: "voucher_transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_voucher_transfers_user_accounts_sender_id",
                schema: "public",
                table: "voucher_transfers");

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_transfers_customers_recipient_id",
                schema: "public",
                table: "voucher_transfers",
                column: "recipient_id",
                principalSchema: "public",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_voucher_transfers_customers_sender_id",
                schema: "public",
                table: "voucher_transfers",
                column: "sender_id",
                principalSchema: "public",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
