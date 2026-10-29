using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261029_A_IdInternoYDetalleDeProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Detalle",
                table: "Productos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdInterno",
                table: "Productos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_CuentaId_IdInterno",
                table: "Productos",
                columns: new[] { "CuentaId", "IdInterno" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Productos_CuentaId_IdInterno",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "Detalle",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "IdInterno",
                table: "Productos");
        }
    }
}
