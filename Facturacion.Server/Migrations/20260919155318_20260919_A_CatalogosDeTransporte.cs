using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20260919_A_CatalogosDeTransporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FigurasTransporte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TipoFigura = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Rfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    NumeroLicencia = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Calle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NumeroExterior = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: false),
                    NumeroInterior = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Municipio = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CodigoPostal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAltaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaModificacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FigurasTransporte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FigurasTransporte_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vehiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ConfiguracionAutotransporte = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Placa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AnioModelo = table.Column<int>(type: "int", nullable: false),
                    Aseguradora = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Poliza = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TipoPermiso = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NumeroPermiso = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PesoBrutoVehicular = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAltaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaModificacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehiculos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FigurasTransporte_EmpresaId_Activo_Nombre",
                table: "FigurasTransporte",
                columns: new[] { "EmpresaId", "Activo", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_FigurasTransporte_EmpresaId_Clave",
                table: "FigurasTransporte",
                columns: new[] { "EmpresaId", "Clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_EmpresaId_Activo_Descripcion",
                table: "Vehiculos",
                columns: new[] { "EmpresaId", "Activo", "Descripcion" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_EmpresaId_Clave",
                table: "Vehiculos",
                columns: new[] { "EmpresaId", "Clave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FigurasTransporte");

            migrationBuilder.DropTable(
                name: "Vehiculos");
        }
    }
}
