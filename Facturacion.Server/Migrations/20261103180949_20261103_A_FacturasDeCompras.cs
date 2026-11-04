using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_A_FacturasDeCompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacturasDeCompras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CuentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmisorRfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    EmisorNombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    EmisorRegimen = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EmisorCodigoPostal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    EmisorZonaHoraria = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Serie = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    VersionProveedor = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: false),
                    ReceptorRfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    ReceptorNombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    ReceptorRegimen = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ReceptorCodigoPostal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    UsoCfdi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CantidadTimbres = table.Column<int>(type: "int", nullable: false),
                    ClaveProdServ = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ClaveUnidad = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Iva = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TasaIva = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    FormaPago = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadaPorOperadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualizadaPorOperadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasDeCompras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturasDeCompras_ComprasTimbres_CompraId",
                        column: x => x.CompraId,
                        principalTable: "ComprasTimbres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasDeCompras_CompraId",
                table: "FacturasDeCompras",
                column: "CompraId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturasDeCompras_CuentaId_CreadaUtc",
                table: "FacturasDeCompras",
                columns: new[] { "CuentaId", "CreadaUtc" });

            migrationBuilder.Sql("""
                INSERT INTO dbo.OperadoresPermisos (OperadorId, Permiso)
                SELECT o.Id, N'panel_facturar_compras' FROM dbo.OperadoresPlataforma o
                WHERE o.EsPrincipal = 1 AND NOT EXISTS (
                    SELECT 1 FROM dbo.OperadoresPermisos p
                    WHERE p.OperadorId = o.Id AND p.Permiso = N'panel_facturar_compras');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM dbo.OperadoresPermisos WHERE Permiso = N'panel_facturar_compras';");
            migrationBuilder.DropTable(
                name: "FacturasDeCompras");
        }
    }
}
