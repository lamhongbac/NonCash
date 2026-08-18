using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BusinessRegistrationRequestContractWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the table to reflect the domain (businesses register, not brands).
            migrationBuilder.RenameTable(
                name: "brand_registration_requests",
                schema: "public",
                newName: "business_registration_requests");

            // Add contract workflow columns.
            migrationBuilder.AddColumn<string>(
                name: "contract_status",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTime>(
                name: "contract_sent_at",
                schema: "public",
                table: "business_registration_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_file_url",
                schema: "public",
                table: "business_registration_requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "welcome_policy_template_id",
                schema: "public",
                table: "business_registration_requests",
                type: "uuid",
                nullable: true);

            // Add FK to welcome_grant_policy_templates.
            migrationBuilder.AddForeignKey(
                name: "FK_business_registration_requests_welcome_grant_policy_templat~",
                schema: "public",
                table: "business_registration_requests",
                column: "welcome_policy_template_id",
                principalSchema: "public",
                principalTable: "welcome_grant_policy_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Add indexes.
            migrationBuilder.CreateIndex(
                name: "IX_business_registration_requests_contract_status",
                schema: "public",
                table: "business_registration_requests",
                column: "contract_status");

            migrationBuilder.CreateIndex(
                name: "IX_business_registration_requests_welcome_policy_template_id",
                schema: "public",
                table: "business_registration_requests",
                column: "welcome_policy_template_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_business_registration_requests_welcome_policy_template_id",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropIndex(
                name: "IX_business_registration_requests_contract_status",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_business_registration_requests_welcome_grant_policy_templat~",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "welcome_policy_template_id",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "contract_file_url",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "contract_sent_at",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "contract_status",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.RenameTable(
                name: "business_registration_requests",
                schema: "public",
                newName: "brand_registration_requests");
        }
    }
}
