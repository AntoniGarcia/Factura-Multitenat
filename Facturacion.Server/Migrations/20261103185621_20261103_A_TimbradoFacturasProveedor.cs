using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_A_TimbradoFacturasProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaTimbradoUtc",
                table: "FacturasDeCompras",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Folio",
                table: "FacturasDeCompras",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Uuid",
                table: "FacturasDeCompras",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntentosFacturasProveedor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CuentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacturaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaveSolicitud = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VersionSolicitada = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: false),
                    Control = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Folio = table.Column<int>(type: "int", nullable: false),
                    FechaEmisionUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    XmlEnviado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CadenaOriginal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sello = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoCertificado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CodigoError = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    XmlTimbrado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Uuid = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaTimbradoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NoCertificadoSat = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SelloSat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CadenaOriginalSat = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosFacturasProveedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntentosFacturasProveedor_CertificadosProveedor_CertificadoId",
                        column: x => x.CertificadoId,
                        principalTable: "CertificadosProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntentosFacturasProveedor_FacturasDeCompras_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "FacturasDeCompras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeriesFacturasProveedor",
                columns: table => new
                {
                    Rfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Serie = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    UltimoFolio = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesFacturasProveedor", x => new { x.Rfc, x.Serie });
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntentosFacturasProveedor_CertificadoId",
                table: "IntentosFacturasProveedor",
                column: "CertificadoId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosFacturasProveedor_Estado_ActualizadaUtc",
                table: "IntentosFacturasProveedor",
                columns: new[] { "Estado", "ActualizadaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentosFacturasProveedor_FacturaId",
                table: "IntentosFacturasProveedor",
                column: "FacturaId",
                unique: true,
                filter: "[Estado] = N'timbrando'");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosFacturasProveedor_OperadorId_ClaveSolicitud",
                table: "IntentosFacturasProveedor",
                columns: new[] { "OperadorId", "ClaveSolicitud" },
                unique: true);
            migrationBuilder.Sql("""
CREATE PROCEDURE dbo.ReservarFolioProveedor
    @Rfc nvarchar(13), @Serie nvarchar(25)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM dbo.SeriesFacturasProveedor WITH (UPDLOCK, HOLDLOCK)
                       WHERE Rfc = @Rfc AND Serie = @Serie)
            INSERT dbo.SeriesFacturasProveedor (Rfc, Serie, UltimoFolio) VALUES (@Rfc, @Serie, 0);
        UPDATE dbo.SeriesFacturasProveedor WITH (UPDLOCK, ROWLOCK)
        SET UltimoFolio = UltimoFolio + 1
        OUTPUT inserted.UltimoFolio AS Value
        WHERE Rfc = @Rfc AND Serie = @Serie AND UltimoFolio < 2147483647;
        IF @@ROWCOUNT <> 1 THROW 51000, N'La serie del proveedor no tiene folios disponibles.', 1;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
""");
            migrationBuilder.Sql("""
INSERT dbo.OperadoresPermisos (OperadorId, Permiso)
SELECT o.Id, N'panel_timbrar_compras' FROM dbo.OperadoresPlataforma o
WHERE o.EsPrincipal = 1 AND NOT EXISTS
    (SELECT 1 FROM dbo.OperadoresPermisos p WHERE p.OperadorId = o.Id AND p.Permiso = N'panel_timbrar_compras');
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE dbo.ReservarFolioProveedor;");
            migrationBuilder.Sql("DELETE dbo.OperadoresPermisos WHERE Permiso = N'panel_timbrar_compras';");
            migrationBuilder.DropTable(
                name: "IntentosFacturasProveedor");

            migrationBuilder.DropTable(
                name: "SeriesFacturasProveedor");

            migrationBuilder.DropColumn(
                name: "FechaTimbradoUtc",
                table: "FacturasDeCompras");

            migrationBuilder.DropColumn(
                name: "Folio",
                table: "FacturasDeCompras");

            migrationBuilder.DropColumn(
                name: "Uuid",
                table: "FacturasDeCompras");
        }
    }
}
