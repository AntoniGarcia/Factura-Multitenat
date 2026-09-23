using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260922_G_DatosOperacionNotaria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatosNotaria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroInstrumentoNotarial = table.Column<int>(type: "int", nullable: false),
                    FechaInstrumentoNotarial = table.Column<DateOnly>(type: "date", nullable: false),
                    MontoOperacion = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    SubtotalOperacion = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    IvaOperacion = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatosNotaria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatosNotaria_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InmueblesNotariales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatosNotariaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    TipoInmueble = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Calle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NumeroExterior = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: true),
                    NumeroInterior = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Colonia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Localidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Municipio = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CodigoPostal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InmueblesNotariales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InmueblesNotariales_DatosNotaria_DatosNotariaId",
                        column: x => x.DatosNotariaId,
                        principalTable: "DatosNotaria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatosNotaria_ComprobanteId",
                table: "DatosNotaria",
                column: "ComprobanteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InmueblesNotariales_DatosNotariaId_Orden",
                table: "InmueblesNotariales",
                columns: new[] { "DatosNotariaId", "Orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InmueblesNotariales");

            migrationBuilder.DropTable(
                name: "DatosNotaria");
        }
    }
}
