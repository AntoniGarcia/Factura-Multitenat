using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_OperadorDePlataforma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperadorId",
                table: "Bitacora",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperadoresPlataforma",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    CorreoNormalizado = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    HashContrasena = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAltaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoAccesoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccesosFallidos = table.Column<int>(type: "int", nullable: false),
                    BloqueadoHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BloqueosConsecutivos = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperadoresPlataforma", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokensOperador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HashToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumidoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevocadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRevocacion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReemplazadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpCreacion = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    AgenteUsuario = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokensOperador", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokensOperador_OperadoresPlataforma_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "OperadoresPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_PorOperador",
                table: "Bitacora",
                columns: new[] { "OperadorId", "MomentoUtc" },
                filter: "[OperadorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OperadoresPlataforma_Correo",
                table: "OperadoresPlataforma",
                column: "CorreoNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokensOperador_FamiliaId",
                table: "RefreshTokensOperador",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokensOperador_HashToken",
                table: "RefreshTokensOperador",
                column: "HashToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokensOperador_OperadorId",
                table: "RefreshTokensOperador",
                column: "OperadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokensOperador");

            migrationBuilder.DropTable(
                name: "OperadoresPlataforma");

            migrationBuilder.DropIndex(
                name: "IX_Bitacora_PorOperador",
                table: "Bitacora");

            migrationBuilder.DropColumn(
                name: "OperadorId",
                table: "Bitacora");
        }
    }
}
