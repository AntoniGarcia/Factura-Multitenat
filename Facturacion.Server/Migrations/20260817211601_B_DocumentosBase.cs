using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class B_DocumentosBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comprobantes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SerieId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Serie = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Folio = table.Column<int>(type: "int", nullable: true),
                    Uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoDeComprobante = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    FechaEmisionUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LugarExpedicion = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Moneda = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TipoCambio = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    FormaPago = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    MetodoPago = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Exportacion = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CondicionesDePago = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EmisorRfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    EmisorNombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    EmisorRegimenFiscal = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceptorRfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    ReceptorNombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    ReceptorRegimenFiscal = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ReceptorDomicilioFiscal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ReceptorUsoCfdi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GlobalPeriodicidad = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    GlobalMeses = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    GlobalAnio = table.Column<int>(type: "int", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalImpuestosTrasladados = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalImpuestosRetenidos = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    FechaTimbradoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NoCertificadoEmisor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NoCertificadoSat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SelloCfd = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SelloSat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CadenaOriginalSat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RutaXml = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comprobantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comprobantes_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComprobantesRelacionados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoRelacion = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UuidRelacionado = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprobantesRelacionados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprobantesRelacionados_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conceptos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaveProdServ = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ClaveUnidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnidadTexto = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NoIdentificacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ObjetoImp = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conceptos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conceptos_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntentosTimbrado",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    IniciadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TerminadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resultado = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ClaveIdempotencia = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CodigoError = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MensajeError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DuracionMs = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosTimbrado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntentosTimbrado_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesCancelacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UuidSustituye = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SolicitadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResueltaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CodigoRespuesta = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MensajeRespuesta = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SolicitadaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesCancelacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesCancelacion_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConceptosImpuestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConceptoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Impuesto = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TipoFactor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TasaOCuota = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Base = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    EsRetencion = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptosImpuestos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConceptosImpuestos_Conceptos_ConceptoId",
                        column: x => x.ConceptoId,
                        principalTable: "Conceptos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_EmpresaId_Estatus_FechaEmisionUtc",
                table: "Comprobantes",
                columns: new[] { "EmpresaId", "Estatus", "FechaEmisionUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_EmpresaId_FechaEmisionUtc",
                table: "Comprobantes",
                columns: new[] { "EmpresaId", "FechaEmisionUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_FolioPorSerie",
                table: "Comprobantes",
                columns: new[] { "EmpresaId", "SerieId", "Folio" },
                unique: true,
                filter: "[Folio] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Comprobantes_Uuid",
                table: "Comprobantes",
                column: "Uuid",
                unique: true,
                filter: "[Uuid] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ComprobantesRelacionados_SinRepetir",
                table: "ComprobantesRelacionados",
                columns: new[] { "ComprobanteId", "UuidRelacionado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conceptos_OrdenPorComprobante",
                table: "Conceptos",
                columns: new[] { "ComprobanteId", "Orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConceptosImpuestos_SinImpuestoRepetido",
                table: "ConceptosImpuestos",
                columns: new[] { "ConceptoId", "Impuesto", "EsRetencion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntentosTimbrado_EnVuelo",
                table: "IntentosTimbrado",
                columns: new[] { "EmpresaId", "IniciadoUtc" },
                filter: "[Resultado] = 'en_vuelo'");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosTimbrado_NumeroPorComprobante",
                table: "IntentosTimbrado",
                columns: new[] { "ComprobanteId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesCancelacion_EmpresaId_Estado",
                table: "SolicitudesCancelacion",
                columns: new[] { "EmpresaId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesCancelacion_PorComprobante",
                table: "SolicitudesCancelacion",
                columns: new[] { "ComprobanteId", "SolicitadaUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComprobantesRelacionados");

            migrationBuilder.DropTable(
                name: "ConceptosImpuestos");

            migrationBuilder.DropTable(
                name: "IntentosTimbrado");

            migrationBuilder.DropTable(
                name: "SolicitudesCancelacion");

            migrationBuilder.DropTable(
                name: "Conceptos");

            migrationBuilder.DropTable(
                name: "Comprobantes");
        }
    }
}
