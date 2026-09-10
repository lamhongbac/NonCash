using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropMemberAccountUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_member_accounts_username",
                schema: "public",
                table: "member_accounts");

            migrationBuilder.DropColumn(
                name: "username",
                schema: "public",
                table: "member_accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "username",
                schema: "public",
                table: "member_accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_member_accounts_username",
                schema: "public",
                table: "member_accounts",
                column: "username",
                unique: true);
        }
    }
}
