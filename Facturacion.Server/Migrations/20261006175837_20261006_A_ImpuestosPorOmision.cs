using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261006_A_ImpuestosPorOmision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TasaIepsPorDefecto",
                table: "ConfiguracionesEmpresa",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TasaIshPorDefecto",
                table: "ConfiguracionesEmpresa",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TasaIepsPorDefecto",
                table: "ConfiguracionesEmpresa");

            migrationBuilder.DropColumn(
                name: "TasaIshPorDefecto",
                table: "ConfiguracionesEmpresa");
        }
    }
}
