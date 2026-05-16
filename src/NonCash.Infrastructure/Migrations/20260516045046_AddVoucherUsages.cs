using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voucher_usages",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pos_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    usage_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount_used = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_usages", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_usages_voucher_plan_details_voucher_id",
                        column: x => x.voucher_id,
                        principalSchema: "public",
                        principalTable: "voucher_plan_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_usages_pos_id",
                schema: "public",
                table: "voucher_usages",
                column: "pos_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_usages_transaction_id",
                schema: "public",
                table: "voucher_usages",
                column: "transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_voucher_usages_voucher_id",
                schema: "public",
                table: "voucher_usages",
                column: "voucher_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_usages",
                schema: "public");
        }
    }
}
