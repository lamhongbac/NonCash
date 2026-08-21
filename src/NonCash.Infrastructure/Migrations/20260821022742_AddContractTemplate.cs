using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "contract_template_id",
                schema: "public",
                table: "business_registration_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contract_templates",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    html_template = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_registration_requests_contract_template_id",
                schema: "public",
                table: "business_registration_requests",
                column: "contract_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_templates_is_default",
                schema: "public",
                table: "contract_templates",
                column: "is_default",
                unique: true,
                filter: "is_default = true");

            migrationBuilder.AddForeignKey(
                name: "FK_business_registration_requests_contract_templates_contract_~",
                schema: "public",
                table: "business_registration_requests",
                column: "contract_template_id",
                principalSchema: "public",
                principalTable: "contract_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_business_registration_requests_contract_templates_contract_~",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropTable(
                name: "contract_templates",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_business_registration_requests_contract_template_id",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "contract_template_id",
                schema: "public",
                table: "business_registration_requests");
        }
    }
}
