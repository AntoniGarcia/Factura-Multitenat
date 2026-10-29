using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261029_B_MonedasDeEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonedasDeEmpresa",
                columns: table => new
                {
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    ModificadaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonedasDeEmpresa", x => new { x.EmpresaId, x.Moneda });
                    table.ForeignKey(
                        name: "FK_MonedasDeEmpresa_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permisos",
                columns: new[] { "Clave", "Descripcion" },
                values: new object[] { "configuracion_monedas", "Configuración: Monedas" });

            // AGENTS.md §11, 29 de octubre de 2026: solo el peso es fijo. Las empresas que ya
            // tienen comprobantes o pagos en otra moneda —borradores incluidos— la conservan,
            // para que esos documentos se puedan seguir editando.
            migrationBuilder.Sql("""
                INSERT INTO MonedasDeEmpresa (EmpresaId, Moneda, Activa, ModificadaUtc)
                SELECT u.EmpresaId, u.Moneda, 1, SYSUTCDATETIME()
                FROM (SELECT EmpresaId, Moneda FROM Comprobantes
                      UNION
                      SELECT EmpresaId, MonedaP FROM Pagos) AS u
                WHERE u.Moneda NOT IN ('MXN', 'XXX');
                """);

            // Quien ya podía cambiar la configuración general recibe la casilla nueva.
            migrationBuilder.Sql("""
                INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
                SELECT p.UsuarioId, p.EmpresaId, 'configuracion_monedas', p.OtorgadoUtc, p.OtorgadoPorUsuarioId
                FROM UsuariosEmpresasPermisos p
                WHERE p.PermisoClave = 'configuracion_general'
                  AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                                  WHERE x.UsuarioId = p.UsuarioId AND x.EmpresaId = p.EmpresaId
                                    AND x.PermisoClave = 'configuracion_monedas');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM UsuariosEmpresasPermisos WHERE PermisoClave = 'configuracion_monedas';");

            migrationBuilder.DropTable(
                name: "MonedasDeEmpresa");

            migrationBuilder.DeleteData(
                table: "Permisos",
                keyColumn: "Clave",
                keyValue: "configuracion_monedas");
        }
    }
}
