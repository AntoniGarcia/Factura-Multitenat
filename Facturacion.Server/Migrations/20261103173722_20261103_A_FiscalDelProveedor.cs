using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_A_FiscalDelProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionFiscalProveedor",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Rfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    NombreFiscal = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    RegimenFiscal = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CodigoPostalExpedicion = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ZonaHoraria = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Serie = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    ActualizadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadaPorOperadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionFiscalProveedor", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionFiscalProveedor_Unica", "[Id] = 1");
                });

            migrationBuilder.CreateTable(
                name: "CertificadosProveedor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfiguracionId = table.Column<byte>(type: "tinyint", nullable: false),
                    Rfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    NumeroSerie = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VigenciaDesdeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VigenciaHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CerCifrado = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    KeyCifrada = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContrasenaCifrada = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CargadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CargadoPorOperadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificadosProveedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificadosProveedor_ConfiguracionFiscalProveedor_ConfiguracionId",
                        column: x => x.ConfiguracionId,
                        principalTable: "ConfiguracionFiscalProveedor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificadosProveedor_ConfiguracionId",
                table: "CertificadosProveedor",
                column: "ConfiguracionId",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CertificadosProveedor_ConfiguracionId_NumeroSerie",
                table: "CertificadosProveedor",
                columns: new[] { "ConfiguracionId", "NumeroSerie" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificadosProveedor");

            migrationBuilder.DropTable(
                name: "ConfiguracionFiscalProveedor");
        }
    }
}
