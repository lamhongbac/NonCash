using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherPlanHeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voucher_plan_headers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    icon_url = table.Column<string>(type: "text", nullable: true),
                    value_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    face_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    net_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    publish_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    target_quantity = table.Column<int>(type: "integer", nullable: false),
                    budget = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    target_distributed = table.Column<int>(type: "integer", nullable: false),
                    target_used = table.Column<int>(type: "integer", nullable: false),
                    approval_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_plan_headers", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_plan_headers_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_voucher_plan_headers_user_accounts_approver_id",
                        column: x => x.approver_id,
                        principalSchema: "public",
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_plan_headers_user_accounts_creator_id",
                        column: x => x.creator_id,
                        principalSchema: "public",
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_outlets",
                schema: "public",
                columns: table => new
                {
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outlet_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_outlets", x => new { x.plan_id, x.outlet_id });
                    table.ForeignKey(
                        name: "FK_plan_outlets_outlets_outlet_id",
                        column: x => x.outlet_id,
                        principalSchema: "public",
                        principalTable: "outlets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_plan_outlets_voucher_plan_headers_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "public",
                        principalTable: "voucher_plan_headers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plan_outlets_outlet_id",
                schema: "public",
                table: "plan_outlets",
                column: "outlet_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_headers_approval_status",
                schema: "public",
                table: "voucher_plan_headers",
                column: "approval_status");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_headers_approver_id",
                schema: "public",
                table: "voucher_plan_headers",
                column: "approver_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_headers_brand_id",
                schema: "public",
                table: "voucher_plan_headers",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_headers_creator_id",
                schema: "public",
                table: "voucher_plan_headers",
                column: "creator_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_outlets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "voucher_plan_headers",
                schema: "public");
        }
    }
}
