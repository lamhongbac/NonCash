using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandCustomerMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brand_customers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    blocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    marketing_opt_out = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_customers", x => x.id);
                    table.ForeignKey(
                        name: "FK_brand_customers_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "public",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_brand_customers_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "public",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_brand_customers_brand_id",
                schema: "public",
                table: "brand_customers",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_brand_customers_brand_id_customer_id",
                schema: "public",
                table: "brand_customers",
                columns: new[] { "brand_id", "customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_brand_customers_customer_id",
                schema: "public",
                table: "brand_customers",
                column: "customer_id");

            // D1 backfill (docs/customer-action-matrix.md): attribute all existing
            // customers to the earliest-created Active brand. No-op when no brands exist.
            migrationBuilder.Sql(@"
INSERT INTO public.brand_customers (id, brand_id, customer_id, source, is_blocked, marketing_opt_out, created_at)
SELECT gen_random_uuid(), b.id, c.id, 'Import', false, false, NOW()
FROM public.customers c
CROSS JOIN (SELECT id FROM public.brands WHERE status = 'Active' ORDER BY created_at LIMIT 1) b
WHERE NOT EXISTS (
    SELECT 1 FROM public.brand_customers bc WHERE bc.brand_id = b.id AND bc.customer_id = c.id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brand_customers",
                schema: "public");
        }
    }
}
