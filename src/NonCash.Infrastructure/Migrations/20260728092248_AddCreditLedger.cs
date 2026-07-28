using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credit_ledger_entries",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    voucher_detail_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_ledger_entries_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_credit_ledger_entries_brand_id_created_at",
                schema: "public",
                table: "credit_ledger_entries",
                columns: new[] { "brand_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_ledger_entries_voucher_detail_id",
                schema: "public",
                table: "credit_ledger_entries",
                column: "voucher_detail_id",
                unique: true,
                filter: "voucher_detail_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_ledger_entries",
                schema: "public");
        }
    }
}
