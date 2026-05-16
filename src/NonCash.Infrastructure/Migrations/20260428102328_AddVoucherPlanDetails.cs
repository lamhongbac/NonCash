using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherPlanDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voucher_plan_details",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    voucher_code_secret = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usage_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    used_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voucher_plan_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_voucher_plan_details_voucher_plan_headers_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "public",
                        principalTable: "voucher_plan_headers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_details_member_id",
                schema: "public",
                table: "voucher_plan_details",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_details_parent_id",
                schema: "public",
                table: "voucher_plan_details",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_voucher_plan_details_serial_no",
                schema: "public",
                table: "voucher_plan_details",
                column: "serial_no",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voucher_plan_details",
                schema: "public");
        }
    }
}
