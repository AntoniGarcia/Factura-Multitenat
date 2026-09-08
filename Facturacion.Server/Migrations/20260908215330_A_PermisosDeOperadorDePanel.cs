using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class A_PermisosDeOperadorDePanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El vocabulario de permisos del operador cambia: de las claves de facturación
            // (timbrar, configurar_empresa…) a las trece del panel (panel_ver_paquetes, …).
            //
            // Backfill: antes el menú del panel no filtraba por permisos — todo operador veía
            // todas las secciones—. Para no quitar acceso que ya existía, todo operador que
            // tuviera permisos hoy se queda con el set completo del panel. Si mañana se quiere
            // podar, se hace desde la pantalla de operadores.
            migrationBuilder.Sql("""
                DECLARE @conPermisos TABLE (OperadorId uniqueidentifier PRIMARY KEY);

                INSERT INTO @conPermisos (OperadorId)
                SELECT DISTINCT OperadorId FROM OperadoresPermisos;

                DELETE FROM OperadoresPermisos;

                INSERT INTO OperadoresPermisos (OperadorId, Permiso)
                SELECT o.OperadorId, p.Permiso
                FROM @conPermisos o
                CROSS APPLY (VALUES
                    (N'panel_ver_paquetes'),
                    (N'panel_administrar_paquetes'),
                    (N'panel_ver_compras'),
                    (N'panel_acreditar_compras'),
                    (N'panel_ver_clientes'),
                    (N'panel_administrar_clientes'),
                    (N'panel_asignar_timbres'),
                    (N'panel_ver_usuarios'),
                    (N'panel_administrar_usuarios'),
                    (N'panel_ver_operadores'),
                    (N'panel_administrar_operadores'),
                    (N'panel_ver_configuracion'),
                    (N'panel_administrar_configuracion')) AS p(Permiso);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se reconstruye el set anterior: las claves de facturación no tenían
            // significado en el panel y no se puede saber cuáles tenía cada operador.
            migrationBuilder.Sql("""
                DELETE FROM OperadoresPermisos
                WHERE Permiso LIKE N'panel_%';
                """);
        }
    }
}