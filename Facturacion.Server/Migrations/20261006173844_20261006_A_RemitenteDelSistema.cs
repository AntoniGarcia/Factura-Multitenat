using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261006_A_RemitenteDelSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NombreRemitenteSistema",
                table: "CorreosDeEmpresa",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponderA",
                table: "CorreosDeEmpresa",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NombreRemitenteSistema",
                table: "CorreosDeEmpresa");

            migrationBuilder.DropColumn(
                name: "ResponderA",
                table: "CorreosDeEmpresa");
        }
    }
}
