using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_transactions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    gateway_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    request_payload = table.Column<string>(type: "text", nullable: true),
                    response_payload = table.Column<string>(type: "text", nullable: true),
                    webhook_payload = table.Column<string>(type: "text", nullable: true),
                    gateway_response_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalSchema: "public",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_gateway_transaction_id",
                schema: "public",
                table: "payment_transactions",
                column: "gateway_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_purchase_order_id",
                schema: "public",
                table: "payment_transactions",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_status",
                schema: "public",
                table: "payment_transactions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions",
                schema: "public");
        }
    }
}
