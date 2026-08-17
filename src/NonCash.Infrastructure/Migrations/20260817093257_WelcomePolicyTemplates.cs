using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WelcomePolicyTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "public",
                table: "welcome_grant_policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "welcome_grant_policy_template_id",
                schema: "public",
                table: "welcome_grant_policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "welcome_grant_policy_templates",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    welcome_credits = table.Column<int>(type: "integer", nullable: false),
                    welcome_credit_expiry_months = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_welcome_grant_policy_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_welcome_grant_policies_welcome_grant_policy_template_id",
                schema: "public",
                table: "welcome_grant_policies",
                column: "welcome_grant_policy_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_welcome_grant_policy_templates_is_default",
                schema: "public",
                table: "welcome_grant_policy_templates",
                column: "is_default",
                unique: true,
                filter: "is_default = true");

            migrationBuilder.AddForeignKey(
                name: "FK_welcome_grant_policies_welcome_grant_policy_templates_welco~",
                schema: "public",
                table: "welcome_grant_policies",
                column: "welcome_grant_policy_template_id",
                principalSchema: "public",
                principalTable: "welcome_grant_policy_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Seed the platform default template with values matching the current CreditConfig defaults.
            migrationBuilder.InsertData(
                schema: "public",
                table: "welcome_grant_policy_templates",
                columns: new[] { "id", "name", "welcome_credits", "welcome_credit_expiry_months", "is_active", "is_default", "created_by", "updated_by", "created_at", "updated_at" },
                values: new object[] { Guid.NewGuid(), "Platform Default", 500, 12, true, true, null, null, DateTime.UtcNow, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_welcome_grant_policies_welcome_grant_policy_templates_welco~",
                schema: "public",
                table: "welcome_grant_policies");

            migrationBuilder.DropTable(
                name: "welcome_grant_policy_templates",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_welcome_grant_policies_welcome_grant_policy_template_id",
                schema: "public",
                table: "welcome_grant_policies");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "public",
                table: "welcome_grant_policies");

            migrationBuilder.DropColumn(
                name: "welcome_grant_policy_template_id",
                schema: "public",
                table: "welcome_grant_policies");
        }
    }
}
