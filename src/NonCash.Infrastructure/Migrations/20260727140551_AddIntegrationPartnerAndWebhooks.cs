using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationPartnerAndWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_partners",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    callback_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    api_key_prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    api_key_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    webhook_secret = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_partners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "voucher_events",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    voucher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    member_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_events_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_voucher_events_voucher_plan_details_voucher_id",
                        column: x => x.voucher_id,
                        principalSchema: "public",
                        principalTable: "voucher_plan_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "partner_brands",
                schema: "public",
                columns: table => new
                {
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partner_brands", x => new { x.partner_id, x.brand_id });
                    table.ForeignKey(
                        name: "FK_partner_brands_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_partner_brands_integration_partners_partner_id",
                        column: x => x.partner_id,
                        principalSchema: "public",
                        principalTable: "integration_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_integration_partners_partner_id",
                        column: x => x.partner_id,
                        principalSchema: "public",
                        principalTable: "integration_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_voucher_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "public",
                        principalTable: "voucher_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_partners_api_key_prefix",
                schema: "public",
                table: "integration_partners",
                column: "api_key_prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_partners_name",
                schema: "public",
                table: "integration_partners",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_partner_brands_brand_id",
                schema: "public",
                table: "partner_brands",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_events_brand_id",
                schema: "public",
                table: "voucher_events",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_events_created_at",
                schema: "public",
                table: "voucher_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_events_event_type",
                schema: "public",
                table: "voucher_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_events_member_phone",
                schema: "public",
                table: "voucher_events",
                column: "member_phone");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_events_voucher_id",
                schema: "public",
                table: "voucher_events",
                column: "voucher_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_delivered_at",
                schema: "public",
                table: "webhook_deliveries",
                column: "delivered_at");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_event_partner",
                schema: "public",
                table: "webhook_deliveries",
                columns: new[] { "event_id", "partner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_next_retry_at",
                schema: "public",
                table: "webhook_deliveries",
                column: "next_retry_at");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_partner_id",
                schema: "public",
                table: "webhook_deliveries",
                column: "partner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partner_brands",
                schema: "public");

            migrationBuilder.DropTable(
                name: "webhook_deliveries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "integration_partners",
                schema: "public");

            migrationBuilder.DropTable(
                name: "voucher_events",
                schema: "public");
        }
    }
}
