using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailToUserAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "public",
                table: "user_accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                schema: "public",
                table: "user_accounts");
        }
    }
}
