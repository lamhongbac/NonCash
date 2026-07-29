using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditPolicyBatchAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brand_groups",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "brand_group_members",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_group_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_brand_group_members_brand_groups_brand_group_id",
                        column: x => x.brand_group_id,
                        principalSchema: "public",
                        principalTable: "brand_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_brand_group_members_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_pricing_policies",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    brand_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    price_per_credit_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    credit_expiry_months = table.Column<int>(type: "integer", nullable: true),
                    welcome_credits = table.Column<int>(type: "integer", nullable: false),
                    welcome_credit_expiry_months = table.Column<int>(type: "integer", nullable: true),
                    low_balance_warning_pct = table.Column<int>(type: "integer", nullable: true),
                    expiry_warning_days = table.Column<int>(type: "integer", nullable: true),
                    adjustment_approval_threshold = table.Column<int>(type: "integer", nullable: true),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_pricing_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_pricing_policies_brand_groups_brand_group_id",
                        column: x => x.brand_group_id,
                        principalSchema: "public",
                        principalTable: "brand_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_pricing_policies_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_adjustment_requests",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjustment_type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    related_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    evidence_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    evidence_image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    approval_threshold = table.Column<int>(type: "integer", nullable: true),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_adjustment_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_adjustment_requests_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_adjustment_requests_credit_pricing_policies_policy_id",
                        column: x => x.policy_id,
                        principalSchema: "public",
                        principalTable: "credit_pricing_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_batches",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_type = table.Column<int>(type: "integer", nullable: false),
                    original_amount = table.Column<int>(type: "integer", nullable: false),
                    remaining_amount = table.Column<int>(type: "integer", nullable: false),
                    price_per_credit_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_paid_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    evidence_image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    adjustment_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_batches_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_batches_credit_adjustment_requests_adjustment_reques~",
                        column: x => x.adjustment_request_id,
                        principalSchema: "public",
                        principalTable: "credit_adjustment_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_batches_credit_pricing_policies_policy_id",
                        column: x => x.policy_id,
                        principalSchema: "public",
                        principalTable: "credit_pricing_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_consumptions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_consumptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_consumptions_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_consumptions_credit_batches_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "public",
                        principalTable: "credit_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_expiry_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expired_credits = table.Column<int>(type: "integer", nullable: false),
                    expired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_expiry_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_expiry_logs_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_credit_expiry_logs_credit_batches_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "public",
                        principalTable: "credit_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_brand_group_members_brand_id",
                schema: "public",
                table: "brand_group_members",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_brand_group_members_group_brand",
                schema: "public",
                table: "brand_group_members",
                columns: new[] { "brand_group_id", "brand_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_brand_groups_name",
                schema: "public",
                table: "brand_groups",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_adjustment_requests_brand_id_created_at",
                schema: "public",
                table: "credit_adjustment_requests",
                columns: new[] { "brand_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_adjustment_requests_policy_id",
                schema: "public",
                table: "credit_adjustment_requests",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_adjustment_requests_related_batch_id",
                schema: "public",
                table: "credit_adjustment_requests",
                column: "related_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_adjustment_requests_status_requested_at",
                schema: "public",
                table: "credit_adjustment_requests",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_batches_adjustment_request_id",
                schema: "public",
                table: "credit_batches",
                column: "adjustment_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_batches_brand_id_created_at",
                schema: "public",
                table: "credit_batches",
                columns: new[] { "brand_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_batches_brand_id_expires_at",
                schema: "public",
                table: "credit_batches",
                columns: new[] { "brand_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_batches_policy_id",
                schema: "public",
                table: "credit_batches",
                column: "policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_consumptions_batch_id",
                schema: "public",
                table: "credit_consumptions",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_consumptions_brand_id_created_at",
                schema: "public",
                table: "credit_consumptions",
                columns: new[] { "brand_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_consumptions_voucher_detail_id",
                schema: "public",
                table: "credit_consumptions",
                column: "voucher_detail_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_expiry_logs_batch_id",
                schema: "public",
                table: "credit_expiry_logs",
                column: "batch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_expiry_logs_brand_id",
                schema: "public",
                table: "credit_expiry_logs",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_pricing_policies_brand_group_id",
                schema: "public",
                table: "credit_pricing_policies",
                column: "brand_group_id",
                filter: "brand_group_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_credit_pricing_policies_brand_id",
                schema: "public",
                table: "credit_pricing_policies",
                column: "brand_id",
                filter: "brand_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_credit_pricing_policies_scope_active_from",
                schema: "public",
                table: "credit_pricing_policies",
                columns: new[] { "scope", "is_active", "effective_from" });

            migrationBuilder.AddForeignKey(
                name: "FK_credit_adjustment_requests_credit_batches_related_batch_id",
                schema: "public",
                table: "credit_adjustment_requests",
                column: "related_batch_id",
                principalSchema: "public",
                principalTable: "credit_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_credit_pricing_policies_brand_groups_brand_group_id",
                schema: "public",
                table: "credit_pricing_policies");

            migrationBuilder.DropForeignKey(
                name: "FK_credit_adjustment_requests_credit_batches_related_batch_id",
                schema: "public",
                table: "credit_adjustment_requests");

            migrationBuilder.DropTable(
                name: "brand_group_members",
                schema: "public");

            migrationBuilder.DropTable(
                name: "credit_consumptions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "credit_expiry_logs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "brand_groups",
                schema: "public");

            migrationBuilder.DropTable(
                name: "credit_batches",
                schema: "public");

            migrationBuilder.DropTable(
                name: "credit_adjustment_requests",
                schema: "public");

            migrationBuilder.DropTable(
                name: "credit_pricing_policies",
                schema: "public");
        }
    }
}
