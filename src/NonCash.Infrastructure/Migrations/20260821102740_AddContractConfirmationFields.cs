using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractConfirmationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contract_confirmation_token",
                schema: "public",
                table: "business_registration_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "contract_confirmed_at",
                schema: "public",
                table: "business_registration_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contract_confirmed_by_ip",
                schema: "public",
                table: "business_registration_requests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contract_confirmation_token",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "contract_confirmed_at",
                schema: "public",
                table: "business_registration_requests");

            migrationBuilder.DropColumn(
                name: "contract_confirmed_by_ip",
                schema: "public",
                table: "business_registration_requests");
        }
    }
}
