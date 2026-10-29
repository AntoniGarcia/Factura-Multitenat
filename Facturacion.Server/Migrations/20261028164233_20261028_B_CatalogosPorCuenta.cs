using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <summary>
    /// Clientes y productos pasan de la empresa a la cuenta (AGENTS.md §11, 28 de octubre de
    /// 2026). Al unir los catálogos de las empresas de una cuenta:
    /// <list type="bullet">
    ///   <item>Los clientes con el mismo RFC no genérico se fusionan en el más antiguo. Los
    ///   comprobantes y traslados que apuntaban a los otros pasan a él; su contenido no cambia,
    ///   porque cada comprobante guarda su propia copia del receptor.</item>
    ///   <item>Las claves de cliente y los códigos de producto de la empresa más antigua se
    ///   conservan; los que chocan en las demás se renumeran después del mayor de la cuenta.</item>
    /// </list>
    /// Cada fusión y cada renumeración queda en la bitácora.
    /// </summary>
    public partial class _20261028_B_CatalogosPorCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Clientes_Empresas_EmpresaId", table: "Clientes");
            migrationBuilder.DropForeignKey(name: "FK_Productos_Empresas_EmpresaId", table: "Productos");

            // Los índices únicos se quitan antes de unir: mientras dura la conversión, una
            // cuenta puede tener dos clientes con el mismo RFC o la misma clave.
            migrationBuilder.DropIndex(name: "IX_Clientes_ClaveInternaPorEmpresa", table: "Clientes");
            migrationBuilder.DropIndex(name: "IX_Clientes_RfcPorEmpresa", table: "Clientes");
            migrationBuilder.DropIndex(name: "IX_Clientes_EmpresaId_Activo_Nombre", table: "Clientes");
            migrationBuilder.DropIndex(name: "IX_Productos_CodigoInternoPorEmpresa", table: "Productos");
            migrationBuilder.DropIndex(name: "IX_Productos_EmpresaId_Activo_Descripcion", table: "Productos");

            migrationBuilder.RenameColumn(name: "EmpresaId", table: "Clientes", newName: "CuentaId");
            migrationBuilder.RenameColumn(name: "EmpresaId", table: "Productos", newName: "CuentaId");
            migrationBuilder.RenameColumn(name: "EmpresaId", table: "ProductosImpuestos", newName: "CuentaId");

            // Las columnas renombradas todavía guardan la empresa. Se aparta antes de traducirla
            // a la cuenta, porque decide qué claves se conservan.
            migrationBuilder.Sql("""
                SELECT c.Id, c.CuentaId AS EmpresaId INTO #ClientesOrigen FROM dbo.Clientes AS c;
                SELECT p.Id, p.CuentaId AS EmpresaId INTO #ProductosOrigen FROM dbo.Productos AS p;

                UPDATE c SET c.CuentaId = e.CuentaId
                FROM dbo.Clientes AS c JOIN dbo.Empresas AS e ON e.Id = c.CuentaId;

                UPDATE p SET p.CuentaId = e.CuentaId
                FROM dbo.Productos AS p JOIN dbo.Empresas AS e ON e.Id = p.CuentaId;

                UPDATE i SET i.CuentaId = p.CuentaId
                FROM dbo.ProductosImpuestos AS i JOIN dbo.Productos AS p ON p.Id = i.ProductoId;

                -- ── Fusión de clientes con el mismo RFC ─────────────────────────────────────
                -- Se queda el más antiguo. Si cualquiera de los fusionados seguía activo, el que
                -- queda también: ninguna empresa pierde de vista a un cliente con el que trabaja.
                SELECT c.Id, c.CuentaId, c.Rfc, c.ClaveInterna, o.EmpresaId,
                       FIRST_VALUE(c.Id) OVER (PARTITION BY c.CuentaId, c.Rfc ORDER BY c.FechaAltaUtc, c.Id) AS Conservar,
                       MAX(CAST(c.Activo AS int)) OVER (PARTITION BY c.CuentaId, c.Rfc) AS AlgunoActivo
                INTO #PorRfc
                FROM dbo.Clientes AS c JOIN #ClientesOrigen AS o ON o.Id = c.Id
                WHERE c.Rfc <> 'XAXX010101000' AND c.Rfc <> 'XEXX010101000';

                UPDATE x SET x.ClienteId = d.Conservar
                FROM dbo.Comprobantes AS x JOIN #PorRfc AS d ON d.Id = x.ClienteId
                WHERE d.Id <> d.Conservar;

                UPDATE t SET t.ClienteDestinoId = d.Conservar
                FROM dbo.TrasladosCartaPorte AS t JOIN #PorRfc AS d ON d.Id = t.ClienteDestinoId
                WHERE d.Id <> d.Conservar;

                UPDATE c SET c.Activo = 1
                FROM dbo.Clientes AS c JOIN #PorRfc AS d ON d.Id = c.Id
                WHERE d.Id = d.Conservar AND d.AlgunoActivo = 1 AND c.Activo = 0;

                INSERT INTO dbo.Bitacora (EmpresaId, CuentaId, Entidad, EntidadId, Accion, ValorAnterior, ValorNuevo, MomentoUtc)
                SELECT d.EmpresaId, d.CuentaId, 'Cliente', LOWER(CONVERT(nvarchar(36), d.Id)), 'cliente_fusionado',
                       N'{"Rfc":"' + d.Rfc + N'","ClaveInterna":' + CAST(d.ClaveInterna AS nvarchar(11)) +
                           N',"EmpresaId":"' + LOWER(CONVERT(nvarchar(36), d.EmpresaId)) + N'"}',
                       N'{"FusionadoEn":"' + LOWER(CONVERT(nvarchar(36), d.Conservar)) + N'"}',
                       SYSUTCDATETIME()
                FROM #PorRfc AS d
                WHERE d.Id <> d.Conservar;

                DELETE c FROM dbo.Clientes AS c JOIN #PorRfc AS d ON d.Id = c.Id
                WHERE d.Id <> d.Conservar;

                -- ── Claves de cliente repetidas en la cuenta ────────────────────────────────
                -- Conserva la suya el cliente de la empresa más antigua; los demás van después
                -- de la clave mayor de la cuenta.
                SELECT c.Id, c.CuentaId, c.ClaveInterna, o.EmpresaId, e.FechaAltaUtc AS EmpresaAlta,
                       ROW_NUMBER() OVER (PARTITION BY c.CuentaId, c.ClaveInterna ORDER BY e.FechaAltaUtc, e.Id, c.Id) AS N
                INTO #ClavesDeClientes
                FROM dbo.Clientes AS c
                JOIN #ClientesOrigen AS o ON o.Id = c.Id
                JOIN dbo.Empresas AS e ON e.Id = o.EmpresaId;

                SELECT k.Id, k.CuentaId, k.EmpresaId, k.ClaveInterna AS Anterior,
                       m.Mayor + ROW_NUMBER() OVER (PARTITION BY k.CuentaId ORDER BY k.EmpresaAlta, k.EmpresaId, k.ClaveInterna, k.Id) AS Nueva
                INTO #ClientesRenumerados
                FROM #ClavesDeClientes AS k
                JOIN (SELECT CuentaId, MAX(ClaveInterna) AS Mayor FROM dbo.Clientes GROUP BY CuentaId) AS m ON m.CuentaId = k.CuentaId
                WHERE k.N > 1;

                UPDATE c SET c.ClaveInterna = r.Nueva
                FROM dbo.Clientes AS c JOIN #ClientesRenumerados AS r ON r.Id = c.Id;

                INSERT INTO dbo.Bitacora (EmpresaId, CuentaId, Entidad, EntidadId, Accion, ValorAnterior, ValorNuevo, MomentoUtc)
                SELECT r.EmpresaId, r.CuentaId, 'Cliente', LOWER(CONVERT(nvarchar(36), r.Id)), 'cliente_renumerado',
                       N'{"ClaveInterna":' + CAST(r.Anterior AS nvarchar(11)) + N'}',
                       N'{"ClaveInterna":' + CAST(r.Nueva AS nvarchar(11)) + N'}',
                       SYSUTCDATETIME()
                FROM #ClientesRenumerados AS r;

                -- ── Códigos de producto repetidos en la cuenta ──────────────────────────────
                SELECT p.Id, p.CuentaId, p.CodigoInterno, o.EmpresaId, e.FechaAltaUtc AS EmpresaAlta,
                       ROW_NUMBER() OVER (PARTITION BY p.CuentaId, p.CodigoInterno ORDER BY e.FechaAltaUtc, e.Id, p.Id) AS N
                INTO #CodigosDeProductos
                FROM dbo.Productos AS p
                JOIN #ProductosOrigen AS o ON o.Id = p.Id
                JOIN dbo.Empresas AS e ON e.Id = o.EmpresaId;

                SELECT k.Id, k.CuentaId, k.EmpresaId, k.CodigoInterno AS Anterior,
                       m.Mayor + ROW_NUMBER() OVER (PARTITION BY k.CuentaId ORDER BY k.EmpresaAlta, k.EmpresaId, k.CodigoInterno, k.Id) AS Nueva
                INTO #ProductosRenumerados
                FROM #CodigosDeProductos AS k
                JOIN (SELECT CuentaId, MAX(CodigoInterno) AS Mayor FROM dbo.Productos GROUP BY CuentaId) AS m ON m.CuentaId = k.CuentaId
                WHERE k.N > 1;

                UPDATE p SET p.CodigoInterno = r.Nueva
                FROM dbo.Productos AS p JOIN #ProductosRenumerados AS r ON r.Id = p.Id;

                INSERT INTO dbo.Bitacora (EmpresaId, CuentaId, Entidad, EntidadId, Accion, ValorAnterior, ValorNuevo, MomentoUtc)
                SELECT r.EmpresaId, r.CuentaId, 'Producto', LOWER(CONVERT(nvarchar(36), r.Id)), 'producto_renumerado',
                       N'{"CodigoInterno":' + CAST(r.Anterior AS nvarchar(11)) + N'}',
                       N'{"CodigoInterno":' + CAST(r.Nueva AS nvarchar(11)) + N'}',
                       SYSUTCDATETIME()
                FROM #ProductosRenumerados AS r;

                DROP TABLE #ClientesOrigen, #ProductosOrigen, #PorRfc, #ClavesDeClientes,
                           #ClientesRenumerados, #CodigosDeProductos, #ProductosRenumerados;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_ClaveInternaPorCuenta", table: "Clientes",
                columns: new[] { "CuentaId", "ClaveInterna" }, unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Clientes_RfcPorCuenta", table: "Clientes",
                columns: new[] { "CuentaId", "Rfc" }, unique: true,
                filter: "[Rfc] <> 'XAXX010101000' AND [Rfc] <> 'XEXX010101000'");
            migrationBuilder.CreateIndex(
                name: "IX_Clientes_CuentaId_Activo_Nombre", table: "Clientes",
                columns: new[] { "CuentaId", "Activo", "Nombre" });
            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoInternoPorCuenta", table: "Productos",
                columns: new[] { "CuentaId", "CodigoInterno" }, unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Productos_CuentaId_Activo_Descripcion", table: "Productos",
                columns: new[] { "CuentaId", "Activo", "Descripcion" });

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Cuentas_CuentaId", table: "Clientes", column: "CuentaId",
                principalTable: "Cuentas", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Cuentas_CuentaId", table: "Productos", column: "CuentaId",
                principalTable: "Cuentas", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Aproximado: cada cliente y producto vuelve a la empresa más antigua de su cuenta. Los
        /// clientes fusionados no se separan y las claves renumeradas no recuperan la anterior;
        /// las dos cosas quedan en la bitácora.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Clientes_Cuentas_CuentaId", table: "Clientes");
            migrationBuilder.DropForeignKey(name: "FK_Productos_Cuentas_CuentaId", table: "Productos");

            migrationBuilder.DropIndex(name: "IX_Clientes_ClaveInternaPorCuenta", table: "Clientes");
            migrationBuilder.DropIndex(name: "IX_Clientes_RfcPorCuenta", table: "Clientes");
            migrationBuilder.DropIndex(name: "IX_Clientes_CuentaId_Activo_Nombre", table: "Clientes");
            migrationBuilder.DropIndex(name: "IX_Productos_CodigoInternoPorCuenta", table: "Productos");
            migrationBuilder.DropIndex(name: "IX_Productos_CuentaId_Activo_Descripcion", table: "Productos");

            migrationBuilder.RenameColumn(name: "CuentaId", table: "Clientes", newName: "EmpresaId");
            migrationBuilder.RenameColumn(name: "CuentaId", table: "Productos", newName: "EmpresaId");
            migrationBuilder.RenameColumn(name: "CuentaId", table: "ProductosImpuestos", newName: "EmpresaId");

            migrationBuilder.Sql("""
                SELECT c.Id AS CuentaId,
                       (SELECT TOP 1 e.Id FROM dbo.Empresas AS e WHERE e.CuentaId = c.Id ORDER BY e.FechaAltaUtc, e.Id) AS EmpresaId
                INTO #EmpresaMasAntigua
                FROM dbo.Cuentas AS c;

                UPDATE x SET x.EmpresaId = m.EmpresaId
                FROM dbo.Clientes AS x JOIN #EmpresaMasAntigua AS m ON m.CuentaId = x.EmpresaId;

                UPDATE x SET x.EmpresaId = m.EmpresaId
                FROM dbo.Productos AS x JOIN #EmpresaMasAntigua AS m ON m.CuentaId = x.EmpresaId;

                UPDATE i SET i.EmpresaId = p.EmpresaId
                FROM dbo.ProductosImpuestos AS i JOIN dbo.Productos AS p ON p.Id = i.ProductoId;

                DROP TABLE #EmpresaMasAntigua;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_ClaveInternaPorEmpresa", table: "Clientes",
                columns: new[] { "EmpresaId", "ClaveInterna" }, unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Clientes_RfcPorEmpresa", table: "Clientes",
                columns: new[] { "EmpresaId", "Rfc" }, unique: true,
                filter: "[Rfc] <> 'XAXX010101000' AND [Rfc] <> 'XEXX010101000'");
            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_Activo_Nombre", table: "Clientes",
                columns: new[] { "EmpresaId", "Activo", "Nombre" });
            migrationBuilder.CreateIndex(
                name: "IX_Productos_CodigoInternoPorEmpresa", table: "Productos",
                columns: new[] { "EmpresaId", "CodigoInterno" }, unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_Productos_EmpresaId_Activo_Descripcion", table: "Productos",
                columns: new[] { "EmpresaId", "Activo", "Descripcion" });

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Empresas_EmpresaId", table: "Clientes", column: "EmpresaId",
                principalTable: "Empresas", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Empresas_EmpresaId", table: "Productos", column: "EmpresaId",
                principalTable: "Empresas", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }
    }
}
