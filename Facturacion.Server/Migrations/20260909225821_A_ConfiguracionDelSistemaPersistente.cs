using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_ConfiguracionDelSistemaPersistente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionDelSistema",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    ServidorSmtp = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    PuertoSmtp = table.Column<int>(type: "int", nullable: false),
                    UsuarioSmtp = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    ContrasenaSmtpCifrada = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RemitenteCorreo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    RemitenteNombre = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UsarTls = table.Column<bool>(type: "bit", nullable: false),
                    NombreDelSistema = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AsuntoVerificacion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CuerpoVerificacion = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    AsuntoContrasena = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CuerpoContrasena = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ActualizadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualizadaPorOperadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionDelSistema", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionDelSistema_Unica", "[Id] = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionDelSistema");
        }
    }
}
