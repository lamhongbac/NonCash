using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    template_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    notification_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_notification_type",
                schema: "public",
                table: "email_logs",
                column: "notification_type");

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_sent_at",
                schema: "public",
                table: "email_logs",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "IX_email_logs_success",
                schema: "public",
                table: "email_logs",
                column: "success");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_logs",
                schema: "public");
        }
    }
}
