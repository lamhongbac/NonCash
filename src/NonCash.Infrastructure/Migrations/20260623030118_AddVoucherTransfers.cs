using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "transfer_lock_id",
                schema: "public",
                table: "voucher_plan_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "transfer_locked_at",
                schema: "public",
                table: "voucher_plan_details",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "voucher_transfers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    transfer_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    initiated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_transfers", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_transfers_customers_recipient_id",
                        column: x => x.recipient_id,
                        principalSchema: "public",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_transfers_customers_sender_id",
                        column: x => x.sender_id,
                        principalSchema: "public",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_transfers_voucher_plan_details_voucher_id",
                        column: x => x.voucher_id,
                        principalSchema: "public",
                        principalTable: "voucher_plan_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_details_transfer_lock_id",
                schema: "public",
                table: "voucher_plan_details",
                column: "transfer_lock_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_transfers_expires_at",
                schema: "public",
                table: "voucher_transfers",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_transfers_recipient_id",
                schema: "public",
                table: "voucher_transfers",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_transfers_sender_id",
                schema: "public",
                table: "voucher_transfers",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_transfers_status",
                schema: "public",
                table: "voucher_transfers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_transfers_voucher_id",
                schema: "public",
                table: "voucher_transfers",
                column: "voucher_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_transfers",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_voucher_plan_details_transfer_lock_id",
                schema: "public",
                table: "voucher_plan_details");

            migrationBuilder.DropColumn(
                name: "transfer_lock_id",
                schema: "public",
                table: "voucher_plan_details");

            migrationBuilder.DropColumn(
                name: "transfer_locked_at",
                schema: "public",
                table: "voucher_plan_details");
        }
    }
}
