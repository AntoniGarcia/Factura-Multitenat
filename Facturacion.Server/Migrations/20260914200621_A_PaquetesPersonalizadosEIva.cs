using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_PaquetesPersonalizadosEIva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Paquetes_Activo_Orden",
                table: "Paquetes");

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Paquetes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Iva",
                table: "ComprasTimbres",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "ComprasTimbres",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TasaIva",
                table: "ComprasTimbres",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            // Los precios ya publicados incluían IVA. Se congela el desglose de las compras
            // existentes con la misma regla de seis decimales que usa el servidor.
            migrationBuilder.Sql("""
                UPDATE ComprasTimbres
                SET TasaIva = 0.16,
                    Subtotal = ROUND(PrecioTotal / 1.16, 6),
                    Iva = PrecioTotal - ROUND(PrecioTotal / 1.16, 6)
                """);

            migrationBuilder.UpdateData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000001"),
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000002"),
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000006"),
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000007"),
                column: "EmpresaId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Paquetes",
                keyColumn: "Id",
                keyValue: new Guid("9c1f0a10-0000-4000-8000-000000000008"),
                column: "EmpresaId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Paquetes_EmpresaId_Activo_Orden",
                table: "Paquetes",
                columns: new[] { "EmpresaId", "Activo", "Orden" });

            migrationBuilder.AddForeignKey(
                name: "FK_Paquetes_Empresas_EmpresaId",
                table: "Paquetes",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Paquetes_Empresas_EmpresaId",
                table: "Paquetes");

            migrationBuilder.DropIndex(
                name: "IX_Paquetes_EmpresaId_Activo_Orden",
                table: "Paquetes");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Paquetes");

            migrationBuilder.DropColumn(
                name: "Iva",
                table: "ComprasTimbres");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "ComprasTimbres");

            migrationBuilder.DropColumn(
                name: "TasaIva",
                table: "ComprasTimbres");

            migrationBuilder.CreateIndex(
                name: "IX_Paquetes_Activo_Orden",
                table: "Paquetes",
                columns: new[] { "Activo", "Orden" });
        }
    }
}
