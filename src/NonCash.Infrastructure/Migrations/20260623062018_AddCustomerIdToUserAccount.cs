using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdToUserAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                schema: "public",
                table: "user_accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_customer_id",
                schema: "public",
                table: "user_accounts",
                column: "customer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_accounts_customers_customer_id",
                schema: "public",
                table: "user_accounts",
                column: "customer_id",
                principalSchema: "public",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_accounts_customers_customer_id",
                schema: "public",
                table: "user_accounts");

            migrationBuilder.DropIndex(
                name: "IX_user_accounts_customer_id",
                schema: "public",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "customer_id",
                schema: "public",
                table: "user_accounts");
        }
    }
}
