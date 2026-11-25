using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemByte.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SingleInstallationCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            EnsureEmptyCatalog(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_CatalogCategories_Clients_ClientId",
                table: "CatalogCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_CatalogItems_Clients_ClientId",
                table: "CatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_ClientId_Name",
                table: "CatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_CatalogCategories_ClientId_Name",
                table: "CatalogCategories");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "CatalogCategories");

            migrationBuilder.AddColumn<string>(
                name: "BillingPeriod",
                table: "CatalogItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "CatalogItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryDays",
                table: "CatalogItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryMode",
                table: "CatalogItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "CatalogItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Supplier",
                table: "CatalogItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "CatalogItems",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasure",
                table: "CatalogItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_Name",
                table: "CatalogItems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCategories_Name",
                table: "CatalogCategories",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            EnsureEmptyCatalog(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_Name",
                table: "CatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_CatalogCategories_Name",
                table: "CatalogCategories");

            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "DeliveryDays",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "DeliveryMode",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "Supplier",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                table: "CatalogItems");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "CatalogItems",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "CatalogCategories",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_ClientId_Name",
                table: "CatalogItems",
                columns: new[] { "ClientId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCategories_ClientId_Name",
                table: "CatalogCategories",
                columns: new[] { "ClientId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogCategories_Clients_ClientId",
                table: "CatalogCategories",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogItems_Clients_ClientId",
                table: "CatalogItems",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        private static void EnsureEmptyCatalog(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE "CatalogItems", "CatalogCategories" IN ACCESS EXCLUSIVE MODE;
                DO $catalog$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "CatalogItems") OR EXISTS (SELECT 1 FROM "CatalogCategories") THEN
                        RAISE EXCEPTION 'La conversión o reversión del catálogo requiere un catálogo vacío. Prepare una migración de datos específica antes de continuar.';
                    END IF;
                END;
                $catalog$;
                """);
        }
    }
}
