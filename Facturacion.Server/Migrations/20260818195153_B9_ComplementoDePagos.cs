using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class B9_ComplementoDePagos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pagos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaPagoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FormaDePagoP = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    MonedaP = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TipoCambioP = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Monto = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    NumOperacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RfcEmisorCtaOrd = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    NomBancoOrdExt = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CtaOrdenante = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RfcEmisorCtaBen = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    CtaBeneficiario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagos_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PagosDocumentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PagoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobantePagadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdDocumento = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Serie = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Folio = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MonedaDR = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EquivalenciaDR = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    NumParcialidad = table.Column<int>(type: "int", nullable: false),
                    ImpSaldoAnt = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ImpPagado = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ImpSaldoInsoluto = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ObjetoImpDR = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosDocumentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosDocumentos_Pagos_PagoId",
                        column: x => x.PagoId,
                        principalTable: "Pagos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PagosDocumentosImpuestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoPagadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Impuesto = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TipoFactor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TasaOCuota = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Base = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    EsRetencion = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosDocumentosImpuestos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosDocumentosImpuestos_PagosDocumentos_DocumentoPagadoId",
                        column: x => x.DocumentoPagadoId,
                        principalTable: "PagosDocumentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pagos_UnoPorComprobante",
                table: "Pagos",
                column: "ComprobanteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagosDocumentos_PorDocumento",
                table: "PagosDocumentos",
                column: "IdDocumento");

            migrationBuilder.CreateIndex(
                name: "IX_PagosDocumentos_SinDocumentoRepetido",
                table: "PagosDocumentos",
                columns: new[] { "PagoId", "IdDocumento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagosDocumentosImpuestos_SinImpuestoRepetido",
                table: "PagosDocumentosImpuestos",
                columns: new[] { "DocumentoPagadoId", "Impuesto", "EsRetencion", "TasaOCuota" },
                unique: true,
                filter: "[TasaOCuota] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PagosDocumentosImpuestos");

            migrationBuilder.DropTable(
                name: "PagosDocumentos");

            migrationBuilder.DropTable(
                name: "Pagos");
        }
    }
}
