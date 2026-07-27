using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "business_id",
                schema: "public",
                table: "brands",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "businesses",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_businesses", x => x.id);
                });

            migrationBuilder.Sql(@"
                INSERT INTO public.businesses (id, business_name, tax_code, address, contact_email, phone_number, is_active, created_at, updated_at)
                SELECT (md5(random()::text || clock_timestamp()::text)::uuid), name, tax_code, '', contact_email, '', true, now(), NULL
                FROM public.brands;
            ");

            migrationBuilder.Sql(@"
                UPDATE public.brands b
                SET business_id = bu.id
                FROM public.businesses bu
                WHERE bu.tax_code = b.tax_code;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "business_id",
                schema: "public",
                table: "brands",
                type: "uuid",
                nullable: false,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_brands_business_id",
                schema: "public",
                table: "brands",
                column: "business_id");

            migrationBuilder.CreateIndex(
                name: "IX_businesses_tax_code",
                schema: "public",
                table: "businesses",
                column: "tax_code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_brands_businesses_business_id",
                schema: "public",
                table: "brands",
                column: "business_id",
                principalSchema: "public",
                principalTable: "businesses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_brands_businesses_business_id",
                schema: "public",
                table: "brands");

            migrationBuilder.DropTable(
                name: "businesses",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_brands_business_id",
                schema: "public",
                table: "brands");

            migrationBuilder.DropColumn(
                name: "business_id",
                schema: "public",
                table: "brands");
        }
    }
}
