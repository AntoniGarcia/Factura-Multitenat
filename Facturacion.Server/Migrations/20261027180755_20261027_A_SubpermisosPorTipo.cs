using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261027_A_SubpermisosPorTipo : Migration
    {
        // El orden importa, igual que en 20261009_A: la llave foránea de UsuariosEmpresasPermisos
        // hacia Permisos impide borrar una clave mientras un usuario la tenga. Primero las claves
        // nuevas, luego la conversión de los renglones y al final se borran las viejas. Las claves
        // van escritas aquí y no tomadas de Shared: la migración tiene que seguir diciendo lo mismo
        // aunque las constantes cambien después.
        private static readonly string[] ClavesViejas =
        [
            "administrar_clientes", "administrar_figuras", "administrar_productos", "administrar_vehiculos",
            "cancelar", "configuracion", "emitir_carta_porte", "emitir_comercio_exterior", "emitir_factura",
            "emitir_notaria", "emitir_obra", "emitir_pago", "enviar_correo", "mi_empresa", "ver_documentos"
        ];

        private static readonly string[] ClavesNuevas =
        [
            "carta_porte_cancelar", "carta_porte_crear", "carta_porte_eliminar", "carta_porte_enviar",
            "carta_porte_modificar", "carta_porte_timbrar", "clientes_crear", "clientes_desactivar",
            "clientes_eliminar", "clientes_modificar", "comercio_exterior_cancelar",
            "comercio_exterior_crear", "comercio_exterior_eliminar", "comercio_exterior_enviar",
            "comercio_exterior_modificar", "comercio_exterior_timbrar", "configuracion_correo",
            "configuracion_general", "configuracion_impuestos", "documentos_descarga", "documentos_eliminar",
            "documentos_listado", "empresa_certificados", "empresa_datos", "factura_cancelar",
            "factura_crear", "factura_eliminar", "factura_enviar", "factura_modificar", "factura_timbrar",
            "figuras_crear", "figuras_desactivar", "figuras_eliminar", "figuras_modificar",
            "notaria_cancelar", "notaria_crear", "notaria_eliminar", "notaria_enviar", "notaria_modificar",
            "notaria_timbrar", "obra_cancelar", "obra_crear", "obra_eliminar", "obra_enviar",
            "obra_modificar", "obra_timbrar", "pago_cancelar", "pago_crear", "pago_eliminar", "pago_enviar",
            "pago_modificar", "pago_timbrar", "productos_crear", "productos_desactivar",
            "productos_eliminar", "productos_modificar", "series_crear", "series_desactivar",
            "series_eliminar", "series_modificar", "vehiculos_crear", "vehiculos_desactivar",
            "vehiculos_eliminar", "vehiculos_modificar"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Permisos",
                keyColumn: "Clave",
                keyValue: "comprar_timbres",
                column: "Descripcion",
                value: "Timbres");

            migrationBuilder.InsertData(
                table: "Permisos",
                columns: new[] { "Clave", "Descripcion" },
                values: new object[,]
                {
                    { "carta_porte_cancelar", "Carta Porte: Cancelar" },
                    { "carta_porte_crear", "Carta Porte: Crear" },
                    { "carta_porte_eliminar", "Carta Porte: Eliminar borrador" },
                    { "carta_porte_enviar", "Carta Porte: Enviar por correo" },
                    { "carta_porte_modificar", "Carta Porte: Modificar" },
                    { "carta_porte_timbrar", "Carta Porte: Timbrar" },
                    { "clientes_crear", "Clientes: Crear" },
                    { "clientes_desactivar", "Clientes: Desactivar y reactivar" },
                    { "clientes_eliminar", "Clientes: Eliminar" },
                    { "clientes_modificar", "Clientes: Modificar" },
                    { "comercio_exterior_cancelar", "Comercio Exterior: Cancelar" },
                    { "comercio_exterior_crear", "Comercio Exterior: Crear" },
                    { "comercio_exterior_eliminar", "Comercio Exterior: Eliminar borrador" },
                    { "comercio_exterior_enviar", "Comercio Exterior: Enviar por correo" },
                    { "comercio_exterior_modificar", "Comercio Exterior: Modificar" },
                    { "comercio_exterior_timbrar", "Comercio Exterior: Timbrar" },
                    { "configuracion_correo", "Configuración: Correo" },
                    { "configuracion_general", "Configuración: General" },
                    { "configuracion_impuestos", "Configuración: Impuestos" },
                    { "documentos_descarga", "Documentos: Descarga" },
                    { "documentos_eliminar", "Documentos: Eliminar borradores" },
                    { "documentos_listado", "Documentos: Listado" },
                    { "empresa_certificados", "Mi empresa: Certificados" },
                    { "empresa_datos", "Mi empresa: Datos de la empresa" },
                    { "factura_cancelar", "Factura básica: Cancelar" },
                    { "factura_crear", "Factura básica: Crear" },
                    { "factura_eliminar", "Factura básica: Eliminar borrador" },
                    { "factura_enviar", "Factura básica: Enviar por correo" },
                    { "factura_modificar", "Factura básica: Modificar" },
                    { "factura_timbrar", "Factura básica: Timbrar" },
                    { "figuras_crear", "Figuras de transporte: Crear" },
                    { "figuras_desactivar", "Figuras de transporte: Desactivar y reactivar" },
                    { "figuras_eliminar", "Figuras de transporte: Eliminar" },
                    { "figuras_modificar", "Figuras de transporte: Modificar" },
                    { "notaria_cancelar", "Factura con Notaría: Cancelar" },
                    { "notaria_crear", "Factura con Notaría: Crear" },
                    { "notaria_eliminar", "Factura con Notaría: Eliminar borrador" },
                    { "notaria_enviar", "Factura con Notaría: Enviar por correo" },
                    { "notaria_modificar", "Factura con Notaría: Modificar" },
                    { "notaria_timbrar", "Factura con Notaría: Timbrar" },
                    { "obra_cancelar", "Factura de obra: Cancelar" },
                    { "obra_crear", "Factura de obra: Crear" },
                    { "obra_eliminar", "Factura de obra: Eliminar borrador" },
                    { "obra_enviar", "Factura de obra: Enviar por correo" },
                    { "obra_modificar", "Factura de obra: Modificar" },
                    { "obra_timbrar", "Factura de obra: Timbrar" },
                    { "pago_cancelar", "Complemento de pago: Cancelar" },
                    { "pago_crear", "Complemento de pago: Crear" },
                    { "pago_eliminar", "Complemento de pago: Eliminar borrador" },
                    { "pago_enviar", "Complemento de pago: Enviar por correo" },
                    { "pago_modificar", "Complemento de pago: Modificar" },
                    { "pago_timbrar", "Complemento de pago: Timbrar" },
                    { "productos_crear", "Productos: Crear" },
                    { "productos_desactivar", "Productos: Desactivar y reactivar" },
                    { "productos_eliminar", "Productos: Eliminar" },
                    { "productos_modificar", "Productos: Modificar" },
                    { "series_crear", "Series y folios: Crear" },
                    { "series_desactivar", "Series y folios: Desactivar y reactivar" },
                    { "series_eliminar", "Series y folios: Eliminar" },
                    { "series_modificar", "Series y folios: Modificar" },
                    { "vehiculos_crear", "Vehículos: Crear" },
                    { "vehiculos_desactivar", "Vehículos: Desactivar y reactivar" },
                    { "vehiculos_eliminar", "Vehículos: Eliminar" },
                    { "vehiculos_modificar", "Vehículos: Modificar" }
                });

            // Conversión de los usuarios internos (AGENTS.md §11, 27 de octubre de 2026). Nadie
            // pierde lo que podía hacer: cada permiso viejo se reparte en las casillas que lo
            // componen. Cancelar y enviar pasan a los tipos que el usuario emitía, o a todos si no
            // emitía ninguno. Eliminar en catálogos y en Documentos es nuevo y no se otorga aquí.
            migrationBuilder.Sql("""
                WITH Simples (Vieja, Nueva) AS (
                    SELECT * FROM (VALUES
                        ('ver_documentos', 'documentos_listado'), ('ver_documentos', 'documentos_descarga'),
                        ('emitir_factura', 'factura_crear'), ('emitir_factura', 'factura_modificar'),
                        ('emitir_factura', 'factura_timbrar'), ('emitir_factura', 'factura_eliminar'),
                        ('emitir_notaria', 'notaria_crear'), ('emitir_notaria', 'notaria_modificar'),
                        ('emitir_notaria', 'notaria_timbrar'), ('emitir_notaria', 'notaria_eliminar'),
                        ('emitir_carta_porte', 'carta_porte_crear'), ('emitir_carta_porte', 'carta_porte_modificar'),
                        ('emitir_carta_porte', 'carta_porte_timbrar'), ('emitir_carta_porte', 'carta_porte_eliminar'),
                        ('emitir_comercio_exterior', 'comercio_exterior_crear'), ('emitir_comercio_exterior', 'comercio_exterior_modificar'),
                        ('emitir_comercio_exterior', 'comercio_exterior_timbrar'), ('emitir_comercio_exterior', 'comercio_exterior_eliminar'),
                        ('emitir_obra', 'obra_crear'), ('emitir_obra', 'obra_modificar'),
                        ('emitir_obra', 'obra_timbrar'), ('emitir_obra', 'obra_eliminar'),
                        ('emitir_pago', 'pago_crear'), ('emitir_pago', 'pago_modificar'),
                        ('emitir_pago', 'pago_timbrar'), ('emitir_pago', 'pago_eliminar'),
                        ('administrar_clientes', 'clientes_crear'), ('administrar_clientes', 'clientes_modificar'),
                        ('administrar_clientes', 'clientes_desactivar'),
                        ('administrar_productos', 'productos_crear'), ('administrar_productos', 'productos_modificar'),
                        ('administrar_productos', 'productos_desactivar'),
                        ('administrar_vehiculos', 'vehiculos_crear'), ('administrar_vehiculos', 'vehiculos_modificar'),
                        ('administrar_vehiculos', 'vehiculos_desactivar'),
                        ('administrar_figuras', 'figuras_crear'), ('administrar_figuras', 'figuras_modificar'),
                        ('administrar_figuras', 'figuras_desactivar'),
                        ('mi_empresa', 'empresa_datos'), ('mi_empresa', 'empresa_certificados'),
                        ('configuracion', 'configuracion_general'), ('configuracion', 'configuracion_correo'),
                        ('configuracion', 'configuracion_impuestos'),
                        ('configuracion', 'series_crear'), ('configuracion', 'series_modificar'),
                        ('configuracion', 'series_desactivar')) AS v (Vieja, Nueva)),
                Tipos (Tipo) AS (
                    SELECT * FROM (VALUES ('factura'), ('notaria'), ('carta_porte'), ('comercio_exterior'),
                                          ('obra'), ('pago')) AS v (Tipo)),
                Emitidos AS (
                    SELECT UsuarioId, EmpresaId, SUBSTRING(PermisoClave, 8, 32) AS Tipo
                    FROM UsuariosEmpresasPermisos
                    WHERE PermisoClave LIKE 'emitir[_]%'),
                PorTipo AS (
                    SELECT p.UsuarioId, p.EmpresaId,
                           t.Tipo + CASE p.PermisoClave WHEN 'cancelar' THEN '_cancelar' ELSE '_enviar' END AS Nueva,
                           p.OtorgadoUtc, p.OtorgadoPorUsuarioId
                    FROM UsuariosEmpresasPermisos p
                    CROSS JOIN Tipos t
                    WHERE p.PermisoClave IN ('cancelar', 'enviar_correo')
                      AND (EXISTS (SELECT 1 FROM Emitidos e
                                   WHERE e.UsuarioId = p.UsuarioId AND e.EmpresaId = p.EmpresaId AND e.Tipo = t.Tipo)
                           OR NOT EXISTS (SELECT 1 FROM Emitidos e
                                          WHERE e.UsuarioId = p.UsuarioId AND e.EmpresaId = p.EmpresaId))),
                Todos AS (
                    SELECT p.UsuarioId, p.EmpresaId, s.Nueva, p.OtorgadoUtc, p.OtorgadoPorUsuarioId
                    FROM UsuariosEmpresasPermisos p
                    JOIN Simples s ON s.Vieja = p.PermisoClave
                    UNION ALL
                    SELECT UsuarioId, EmpresaId, Nueva, OtorgadoUtc, OtorgadoPorUsuarioId FROM PorTipo),
                Nuevos AS (
                    SELECT UsuarioId, EmpresaId, Nueva, OtorgadoUtc, OtorgadoPorUsuarioId,
                           ROW_NUMBER() OVER (PARTITION BY UsuarioId, EmpresaId, Nueva ORDER BY OtorgadoUtc) AS Orden
                    FROM Todos)
                INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
                SELECT n.UsuarioId, n.EmpresaId, n.Nueva, n.OtorgadoUtc, n.OtorgadoPorUsuarioId
                FROM Nuevos n
                WHERE n.Orden = 1
                  AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                                  WHERE x.UsuarioId = n.UsuarioId AND x.EmpresaId = n.EmpresaId
                                    AND x.PermisoClave = n.Nueva);
                """);

            migrationBuilder.Sql(
                "DELETE FROM UsuariosEmpresasPermisos WHERE PermisoClave IN (" +
                string.Join(", ", ClavesViejas.Select(c => $"'{c}'")) + ");");

            foreach (var clave in ClavesViejas)
                migrationBuilder.DeleteData(table: "Permisos", keyColumn: "Clave", keyValue: clave);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Permisos",
                keyColumn: "Clave",
                keyValue: "comprar_timbres",
                column: "Descripcion",
                value: "Consultar el saldo y comprar paquetes de timbres");

            migrationBuilder.InsertData(
                table: "Permisos",
                columns: new[] { "Clave", "Descripcion" },
                values: new object[,]
                {
                    { "administrar_clientes", "Dar de alta, editar y dar de baja clientes" },
                    { "administrar_figuras", "Dar de alta, editar y dar de baja figuras de transporte" },
                    { "administrar_productos", "Dar de alta, editar y dar de baja productos" },
                    { "administrar_vehiculos", "Dar de alta, editar y dar de baja vehículos" },
                    { "cancelar", "Cancelar comprobantes timbrados" },
                    { "configuracion", "Configurar correo, impuestos, logo, Notaría y series de la empresa" },
                    { "emitir_carta_porte", "Emitir traslados con Carta Porte" },
                    { "emitir_comercio_exterior", "Emitir facturas con Comercio Exterior" },
                    { "emitir_factura", "Emitir facturas básicas" },
                    { "emitir_notaria", "Emitir facturas con complemento de Notarios Públicos" },
                    { "emitir_obra", "Emitir facturas de obra" },
                    { "emitir_pago", "Emitir complementos de pago" },
                    { "enviar_correo", "Enviar comprobantes por correo" },
                    { "mi_empresa", "Editar los datos fiscales y los certificados de la empresa" },
                    { "ver_documentos", "Consultar y descargar comprobantes" }
                });

            // De regreso cada casilla vuelve al permiso que la contenía. Es una aproximación: quien
            // solo tenía una acción de un tipo recupera el permiso completo de ese tipo, y las
            // casillas que no existían (eliminar en catálogos y en Documentos) se pierden.
            migrationBuilder.Sql("""
                WITH Regreso (Nueva, Vieja) AS (
                    SELECT * FROM (VALUES
                        ('documentos_listado', 'ver_documentos'), ('documentos_descarga', 'ver_documentos'),
                        ('factura_crear', 'emitir_factura'), ('factura_modificar', 'emitir_factura'),
                        ('factura_timbrar', 'emitir_factura'), ('factura_eliminar', 'emitir_factura'),
                        ('notaria_crear', 'emitir_notaria'), ('notaria_modificar', 'emitir_notaria'),
                        ('notaria_timbrar', 'emitir_notaria'), ('notaria_eliminar', 'emitir_notaria'),
                        ('carta_porte_crear', 'emitir_carta_porte'), ('carta_porte_modificar', 'emitir_carta_porte'),
                        ('carta_porte_timbrar', 'emitir_carta_porte'), ('carta_porte_eliminar', 'emitir_carta_porte'),
                        ('comercio_exterior_crear', 'emitir_comercio_exterior'), ('comercio_exterior_modificar', 'emitir_comercio_exterior'),
                        ('comercio_exterior_timbrar', 'emitir_comercio_exterior'), ('comercio_exterior_eliminar', 'emitir_comercio_exterior'),
                        ('obra_crear', 'emitir_obra'), ('obra_modificar', 'emitir_obra'),
                        ('obra_timbrar', 'emitir_obra'), ('obra_eliminar', 'emitir_obra'),
                        ('pago_crear', 'emitir_pago'), ('pago_modificar', 'emitir_pago'),
                        ('pago_timbrar', 'emitir_pago'), ('pago_eliminar', 'emitir_pago'),
                        ('factura_cancelar', 'cancelar'), ('notaria_cancelar', 'cancelar'),
                        ('carta_porte_cancelar', 'cancelar'), ('comercio_exterior_cancelar', 'cancelar'),
                        ('obra_cancelar', 'cancelar'), ('pago_cancelar', 'cancelar'),
                        ('factura_enviar', 'enviar_correo'), ('notaria_enviar', 'enviar_correo'),
                        ('carta_porte_enviar', 'enviar_correo'), ('comercio_exterior_enviar', 'enviar_correo'),
                        ('obra_enviar', 'enviar_correo'), ('pago_enviar', 'enviar_correo'),
                        ('clientes_crear', 'administrar_clientes'), ('clientes_modificar', 'administrar_clientes'),
                        ('clientes_desactivar', 'administrar_clientes'),
                        ('productos_crear', 'administrar_productos'), ('productos_modificar', 'administrar_productos'),
                        ('productos_desactivar', 'administrar_productos'),
                        ('vehiculos_crear', 'administrar_vehiculos'), ('vehiculos_modificar', 'administrar_vehiculos'),
                        ('vehiculos_desactivar', 'administrar_vehiculos'),
                        ('figuras_crear', 'administrar_figuras'), ('figuras_modificar', 'administrar_figuras'),
                        ('figuras_desactivar', 'administrar_figuras'),
                        ('empresa_datos', 'mi_empresa'), ('empresa_certificados', 'mi_empresa'),
                        ('configuracion_general', 'configuracion'), ('configuracion_correo', 'configuracion'),
                        ('configuracion_impuestos', 'configuracion'), ('series_crear', 'configuracion'),
                        ('series_modificar', 'configuracion'), ('series_desactivar', 'configuracion')) AS v (Nueva, Vieja))
                INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
                SELECT DISTINCT p.UsuarioId, p.EmpresaId, r.Vieja, SYSUTCDATETIME(), CAST(NULL AS uniqueidentifier)
                FROM UsuariosEmpresasPermisos p
                JOIN Regreso r ON r.Nueva = p.PermisoClave
                WHERE NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                                  WHERE x.UsuarioId = p.UsuarioId AND x.EmpresaId = p.EmpresaId
                                    AND x.PermisoClave = r.Vieja);
                """);

            migrationBuilder.Sql(
                "DELETE FROM UsuariosEmpresasPermisos WHERE PermisoClave IN (" +
                string.Join(", ", ClavesNuevas.Select(c => $"'{c}'")) + ");");

            foreach (var clave in ClavesNuevas)
                migrationBuilder.DeleteData(table: "Permisos", keyColumn: "Clave", keyValue: clave);
        }
    }
}
