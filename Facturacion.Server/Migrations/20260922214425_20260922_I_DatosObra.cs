using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260922_I_DatosObra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatosObra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PorcentajeAmortizacion = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    Retenciones = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Devoluciones = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PorcentajeIva = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    NombreDeduccion1 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PorcentajeDeduccion1 = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    NombreDeduccion2 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PorcentajeDeduccion2 = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    NombreDeduccion3 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PorcentajeDeduccion3 = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    NombreDeduccion4 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PorcentajeDeduccion4 = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatosObra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatosObra_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatosObra_ComprobanteId",
                table: "DatosObra",
                column: "ComprobanteId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatosObra");
        }
    }
}
