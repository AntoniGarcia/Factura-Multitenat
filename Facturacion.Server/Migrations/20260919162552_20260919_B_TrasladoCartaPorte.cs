using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260919_B_TrasladoCartaPorte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrasladosCartaPorte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehiculoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiguraTransporteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaSalidaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaLlegadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DistanciaRecorridaKm = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PesoBrutoTotalKg = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalMercancias = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrasladosCartaPorte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrasladosCartaPorte_Comprobantes_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "Comprobantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MercanciasCartaPorte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrasladoCartaPorteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    ClaveProdServ = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ClaveUnidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PesoEnKg = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MercanciasCartaPorte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MercanciasCartaPorte_TrasladosCartaPorte_TrasladoCartaPorteId",
                        column: x => x.TrasladoCartaPorteId,
                        principalTable: "TrasladosCartaPorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UbicacionesCartaPorte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrasladoCartaPorteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    RfcRemitenteDestinatario = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    Calle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NumeroExterior = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: false),
                    NumeroInterior = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Municipio = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CodigoPostal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UbicacionesCartaPorte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UbicacionesCartaPorte_TrasladosCartaPorte_TrasladoCartaPorteId",
                        column: x => x.TrasladoCartaPorteId,
                        principalTable: "TrasladosCartaPorte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MercanciasCartaPorte_TrasladoCartaPorteId_Orden",
                table: "MercanciasCartaPorte",
                columns: new[] { "TrasladoCartaPorteId", "Orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrasladosCartaPorte_ComprobanteId",
                table: "TrasladosCartaPorte",
                column: "ComprobanteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UbicacionesCartaPorte_TrasladoCartaPorteId_Orden",
                table: "UbicacionesCartaPorte",
                columns: new[] { "TrasladoCartaPorteId", "Orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MercanciasCartaPorte");

            migrationBuilder.DropTable(
                name: "UbicacionesCartaPorte");

            migrationBuilder.DropTable(
                name: "TrasladosCartaPorte");
        }
    }
}
