using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Facturacion.Server.Migrations
{
    /// <inheritdoc />
    public partial class _20261009_A_PermisosPorSeccionYTitular : Migration
    {
        // El orden importa: la llave foránea de UsuariosEmpresasPermisos hacia Permisos impide
        // borrar una clave vieja mientras un usuario la tenga. Primero las claves nuevas, luego
        // la conversión de los renglones y al final se borran las viejas (AGENTS.md §11,
        // 9 de octubre de 2026).
        private static readonly string[] ClavesViejas = ["timbrar", "configurar_empresa", "administrar_usuarios", "ver_reportes"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsTitular",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

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

            // Titular: por cuenta, quien no fue creado por un administrador (se registró o lo
            // sembró el entorno de desarrollo). Si hubiera varios, el más antiguo; si no hubiera
            // ninguno, el usuario más antiguo de la cuenta, para que ninguna quede sin titular.
            migrationBuilder.Sql("""
                WITH Candidatos AS (
                    SELECT Id, ROW_NUMBER() OVER (
                        PARTITION BY CuentaId
                        ORDER BY CreadoPorAdministrador, FechaAltaUtc, Id) AS Orden
                    FROM AspNetUsers)
                UPDATE u SET EsTitular = 1
                FROM AspNetUsers u
                JOIN Candidatos c ON c.Id = u.Id
                WHERE c.Orden = 1;
                """);

            // El titular entra a todas las empresas de su cuenta.
            migrationBuilder.Sql("""
                INSERT INTO UsuariosEmpresas (UsuarioId, EmpresaId, Activo, FechaAltaUtc)
                SELECT u.Id, e.Id, 1, SYSUTCDATETIME()
                FROM AspNetUsers u
                JOIN Empresas e ON e.CuentaId = u.CuentaId
                WHERE u.EsTitular = 1
                  AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresas ue
                                  WHERE ue.UsuarioId = u.Id AND ue.EmpresaId = e.Id);

                UPDATE ue SET Activo = 1
                FROM UsuariosEmpresas ue
                JOIN AspNetUsers u ON u.Id = ue.UsuarioId
                WHERE u.EsTitular = 1 AND ue.Activo = 0;
                """);

            // Conversión de los permisos de los usuarios internos. Nadie pierde acceso salvo
            // administrar_usuarios, que ahora es solo del titular, y ver_reportes, que no abría nada.
            migrationBuilder.Sql("""
                WITH Equivalencias (Vieja, Nueva) AS (
                    SELECT * FROM (VALUES
                        ('timbrar', 'ver_documentos'),
                        ('timbrar', 'emitir_factura'),
                        ('timbrar', 'emitir_notaria'),
                        ('timbrar', 'emitir_carta_porte'),
                        ('timbrar', 'emitir_comercio_exterior'),
                        ('timbrar', 'emitir_obra'),
                        ('timbrar', 'emitir_pago'),
                        ('timbrar', 'enviar_correo'),
                        ('configurar_empresa', 'mi_empresa'),
                        ('configurar_empresa', 'configuracion'),
                        ('configurar_empresa', 'administrar_clientes'),
                        ('configurar_empresa', 'administrar_productos'),
                        ('configurar_empresa', 'administrar_vehiculos'),
                        ('configurar_empresa', 'administrar_figuras')) AS v (Vieja, Nueva)),
                Nuevos AS (
                    SELECT p.UsuarioId, p.EmpresaId, e.Nueva, p.OtorgadoUtc, p.OtorgadoPorUsuarioId,
                           ROW_NUMBER() OVER (PARTITION BY p.UsuarioId, p.EmpresaId, e.Nueva
                                              ORDER BY p.OtorgadoUtc) AS Orden
                    FROM UsuariosEmpresasPermisos p
                    JOIN Equivalencias e ON e.Vieja = p.PermisoClave)
                INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
                SELECT n.UsuarioId, n.EmpresaId, n.Nueva, n.OtorgadoUtc, n.OtorgadoPorUsuarioId
                FROM Nuevos n
                WHERE n.Orden = 1
                  AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                                  WHERE x.UsuarioId = n.UsuarioId AND x.EmpresaId = n.EmpresaId
                                    AND x.PermisoClave = n.Nueva);

                DELETE FROM UsuariosEmpresasPermisos
                WHERE PermisoClave IN ('timbrar', 'configurar_empresa', 'administrar_usuarios', 'ver_reportes');

                -- El titular no guarda permisos: el token le pone todos.
                DELETE p
                FROM UsuariosEmpresasPermisos p
                JOIN AspNetUsers u ON u.Id = p.UsuarioId
                WHERE u.EsTitular = 1;
                """);

            foreach (var clave in ClavesViejas)
                migrationBuilder.DeleteData(table: "Permisos", keyColumn: "Clave", keyValue: clave);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CuentaId_Titular",
                table: "AspNetUsers",
                column: "CuentaId",
                unique: true,
                filter: "[EsTitular] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CuentaId_Titular",
                table: "AspNetUsers");

            migrationBuilder.InsertData(
                table: "Permisos",
                columns: new[] { "Clave", "Descripcion" },
                values: new object[,]
                {
                    { "administrar_usuarios", "Dar de alta usuarios y asignar permisos" },
                    { "configurar_empresa", "Configurar la empresa, sus series y sus certificados" },
                    { "timbrar", "Emitir y timbrar comprobantes" },
                    { "ver_reportes", "Consultar reportes de la empresa" }
                });

            // De regreso: el titular vuelve a tener los seis permisos guardados en cada empresa
            // de su cuenta, y los internos recuperan timbrar o configurar_empresa si tenían
            // cualquiera de los permisos en que se repartieron.
            migrationBuilder.Sql("""
                INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
                SELECT ue.UsuarioId, ue.EmpresaId, v.Clave, SYSUTCDATETIME(), NULL
                FROM UsuariosEmpresas ue
                JOIN AspNetUsers u ON u.Id = ue.UsuarioId
                CROSS JOIN (VALUES ('timbrar'), ('cancelar'), ('administrar_usuarios'), ('comprar_timbres'),
                                   ('ver_reportes'), ('configurar_empresa')) AS v (Clave)
                WHERE u.EsTitular = 1
                  AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                                  WHERE x.UsuarioId = ue.UsuarioId AND x.EmpresaId = ue.EmpresaId
                                    AND x.PermisoClave = v.Clave);

                WITH Regreso (Nueva, Vieja) AS (
                    SELECT * FROM (VALUES
                        ('emitir_factura', 'timbrar'), ('emitir_notaria', 'timbrar'),
                        ('emitir_carta_porte', 'timbrar'), ('emitir_comercio_exterior', 'timbrar'),
                        ('emitir_obra', 'timbrar'), ('emitir_pago', 'timbrar'),
                        ('mi_empresa', 'configurar_empresa'), ('configuracion', 'configurar_empresa'),
                        ('administrar_clientes', 'configurar_empresa'), ('administrar_productos', 'configurar_empresa'),
                        ('administrar_vehiculos', 'configurar_empresa'), ('administrar_figuras', 'configurar_empresa')) AS v (Nueva, Vieja))
                INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
                SELECT DISTINCT p.UsuarioId, p.EmpresaId, r.Vieja, SYSUTCDATETIME(), CAST(NULL AS uniqueidentifier)
                FROM UsuariosEmpresasPermisos p
                JOIN Regreso r ON r.Nueva = p.PermisoClave
                WHERE NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                                  WHERE x.UsuarioId = p.UsuarioId AND x.EmpresaId = p.EmpresaId
                                    AND x.PermisoClave = r.Vieja);

                DELETE FROM UsuariosEmpresasPermisos
                WHERE PermisoClave IN ('ver_documentos', 'emitir_factura', 'emitir_notaria', 'emitir_carta_porte',
                                       'emitir_comercio_exterior', 'emitir_obra', 'emitir_pago', 'enviar_correo',
                                       'mi_empresa', 'configuracion', 'administrar_clientes', 'administrar_productos',
                                       'administrar_vehiculos', 'administrar_figuras');
                """);

            foreach (var clave in new[]
                     {
                         "administrar_clientes", "administrar_figuras", "administrar_productos", "administrar_vehiculos",
                         "configuracion", "emitir_carta_porte", "emitir_comercio_exterior", "emitir_factura",
                         "emitir_notaria", "emitir_obra", "emitir_pago", "enviar_correo", "mi_empresa", "ver_documentos"
                     })
                migrationBuilder.DeleteData(table: "Permisos", keyColumn: "Clave", keyValue: clave);

            migrationBuilder.DropColumn(
                name: "EsTitular",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "Permisos",
                keyColumn: "Clave",
                keyValue: "comprar_timbres",
                column: "Descripcion",
                value: "Comprar paquetes de timbres");
        }
    }
}
