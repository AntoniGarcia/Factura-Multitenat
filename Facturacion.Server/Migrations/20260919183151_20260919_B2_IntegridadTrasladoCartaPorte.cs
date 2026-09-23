using Facturacion.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations;

/// <summary>
/// Relaciones de integridad para vehículo y figura de transporte. El comprobante ya tiene su
/// llave foránea desde la migración anterior; no se crea una segunda columna para él.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260919183151_20260919_B2_IntegridadTrasladoCartaPorte")]
public sealed class _20260919_B2_IntegridadTrasladoCartaPorte : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_TrasladosCartaPorte_FiguraTransporteId",
            table: "TrasladosCartaPorte",
            column: "FiguraTransporteId");

        migrationBuilder.CreateIndex(
            name: "IX_TrasladosCartaPorte_VehiculoId",
            table: "TrasladosCartaPorte",
            column: "VehiculoId");

        migrationBuilder.AddForeignKey(
            name: "FK_TrasladosCartaPorte_FigurasTransporte_FiguraTransporteId",
            table: "TrasladosCartaPorte",
            column: "FiguraTransporteId",
            principalTable: "FigurasTransporte",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_TrasladosCartaPorte_Vehiculos_VehiculoId",
            table: "TrasladosCartaPorte",
            column: "VehiculoId",
            principalTable: "Vehiculos",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_TrasladosCartaPorte_FigurasTransporte_FiguraTransporteId", table: "TrasladosCartaPorte");
        migrationBuilder.DropForeignKey(name: "FK_TrasladosCartaPorte_Vehiculos_VehiculoId", table: "TrasladosCartaPorte");
        migrationBuilder.DropIndex(name: "IX_TrasladosCartaPorte_FiguraTransporteId", table: "TrasladosCartaPorte");
        migrationBuilder.DropIndex(name: "IX_TrasladosCartaPorte_VehiculoId", table: "TrasladosCartaPorte");
    }
}
