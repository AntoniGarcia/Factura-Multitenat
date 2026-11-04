using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261103_A_SmtpSoloDeEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Habilitado",
                table: "CorreosDeEmpresa");

            migrationBuilder.DropColumn(
                name: "NombreRemitenteSistema",
                table: "CorreosDeEmpresa");

            migrationBuilder.DropColumn(
                name: "ResponderA",
                table: "CorreosDeEmpresa");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Habilitado",
                table: "CorreosDeEmpresa",
                type: "bit",
                nullable: false,
                defaultValue: false);

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

            // Al volver atrás, la empresa que ya tenía su SMTP completo lo sigue usando en vez
            // de pasar en silencio al remitente del sistema.
            migrationBuilder.Sql("""
                UPDATE CorreosDeEmpresa SET Habilitado = 1
                WHERE Servidor IS NOT NULL AND Usuario IS NOT NULL
                  AND ContrasenaCifrada IS NOT NULL AND RemitenteCorreo IS NOT NULL;
                """);
        }
    }
}
