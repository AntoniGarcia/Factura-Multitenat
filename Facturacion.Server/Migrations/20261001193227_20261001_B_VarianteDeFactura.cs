using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261001_B_VarianteDeFactura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Variante",
                table: "Comprobantes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Las facturas que ya existen toman la variante de los datos que tienen guardados,
            // que es como el formulario la deducía antes de esta columna. Pagos y traslados
            // quedan en NULL. Va dentro de EXEC porque en un script generado el UPDATE cae en el
            // mismo lote que el ALTER, y SQL Server compila el lote antes de que la columna exista.
            migrationBuilder.Sql("""
                EXEC(N'
                UPDATE c SET Variante = CASE
                    WHEN EXISTS (SELECT 1 FROM DatosObra o WHERE o.ComprobanteId = c.Id) THEN ''obra''
                    WHEN c.Exportacion = ''02''
                      OR EXISTS (SELECT 1 FROM DatosComercioExterior ce WHERE ce.ComprobanteId = c.Id) THEN ''comercio_exterior''
                    WHEN EXISTS (SELECT 1 FROM DatosNotaria n WHERE n.ComprobanteId = c.Id) THEN ''notaria''
                    ELSE ''basica''
                END
                FROM Comprobantes c
                WHERE c.TipoDeComprobante = ''I'';
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Variante",
                table: "Comprobantes");
        }
    }
}
