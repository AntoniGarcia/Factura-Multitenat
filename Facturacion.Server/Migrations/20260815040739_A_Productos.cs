using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_Productos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Productos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoInterno = table.Column<int>(type: "int", nullable: false),
                    ClaveProdServ = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ClaveUnidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnidadTexto = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PesoKg = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ObjetoImp = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAltaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaModificacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Productos_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductosImpuestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Impuesto = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TipoFactor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TasaOCuota = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    EsRetencion = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosImpuestos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductosImpuestos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoInternoPorEmpresa",
                table: "Productos",
                columns: new[] { "EmpresaId", "CodigoInterno" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_EmpresaId_Activo_Descripcion",
                table: "Productos",
                columns: new[] { "EmpresaId", "Activo", "Descripcion" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductosImpuestos_SinImpuestoRepetido",
                table: "ProductosImpuestos",
                columns: new[] { "ProductoId", "Impuesto", "EsRetencion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductosImpuestos");

            migrationBuilder.DropTable(
                name: "Productos");
        }
    }
}
