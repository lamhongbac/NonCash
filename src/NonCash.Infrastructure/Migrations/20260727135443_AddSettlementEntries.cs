using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "settlement_entries",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sponsor_brand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issuing_brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    redeem_brand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    redeem_outlet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voucher_usage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    face_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    settled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    settled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_settlement_entries_brands_issuing_brand_id",
                        column: x => x.issuing_brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_settlement_entries_brands_redeem_brand_id",
                        column: x => x.redeem_brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_settlement_entries_brands_sponsor_brand_id",
                        column: x => x.sponsor_brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_settlement_entries_outlets_redeem_outlet_id",
                        column: x => x.redeem_outlet_id,
                        principalSchema: "public",
                        principalTable: "outlets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_settlement_entries_voucher_usages_voucher_usage_id",
                        column: x => x.voucher_usage_id,
                        principalSchema: "public",
                        principalTable: "voucher_usages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_settlement_entries_created_at",
                schema: "public",
                table: "settlement_entries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_entries_issuing_brand_id",
                schema: "public",
                table: "settlement_entries",
                column: "issuing_brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_entries_redeem_brand_id",
                schema: "public",
                table: "settlement_entries",
                column: "redeem_brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_entries_redeem_outlet_id",
                schema: "public",
                table: "settlement_entries",
                column: "redeem_outlet_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_entries_sponsor_brand_id",
                schema: "public",
                table: "settlement_entries",
                column: "sponsor_brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_entries_status",
                schema: "public",
                table: "settlement_entries",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_entries_voucher_usage_id",
                schema: "public",
                table: "settlement_entries",
                column: "voucher_usage_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settlement_entries",
                schema: "public");
        }
    }
}
