using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <summary>
    /// <c>ProductosImpuestos</c> gana <c>EmpresaId</c> para quedar cubierta por el filtro
    /// global de empresa, como toda tabla con datos de empresa (CLAUDE.md §5).
    ///
    /// <para><b>Por qué en tres pasos y no en uno</b></para>
    /// La columna se agrega <b>nullable</b>, se rellena desde el producto padre y solo
    /// entonces se vuelve obligatoria. Si se agregara directamente como <c>NOT NULL</c>,
    /// SQL Server exigiría un valor por omisión y todos los renglones existentes quedarían
    /// con <c>Guid.Empty</c>: el filtro global los escondería, y los impuestos de cada
    /// producto ya capturado desaparecerían de la aplicación sin ningún error.
    /// </summary>
    public partial class A_EmpresaEnProductoImpuesto : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<System.Guid>(
                name: "EmpresaId",
                table: "ProductosImpuestos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE impuesto
                SET impuesto.EmpresaId = producto.EmpresaId
                FROM ProductosImpuestos AS impuesto
                INNER JOIN Productos AS producto ON producto.Id = impuesto.ProductoId;
                """);

            migrationBuilder.AlterColumn<System.Guid>(
                name: "EmpresaId",
                table: "ProductosImpuestos",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(System.Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "ProductosImpuestos");
        }
    }
}
