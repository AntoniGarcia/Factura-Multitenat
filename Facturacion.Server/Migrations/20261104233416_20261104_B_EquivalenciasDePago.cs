using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261104_B_EquivalenciasDePago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "EquivalenciaDR",
                table: "PagosDocumentos",
                type: "decimal(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AddColumn<int>(
                name: "DecimalesMonedaDR",
                table: "PagosDocumentos",
                type: "int",
                nullable: false,
                defaultValue: 2);
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM PagosDocumentos d LEFT JOIN SatMoneda m ON m.Clave=d.MonedaDR
                    WHERE m.Clave IS NULL OR m.Decimales NOT BETWEEN 0 AND 6)
                    THROW 50000, N'Revisa las monedas de los documentos pagados antes de actualizar.', 1;
                UPDATE d SET DecimalesMonedaDR=m.Decimales
                FROM PagosDocumentos d JOIN SatMoneda m ON m.Clave=d.MonedaDR;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecimalesMonedaDR",
                table: "PagosDocumentos");

            migrationBuilder.AlterColumn<decimal>(
                name: "EquivalenciaDR",
                table: "PagosDocumentos",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(28,10)",
                oldPrecision: 28,
                oldScale: 10);
        }
    }
}
