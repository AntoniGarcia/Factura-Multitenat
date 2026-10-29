using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261028_C_EmpresaDeAltaEnCatalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaAltaId",
                table: "Productos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaAltaId",
                table: "Clientes",
                type: "uniqueidentifier",
                nullable: true);

            // Los registros que ya existen toman la empresa del alta que quedó en la bitácora
            // (o la de la renumeración que hizo la migración del catálogo compartido, que es la
            // empresa de origen). En una cuenta con una sola empresa no hay duda. Lo que no tenga
            // registro queda en nulo: aparece con «Todas las empresas» y en ningún filtro de una.
            migrationBuilder.Sql("""
                SELECT x.Entidad, x.EntidadId, x.EmpresaId,
                       ROW_NUMBER() OVER (PARTITION BY x.Entidad, x.EntidadId
                                          ORDER BY CASE WHEN x.Accion LIKE '%[_]creado' THEN 0 ELSE 1 END, x.MomentoUtc) AS N
                INTO #Altas
                FROM dbo.Bitacora AS x
                WHERE x.Accion IN ('cliente_creado', 'cliente_renumerado', 'producto_creado', 'producto_renumerado')
                  AND x.EmpresaId IS NOT NULL;

                UPDATE c SET c.EmpresaAltaId = a.EmpresaId
                FROM dbo.Clientes AS c
                JOIN #Altas AS a ON a.Entidad = 'Cliente' AND a.N = 1 AND a.EntidadId = LOWER(CONVERT(nvarchar(36), c.Id))
                JOIN dbo.Empresas AS e ON e.Id = a.EmpresaId AND e.CuentaId = c.CuentaId;

                UPDATE p SET p.EmpresaAltaId = a.EmpresaId
                FROM dbo.Productos AS p
                JOIN #Altas AS a ON a.Entidad = 'Producto' AND a.N = 1 AND a.EntidadId = LOWER(CONVERT(nvarchar(36), p.Id))
                JOIN dbo.Empresas AS e ON e.Id = a.EmpresaId AND e.CuentaId = p.CuentaId;

                UPDATE c SET c.EmpresaAltaId = (SELECT MIN(CONVERT(nvarchar(36), e.Id)) FROM dbo.Empresas AS e WHERE e.CuentaId = c.CuentaId)
                FROM dbo.Clientes AS c
                WHERE c.EmpresaAltaId IS NULL
                  AND (SELECT COUNT(*) FROM dbo.Empresas AS e WHERE e.CuentaId = c.CuentaId) = 1;

                UPDATE p SET p.EmpresaAltaId = (SELECT MIN(CONVERT(nvarchar(36), e.Id)) FROM dbo.Empresas AS e WHERE e.CuentaId = p.CuentaId)
                FROM dbo.Productos AS p
                WHERE p.EmpresaAltaId IS NULL
                  AND (SELECT COUNT(*) FROM dbo.Empresas AS e WHERE e.CuentaId = p.CuentaId) = 1;

                DROP TABLE #Altas;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Productos_EmpresaAltaId",
                table: "Productos",
                column: "EmpresaAltaId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaAltaId",
                table: "Clientes",
                column: "EmpresaAltaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Empresas_EmpresaAltaId",
                table: "Clientes",
                column: "EmpresaAltaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Empresas_EmpresaAltaId",
                table: "Productos",
                column: "EmpresaAltaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Empresas_EmpresaAltaId",
                table: "Clientes");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Empresas_EmpresaAltaId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_EmpresaAltaId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_EmpresaAltaId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "EmpresaAltaId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "EmpresaAltaId",
                table: "Clientes");
        }
    }
}
