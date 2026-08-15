using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "password_reset_token",
                schema: "public",
                table: "user_accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "password_reset_token_expiry",
                schema: "public",
                table: "user_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_password_reset_token",
                schema: "public",
                table: "user_accounts",
                column: "password_reset_token",
                filter: "password_reset_token IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_accounts_password_reset_token",
                schema: "public",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "password_reset_token",
                schema: "public",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "password_reset_token_expiry",
                schema: "public",
                table: "user_accounts");
        }
    }
}
