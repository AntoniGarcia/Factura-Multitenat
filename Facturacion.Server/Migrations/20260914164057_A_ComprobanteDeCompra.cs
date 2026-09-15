using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_ComprobanteDeCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmpresaNombreAlComprar",
                table: "ComprasTimbres",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmpresaRfcAlComprar",
                table: "ComprasTimbres",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            // Las compras existentes deben conservar al menos la identidad que tiene hoy su
            // empresa. Las nuevas ya reciben la copia desde el servicio al momento de crear.
            migrationBuilder.Sql(
                """
                UPDATE c
                   SET c.EmpresaNombreAlComprar = e.NombreFiscal,
                       c.EmpresaRfcAlComprar = e.Rfc
                  FROM ComprasTimbres AS c
                  INNER JOIN Empresas AS e ON e.Id = c.EmpresaId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmpresaNombreAlComprar",
                table: "ComprasTimbres");

            migrationBuilder.DropColumn(
                name: "EmpresaRfcAlComprar",
                table: "ComprasTimbres");
        }
    }
}
