using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherScopeJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Epic 3: add the jsonb "scope" column FIRST (with a valid empty-scope default so the
            // NOT NULL ALTER succeeds on existing rows), then backfill it from the legacy
            // plan_outlets + owning brand, and only THEN drop plan_outlets. Order matters: the
            // backfill must read plan_outlets before it is dropped.
            migrationBuilder.AddColumn<string>(
                name: "scope",
                schema: "public",
                table: "voucher_plan_headers",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{\"Companies\":[],\"Brands\":[],\"Outlets\":[]}'::jsonb");

            // PascalCase keys match System.Text.Json "General" serialization of VoucherScope.
            migrationBuilder.Sql(@"
UPDATE public.voucher_plan_headers p
SET scope = jsonb_build_object(
    'Companies', COALESCE((SELECT jsonb_agg(b.business_id) FROM public.brands b WHERE b.id = p.brand_id), '[]'::jsonb),
    'Brands',    jsonb_build_array(p.brand_id),
    'Outlets',   COALESCE((SELECT jsonb_agg(po.outlet_id) FROM public.plan_outlets po WHERE po.plan_id = p.id), '[]'::jsonb)
);");

            migrationBuilder.DropTable(
                name: "plan_outlets",
                schema: "public");

            // The default only existed to satisfy NOT NULL during the ALTER; drop it so the column
            // matches the EF model (no default) and every insert carries an explicit scope.
            migrationBuilder.Sql(@"
ALTER TABLE public.voucher_plan_headers ALTER COLUMN scope DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            // Restore outlet associations from the jsonb scope before dropping the column
            // (Companies/Brands are new in Epic 3 and have no legacy table to restore into).
            migrationBuilder.Sql(@"
INSERT INTO public.plan_outlets (plan_id, outlet_id)
SELECT p.id, (o.value)::uuid
FROM public.voucher_plan_headers p
CROSS JOIN LATERAL jsonb_array_elements_text(p.scope -> 'Outlets') AS o(value);");

            migrationBuilder.DropColumn(
                name: "scope",
                schema: "public",
                table: "voucher_plan_headers");
        }
    }
}
