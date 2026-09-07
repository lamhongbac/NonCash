using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCR18StoreStaffAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "operator_id",
                schema: "public",
                table: "voucher_usages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pos_no",
                schema: "public",
                table: "voucher_usages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                schema: "public",
                table: "outlets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // customer_audit_logs table already exists (applied by AddCustomerAuditLogs migration).

            migrationBuilder.CreateTable(
                name: "user_outlet_assignments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outlet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_outlet_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_outlet_assignments_outlets_outlet_id",
                        column: x => x.outlet_id,
                        principalSchema: "public",
                        principalTable: "outlets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_outlet_assignments_user_accounts_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outlets_brand_id_code",
                schema: "public",
                table: "outlets",
                columns: new[] { "brand_id", "code" },
                unique: true,
                filter: "code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_outlet_assignments_outlet_id",
                schema: "public",
                table: "user_outlet_assignments",
                column: "outlet_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_outlet_assignments_user_id_outlet_id",
                schema: "public",
                table: "user_outlet_assignments",
                columns: new[] { "user_id", "outlet_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_outlet_assignments",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_outlets_brand_id_code",
                schema: "public",
                table: "outlets");

            migrationBuilder.DropColumn(
                name: "operator_id",
                schema: "public",
                table: "voucher_usages");

            migrationBuilder.DropColumn(
                name: "pos_no",
                schema: "public",
                table: "voucher_usages");

            migrationBuilder.DropColumn(
                name: "code",
                schema: "public",
                table: "outlets");
        }
    }
}
