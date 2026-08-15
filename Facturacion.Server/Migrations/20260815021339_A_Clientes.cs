using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_Clientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaveInterna = table.Column<int>(type: "int", nullable: false),
                    Rfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    RegimenFiscal = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DomicilioFiscalCp = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ResidenciaFiscal = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    NumRegIdTrib = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    UsoCfdiPreferido = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    MetodoPagoPreferido = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    FormaPagoPreferida = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CorreoPrincipal = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Calle = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    NumeroExterior = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NumeroInterior = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Colonia = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Localidad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Municipio = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Pais = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CodigoPostal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAltaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaModificacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clientes_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_ClaveInternaPorEmpresa",
                table: "Clientes",
                columns: new[] { "EmpresaId", "ClaveInterna" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_Activo_Nombre",
                table: "Clientes",
                columns: new[] { "EmpresaId", "Activo", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_RfcPorEmpresa",
                table: "Clientes",
                columns: new[] { "EmpresaId", "Rfc" },
                unique: true,
                filter: "[Rfc] <> 'XAXX010101000' AND [Rfc] <> 'XEXX010101000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
