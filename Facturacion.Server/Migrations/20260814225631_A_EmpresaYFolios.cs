using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_EmpresaYFolios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Calle",
                table: "Empresas",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoPostal",
                table: "Empresas",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Colonia",
                table: "Empresas",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorreoContacto",
                table: "Empresas",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Empresas",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Localidad",
                table: "Empresas",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoNombreOriginal",
                table: "Empresas",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoRuta",
                table: "Empresas",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoTipoMime",
                table: "Empresas",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Municipio",
                table: "Empresas",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroExterior",
                table: "Empresas",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroInterior",
                table: "Empresas",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pais",
                table: "Empresas",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Referencia",
                table: "Empresas",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Empresas",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CertificadosCsd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroSerie = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VigenciaDesdeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VigenciaHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RutaCer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RutaKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContrasenaCifrada = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCargaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CargadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificadosCsd", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificadosCsd_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesEmpresa",
                columns: table => new
                {
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TasaIvaPorDefecto = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TasaRetencionIvaPorDefecto = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TasaRetencionIsrPorDefecto = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    DiasAvisoCaducidadCertificado = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesEmpresa", x => x.EmpresaId);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesEmpresa_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prefijo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    FolioInicial = table.Column<int>(type: "int", nullable: false),
                    FolioActual = table.Column<int>(type: "int", nullable: false),
                    TipoComprobante = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    FechaAltaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Series_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservasFolio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerieId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Folio = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MomentoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MomentoResolucionUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasFolio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasFolio_Series_SerieId",
                        column: x => x.SerieId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificadosCsd_UnoActivoPorEmpresa",
                table: "CertificadosCsd",
                column: "EmpresaId",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasFolio_EmpresaId_Estado",
                table: "ReservasFolio",
                columns: new[] { "EmpresaId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasFolio_SinFolioRepetido",
                table: "ReservasFolio",
                columns: new[] { "SerieId", "Folio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Series_EmpresaId_Prefijo",
                table: "Series",
                columns: new[] { "EmpresaId", "Prefijo" },
                unique: true);

            // ── Reserva de folios ────────────────────────────────────────────────────────
            //
            // ARQUITECTURA.md §5: el folio se aparta con UPDATE ... WITH (UPDLOCK) dentro de una
            // transacción corta, nunca con SELECT MAX(Folio)+1. Va como script en la
            // migración y no como código C# que lo cree al arrancar, para que el esquema de
            // la base sea reproducible desde las migraciones y nada más.
            //
            // Por qué el UPDATE lee y escribe a la vez: asignar a una variable dentro del
            // propio UPDATE hace que leer el folio actual y aumentarlo sean una sola
            // operación atómica sobre el renglón. Separarlo en SELECT y UPDATE abriría la
            // ventana en la que dos peticiones leen el mismo número.
            //
            // Por qué recibe @EmpresaId: la empresa viene del claim del token, nunca del
            // cliente (ARQUITECTURA.md §4). El procedimiento la exige en el WHERE, así que una
            // serie de otra empresa sencillamente no coincide y no entrega folio.
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.ReservarFolio
                    @SerieId    uniqueidentifier,
                    @EmpresaId  uniqueidentifier,
                    @ReservaId  uniqueidentifier,
                    @MomentoUtc datetime2(7)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    DECLARE @Folio int, @Prefijo nvarchar(25);

                    BEGIN TRANSACTION;

                    UPDATE s WITH (UPDLOCK, ROWLOCK)
                       SET @Folio   = s.FolioActual + 1,
                           s.FolioActual = s.FolioActual + 1,
                           @Prefijo = s.Prefijo
                      FROM dbo.Series AS s
                     WHERE s.Id = @SerieId
                       AND s.EmpresaId = @EmpresaId
                       AND s.Activa = 1;

                    IF @Folio IS NULL
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50001, 'La serie no existe, no es de esta empresa, o está inactiva.', 1;
                    END

                    INSERT INTO dbo.ReservasFolio (Id, EmpresaId, SerieId, Folio, Estado, MomentoUtc)
                    VALUES (@ReservaId, @EmpresaId, @SerieId, @Folio, 'reservado', @MomentoUtc);

                    COMMIT TRANSACTION;

                    SELECT @ReservaId AS ReservaId,
                           @SerieId   AS SerieId,
                           @Prefijo   AS Serie,
                           @Folio     AS Folio;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.ReservarFolio;");

            migrationBuilder.DropTable(
                name: "CertificadosCsd");

            migrationBuilder.DropTable(
                name: "ConfiguracionesEmpresa");

            migrationBuilder.DropTable(
                name: "ReservasFolio");

            migrationBuilder.DropTable(
                name: "Series");

            migrationBuilder.DropColumn(
                name: "Calle",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CodigoPostal",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Colonia",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "CorreoContacto",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Localidad",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LogoNombreOriginal",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LogoRuta",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "LogoTipoMime",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Municipio",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "NumeroExterior",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "NumeroInterior",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Pais",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Referencia",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Empresas");
        }
    }
}
