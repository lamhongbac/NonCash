using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitWelcomePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the welcome_grant_policies table first (the seed step inserts into it).
            migrationBuilder.CreateTable(
                name: "welcome_grant_policies",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    business_id = table.Column<Guid>(type: "uuid", nullable: false),
                    welcome_credits = table.Column<int>(type: "integer", nullable: false),
                    welcome_credit_expiry_months = table.Column<int>(type: "integer", nullable: true),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_welcome_grant_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_welcome_grant_policies_businesses_business_id",
                        column: x => x.business_id,
                        principalSchema: "public",
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 2. Add the welcome_policy_id FK column to credit_batches.
            migrationBuilder.AddColumn<Guid>(
                name: "welcome_policy_id",
                schema: "public",
                table: "credit_batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_batches_welcome_policy_id",
                schema: "public",
                table: "credit_batches",
                column: "welcome_policy_id");

            migrationBuilder.CreateIndex(
                name: "IX_welcome_grant_policies_business_active_from",
                schema: "public",
                table: "welcome_grant_policies",
                columns: new[] { "business_id", "is_active", "effective_from" });

            migrationBuilder.AddForeignKey(
                name: "FK_credit_batches_welcome_grant_policies_welcome_policy_id",
                schema: "public",
                table: "credit_batches",
                column: "welcome_policy_id",
                principalSchema: "public",
                principalTable: "welcome_grant_policies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // 3. SEED — migrate brand-scoped welcome credits into business-scoped welcome
            //    policies. Must run BEFORE the DropColumn calls below destroy the source data.
            //    scope = 2 = PolicyScope.Brand. Global/BrandGroup welcome defaults remain in
            //    CreditConfig (appsettings) as the fallback when no business policy exists.
            migrationBuilder.Sql(@"
                INSERT INTO welcome_grant_policies
                    (id, name, business_id, welcome_credits, welcome_credit_expiry_months,
                     effective_from, effective_to, is_active, created_by, created_at, updated_at)
                SELECT
                    gen_random_uuid(),
                    'Migrated: ' || p.name,
                    b.business_id,
                    p.welcome_credits,
                    p.welcome_credit_expiry_months,
                    COALESCE(p.effective_from, p.created_at),
                    p.effective_to,
                    p.is_active,
                    p.created_by,
                    NOW(),
                    NOW()
                FROM credit_pricing_policies p
                JOIN brands b ON b.id = p.brand_id
                WHERE p.scope = 2
                  AND p.welcome_credits > 0
                  AND b.business_id IS NOT NULL;
            ");

            // 4. Drop the welcome columns from credit_pricing_policies
            //    (data preserved in welcome_grant_policies by the seed above).
            migrationBuilder.DropColumn(
                name: "welcome_credit_expiry_months",
                schema: "public",
                table: "credit_pricing_policies");

            migrationBuilder.DropColumn(
                name: "welcome_credits",
                schema: "public",
                table: "credit_pricing_policies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_credit_batches_welcome_grant_policies_welcome_policy_id",
                schema: "public",
                table: "credit_batches");

            migrationBuilder.DropTable(
                name: "welcome_grant_policies",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_credit_batches_welcome_policy_id",
                schema: "public",
                table: "credit_batches");

            migrationBuilder.DropColumn(
                name: "welcome_policy_id",
                schema: "public",
                table: "credit_batches");

            migrationBuilder.AddColumn<int>(
                name: "welcome_credit_expiry_months",
                schema: "public",
                table: "credit_pricing_policies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "welcome_credits",
                schema: "public",
                table: "credit_pricing_policies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
