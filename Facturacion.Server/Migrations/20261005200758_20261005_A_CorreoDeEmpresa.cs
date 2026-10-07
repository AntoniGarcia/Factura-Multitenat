using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261005_A_CorreoDeEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TasaRetencionIepsPorDefecto",
                table: "ConfiguracionesEmpresa",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CorreosDeEmpresa",
                columns: table => new
                {
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Habilitado = table.Column<bool>(type: "bit", nullable: false),
                    Servidor = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: true),
                    Puerto = table.Column<int>(type: "int", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    ContrasenaCifrada = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RemitenteNombre = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RemitenteCorreo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorreosDeEmpresa", x => x.EmpresaId);
                    table.ForeignKey(
                        name: "FK_CorreosDeEmpresa_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorreosDeEmpresa");

            migrationBuilder.DropColumn(
                name: "TasaRetencionIepsPorDefecto",
                table: "ConfiguracionesEmpresa");
        }
    }
}
