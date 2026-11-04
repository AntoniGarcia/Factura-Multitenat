-- Actualización de la base del ZIP Facturacion1024.2.zip / Facturacion-IIS-Cnf-20261024-v2.zip.
-- Base: 20261009161413_20261009_A_PermisosPorSeccionYTitular (59 migraciones).
-- Destino: 20261029220708_20261029_B_MonedasDeEmpresa (67 migraciones).
-- Preparado para SQL Server 2014. Ejecutar el archivo completo en SSMS.
-- Seleccionar la base del sistema en SSMS. No cambia su nombre ni crea otra base.
-- Respaldar la base y detener el grupo de aplicaciones de IIS antes de ejecutarlo.
-- Fusiona clientes de una cuenta con RFC repetido, renumera claves en conflicto,
-- suma bolsas de timbres y unifica permisos por usuario. Las fusiones quedan en bitácora.
-- No contiene credenciales, catálogos SAT ni datos de la PC de desarrollo.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF @@TRANCOUNT <> 0
    THROW 51000, N'Ejecuta este archivo en una ventana sin transacciones abiertas.', 1;

BEGIN TRY
    IF DB_ID() <= 4
        THROW 51001, N'Selecciona la base del sistema; no ejecutes en master, model, msdb o tempdb.', 1;
    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51002, N'Esta base no tiene historial de migraciones. No se aplicaron cambios.', 1;
    IF CONVERT(int, SERVERPROPERTY('ProductMajorVersion')) < 12
        THROW 51003, N'Se requiere SQL Server 2014 o posterior.', 1;

    BEGIN TRANSACTION;
    DECLARE @Bloqueo int;
    EXEC @Bloqueo = sys.sp_getapplock
        @Resource = N'Facturacion.ActualizarEsquema.v1',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;
    IF @Bloqueo < 0
        THROW 51004, N'Hay otra actualización de esquema en curso.', 1;

    DECLARE @Esperadas TABLE (Orden int NOT NULL PRIMARY KEY, MigrationId nvarchar(150) NOT NULL);
    INSERT INTO @Esperadas (Orden, MigrationId) VALUES
    (1, N'20260812152209_A_Fundacion'),
    (2, N'20260812192743_A_Autenticacion'),
    (3, N'20260814143157_A_PreferenciaDeTema'),
    (4, N'20260814214305_A_CatalogosSat'),
    (5, N'20260814214724_A_AnchoTasaOCuota'),
    (6, N'20260814225631_A_EmpresaYFolios'),
    (7, N'20260815021339_A_Clientes'),
    (8, N'20260815040739_A_Productos'),
    (9, N'20260815134713_A_Timbres'),
    (10, N'20260815134957_A_PaquetesSembrados'),
    (11, N'20260817145049_A_Usuarios'),
    (12, N'20260817182648_A_EmpresaEnProductoImpuesto'),
    (13, N'20260817211601_B_DocumentosBase'),
    (14, N'20260818160413_B_XmlEnviadoEnIntento'),
    (15, N'20260818170939_B2_ObservacionesDeComprobante'),
    (16, N'20260818195153_B9_ComplementoDePagos'),
    (17, N'20260818211507_B9_UsoCfdiDeCuatroCaracteres'),
    (18, N'20260822173916_A_UsuarioCreadoPorAdministrador'),
    (19, N'20260824153220_20260824_B0_DoblesDePrueba'),
    (20, N'20260826194351_A_PaquetesCorregidos'),
    (21, N'20260826194853_A_OperadorDePlataforma'),
    (22, N'20260826205845_A_RechazoDeCompra'),
    (23, N'20260828174950_A_AltaConVerificacion'),
    (24, N'20260902152616_A_OperadoresPermisos'),
    (25, N'20260908202757_A_OperadorEsPrincipal'),
    (26, N'20260908215330_A_PermisosDeOperadorDePanel'),
    (27, N'20260909225821_A_ConfiguracionDelSistemaPersistente'),
    (28, N'20260914164057_A_ComprobanteDeCompra'),
    (29, N'20260914200621_A_PaquetesPersonalizadosEIva'),
    (30, N'20260918221238_20260918_A_CatalogosCartaPorte'),
    (31, N'20260919155318_20260919_A_CatalogosDeTransporte'),
    (32, N'20260919162552_20260919_B_TrasladoCartaPorte'),
    (33, N'20260919183151_20260919_B2_IntegridadTrasladoCartaPorte'),
    (34, N'20260922150726__20260922_C_DatosInmutablesCartaPorte'),
    (35, N'20260922151905__20260922_D_PesoVehiculoCartaPorte'),
    (36, N'20260922152717__20260922_E_ClaveProdServCartaPorte'),
    (37, N'20260922162355_20260922_F_ConfiguracionNotaria'),
    (38, N'20260922163942__20260922_G_DatosOperacionNotaria'),
    (39, N'20260922193744__20260922_H_PartesNotariales'),
    (40, N'20260922214425_20260922_I_DatosObra'),
    (41, N'20260922222934_20260922_J_PorcentajesRetencionesObra'),
    (42, N'20260922231307_20260922_K_DatosComercioExterior'),
    (43, N'20260923152505_20260923_L_CatalogosComercioExterior'),
    (44, N'20260923163809_20260923_M_LongitudFraccionArancelaria'),
    (45, N'20260924150447_20260924_N_TipoObra'),
    (46, N'20260924190228_20260924_O_UnidadDimensionesCartaPorte'),
    (47, N'20260924191512_20260924_P_PesoUnitarioCartaPorte'),
    (48, N'20260924194704_20260924_Q_DestinatarioCartaPorte'),
    (49, N'20260924223133_20260924_R_OrigenCartaPorteEmpresa'),
    (50, N'20260926152430_20260926_B_EnviosDeCorreo'),
    (51, N'20260926172053_20260926_B_ConsultaSatEnSolicitud'),
    (52, N'20261001193227_20261001_B_VarianteDeFactura'),
    (53, N'20261003165439_20261003_A_ClavesAsignadasPorElCodigo'),
    (54, N'20261005200758_20261005_A_CorreoDeEmpresa'),
    (55, N'20261006173844_20261006_A_RemitenteDelSistema'),
    (56, N'20261006175837_20261006_A_ImpuestosPorOmision'),
    (57, N'20261007212052_20261007_B_EstadoInmuebleNotarial'),
    (58, N'20261007215004_20261007_B_ReceptorExtranjero'),
    (59, N'20261009161413_20261009_A_PermisosPorSeccionYTitular'),
    (60, N'20261027180755_20261027_A_SubpermisosPorTipo'),
    (61, N'20261027215128_20261027_B_LimitesPorCuenta'),
    (62, N'20261027222851_20261027_C_PermisosIgualesPorEmpresa'),
    (63, N'20261028162231_20261028_A_TimbresPorCuenta'),
    (64, N'20261028164233_20261028_B_CatalogosPorCuenta'),
    (65, N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'),
    (66, N'20261028213928_20261029_A_IdInternoYDetalleDeProducto'),
    (67, N'20261029220708_20261029_B_MonedasDeEmpresa');

    IF EXISTS (SELECT 1 FROM @Esperadas e WHERE e.Orden <= 59
               AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory h WHERE h.MigrationId = e.MigrationId))
        THROW 51005, N'La base es anterior o distinta de la del ZIP de referencia. Revisa su historial; no se aplicaron cambios.', 1;
    -- Dos registros de una versión histórica local no alteran estas ocho migraciones.
    IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory h
               WHERE NOT EXISTS (SELECT 1 FROM @Esperadas e WHERE e.MigrationId = h.MigrationId)
                 AND h.MigrationId NOT IN (N'20261001221744_A_CorreoRespuestaVerificado', N'20261001222119_A_ConcurrenciaCorreoRespuesta'))
        THROW 51006, N'Hay migraciones distintas de las previstas. Revisa la versión antes de continuar.', 1;
    IF EXISTS (SELECT 1 FROM @Esperadas a JOIN dbo.__EFMigrationsHistory h ON h.MigrationId = a.MigrationId
               WHERE EXISTS (SELECT 1 FROM @Esperadas b WHERE b.Orden < a.Orden
                             AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory p WHERE p.MigrationId = b.MigrationId)))
        THROW 51007, N'El historial tiene migraciones fuera de orden. Revisa la base antes de actualizar.', 1;

    -- La migración de timbres recrea cuatro procedimientos: conservar sus permisos explícitos.
    DECLARE @Permisos TABLE (Objeto sysname, Principal sysname, Permiso nvarchar(128), Estado char(1));
    INSERT INTO @Permisos (Objeto, Principal, Permiso, Estado)
    SELECT o.name, u.name, p.permission_name, p.state
    FROM sys.database_permissions p
    JOIN sys.objects o ON o.object_id = p.major_id
    JOIN sys.database_principals u ON u.principal_id = p.grantee_principal_id
    WHERE p.class = 1 AND p.minor_id = 0 AND o.schema_id = SCHEMA_ID(N'dbo')
      AND o.name IN (N'ReservarTimbre', N'ConfirmarTimbre', N'DevolverTimbre', N'AcreditarCompra')
      AND p.state IN ('G', 'W', 'D');

    PRINT N'Base seleccionada: ' + QUOTENAME(DB_NAME());
    PRINT N'Aplicando únicamente las migraciones pendientes...';

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''UPDATE [Permisos] SET [Descripcion] = N''''Timbres''''
    WHERE [Clave] = N''''comprar_timbres'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] ON;
    EXEC(N''INSERT INTO [Permisos] ([Clave], [Descripcion])
    VALUES (N''''carta_porte_cancelar'''', N''''Carta Porte: Cancelar''''),
    (N''''carta_porte_crear'''', N''''Carta Porte: Crear''''),
    (N''''carta_porte_eliminar'''', N''''Carta Porte: Eliminar borrador''''),
    (N''''carta_porte_enviar'''', N''''Carta Porte: Enviar por correo''''),
    (N''''carta_porte_modificar'''', N''''Carta Porte: Modificar''''),
    (N''''carta_porte_timbrar'''', N''''Carta Porte: Timbrar''''),
    (N''''clientes_crear'''', N''''Clientes: Crear''''),
    (N''''clientes_desactivar'''', N''''Clientes: Desactivar y reactivar''''),
    (N''''clientes_eliminar'''', N''''Clientes: Eliminar''''),
    (N''''clientes_modificar'''', N''''Clientes: Modificar''''),
    (N''''comercio_exterior_cancelar'''', N''''Comercio Exterior: Cancelar''''),
    (N''''comercio_exterior_crear'''', N''''Comercio Exterior: Crear''''),
    (N''''comercio_exterior_eliminar'''', N''''Comercio Exterior: Eliminar borrador''''),
    (N''''comercio_exterior_enviar'''', N''''Comercio Exterior: Enviar por correo''''),
    (N''''comercio_exterior_modificar'''', N''''Comercio Exterior: Modificar''''),
    (N''''comercio_exterior_timbrar'''', N''''Comercio Exterior: Timbrar''''),
    (N''''configuracion_correo'''', N''''Configuración: Correo''''),
    (N''''configuracion_general'''', N''''Configuración: General''''),
    (N''''configuracion_impuestos'''', N''''Configuración: Impuestos''''),
    (N''''documentos_descarga'''', N''''Documentos: Descarga''''),
    (N''''documentos_eliminar'''', N''''Documentos: Eliminar borradores''''),
    (N''''documentos_listado'''', N''''Documentos: Listado''''),
    (N''''empresa_certificados'''', N''''Mi empresa: Certificados''''),
    (N''''empresa_datos'''', N''''Mi empresa: Datos de la empresa''''),
    (N''''factura_cancelar'''', N''''Factura básica: Cancelar''''),
    (N''''factura_crear'''', N''''Factura básica: Crear''''),
    (N''''factura_eliminar'''', N''''Factura básica: Eliminar borrador''''),
    (N''''factura_enviar'''', N''''Factura básica: Enviar por correo''''),
    (N''''factura_modificar'''', N''''Factura básica: Modificar''''),
    (N''''factura_timbrar'''', N''''Factura básica: Timbrar''''),
    (N''''figuras_crear'''', N''''Figuras de transporte: Crear''''),
    (N''''figuras_desactivar'''', N''''Figuras de transporte: Desactivar y reactivar''''),
    (N''''figuras_eliminar'''', N''''Figuras de transporte: Eliminar''''),
    (N''''figuras_modificar'''', N''''Figuras de transporte: Modificar''''),
    (N''''notaria_cancelar'''', N''''Factura con Notaría: Cancelar''''),
    (N''''notaria_crear'''', N''''Factura con Notaría: Crear''''),
    (N''''notaria_eliminar'''', N''''Factura con Notaría: Eliminar borrador''''),
    (N''''notaria_enviar'''', N''''Factura con Notaría: Enviar por correo''''),
    (N''''notaria_modificar'''', N''''Factura con Notaría: Modificar''''),
    (N''''notaria_timbrar'''', N''''Factura con Notaría: Timbrar''''),
    (N''''obra_cancelar'''', N''''Factura de obra: Cancelar''''),
    (N''''obra_crear'''', N''''Factura de obra: Crear'''');
    INSERT INTO [Permisos] ([Clave], [Descripcion])
    VALUES (N''''obra_eliminar'''', N''''Factura de obra: Eliminar borrador''''),
    (N''''obra_enviar'''', N''''Factura de obra: Enviar por correo''''),
    (N''''obra_modificar'''', N''''Factura de obra: Modificar''''),
    (N''''obra_timbrar'''', N''''Factura de obra: Timbrar''''),
    (N''''pago_cancelar'''', N''''Complemento de pago: Cancelar''''),
    (N''''pago_crear'''', N''''Complemento de pago: Crear''''),
    (N''''pago_eliminar'''', N''''Complemento de pago: Eliminar borrador''''),
    (N''''pago_enviar'''', N''''Complemento de pago: Enviar por correo''''),
    (N''''pago_modificar'''', N''''Complemento de pago: Modificar''''),
    (N''''pago_timbrar'''', N''''Complemento de pago: Timbrar''''),
    (N''''productos_crear'''', N''''Productos: Crear''''),
    (N''''productos_desactivar'''', N''''Productos: Desactivar y reactivar''''),
    (N''''productos_eliminar'''', N''''Productos: Eliminar''''),
    (N''''productos_modificar'''', N''''Productos: Modificar''''),
    (N''''series_crear'''', N''''Series y folios: Crear''''),
    (N''''series_desactivar'''', N''''Series y folios: Desactivar y reactivar''''),
    (N''''series_eliminar'''', N''''Series y folios: Eliminar''''),
    (N''''series_modificar'''', N''''Series y folios: Modificar''''),
    (N''''vehiculos_crear'''', N''''Vehículos: Crear''''),
    (N''''vehiculos_desactivar'''', N''''Vehículos: Desactivar y reactivar''''),
    (N''''vehiculos_eliminar'''', N''''Vehículos: Eliminar''''),
    (N''''vehiculos_modificar'''', N''''Vehículos: Modificar'''')'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] OFF;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'WITH Simples (Vieja, Nueva) AS (
        SELECT * FROM (VALUES
            (''ver_documentos'', ''documentos_listado''), (''ver_documentos'', ''documentos_descarga''),
            (''emitir_factura'', ''factura_crear''), (''emitir_factura'', ''factura_modificar''),
            (''emitir_factura'', ''factura_timbrar''), (''emitir_factura'', ''factura_eliminar''),
            (''emitir_notaria'', ''notaria_crear''), (''emitir_notaria'', ''notaria_modificar''),
            (''emitir_notaria'', ''notaria_timbrar''), (''emitir_notaria'', ''notaria_eliminar''),
            (''emitir_carta_porte'', ''carta_porte_crear''), (''emitir_carta_porte'', ''carta_porte_modificar''),
            (''emitir_carta_porte'', ''carta_porte_timbrar''), (''emitir_carta_porte'', ''carta_porte_eliminar''),
            (''emitir_comercio_exterior'', ''comercio_exterior_crear''), (''emitir_comercio_exterior'', ''comercio_exterior_modificar''),
            (''emitir_comercio_exterior'', ''comercio_exterior_timbrar''), (''emitir_comercio_exterior'', ''comercio_exterior_eliminar''),
            (''emitir_obra'', ''obra_crear''), (''emitir_obra'', ''obra_modificar''),
            (''emitir_obra'', ''obra_timbrar''), (''emitir_obra'', ''obra_eliminar''),
            (''emitir_pago'', ''pago_crear''), (''emitir_pago'', ''pago_modificar''),
            (''emitir_pago'', ''pago_timbrar''), (''emitir_pago'', ''pago_eliminar''),
            (''administrar_clientes'', ''clientes_crear''), (''administrar_clientes'', ''clientes_modificar''),
            (''administrar_clientes'', ''clientes_desactivar''),
            (''administrar_productos'', ''productos_crear''), (''administrar_productos'', ''productos_modificar''),
            (''administrar_productos'', ''productos_desactivar''),
            (''administrar_vehiculos'', ''vehiculos_crear''), (''administrar_vehiculos'', ''vehiculos_modificar''),
            (''administrar_vehiculos'', ''vehiculos_desactivar''),
            (''administrar_figuras'', ''figuras_crear''), (''administrar_figuras'', ''figuras_modificar''),
            (''administrar_figuras'', ''figuras_desactivar''),
            (''mi_empresa'', ''empresa_datos''), (''mi_empresa'', ''empresa_certificados''),
            (''configuracion'', ''configuracion_general''), (''configuracion'', ''configuracion_correo''),
            (''configuracion'', ''configuracion_impuestos''),
            (''configuracion'', ''series_crear''), (''configuracion'', ''series_modificar''),
            (''configuracion'', ''series_desactivar'')) AS v (Vieja, Nueva)),
    Tipos (Tipo) AS (
        SELECT * FROM (VALUES (''factura''), (''notaria''), (''carta_porte''), (''comercio_exterior''),
                              (''obra''), (''pago'')) AS v (Tipo)),
    Emitidos AS (
        SELECT UsuarioId, EmpresaId, SUBSTRING(PermisoClave, 8, 32) AS Tipo
        FROM UsuariosEmpresasPermisos
        WHERE PermisoClave LIKE ''emitir[_]%''),
    PorTipo AS (
        SELECT p.UsuarioId, p.EmpresaId,
               t.Tipo + CASE p.PermisoClave WHEN ''cancelar'' THEN ''_cancelar'' ELSE ''_enviar'' END AS Nueva,
               p.OtorgadoUtc, p.OtorgadoPorUsuarioId
        FROM UsuariosEmpresasPermisos p
        CROSS JOIN Tipos t
        WHERE p.PermisoClave IN (''cancelar'', ''enviar_correo'')
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
                        AND x.PermisoClave = n.Nueva);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'DELETE FROM UsuariosEmpresasPermisos WHERE PermisoClave IN (''administrar_clientes'', ''administrar_figuras'', ''administrar_productos'', ''administrar_vehiculos'', ''cancelar'', ''configuracion'', ''emitir_carta_porte'', ''emitir_comercio_exterior'', ''emitir_factura'', ''emitir_notaria'', ''emitir_obra'', ''emitir_pago'', ''enviar_correo'', ''mi_empresa'', ''ver_documentos'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_clientes'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_figuras'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_productos'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_vehiculos'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''cancelar'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''configuracion'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_carta_porte'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_comercio_exterior'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_factura'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_notaria'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_obra'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_pago'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''enviar_correo'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''mi_empresa'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''ver_documentos'''';
    SELECT @@ROWCOUNT'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027180755_20261027_A_SubpermisosPorTipo'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261027180755_20261027_A_SubpermisosPorTipo'', N''10.0.10'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027215128_20261027_B_LimitesPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Cuentas] ADD [LimiteDeEmpresas] int NOT NULL DEFAULT 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027215128_20261027_B_LimitesPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Cuentas] ADD [LimiteDeUsuarios] int NOT NULL DEFAULT 3;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027215128_20261027_B_LimitesPorCuenta'
)
BEGIN
    EXEC(N'UPDATE c SET
        LimiteDeUsuarios = CASE WHEN u.Total > 3 THEN u.Total ELSE 3 END,
        LimiteDeEmpresas = CASE WHEN e.Total > 1 THEN e.Total ELSE 1 END
    FROM Cuentas c
    CROSS APPLY (
        SELECT COUNT(*) AS Total FROM AspNetUsers us
        WHERE us.CuentaId = c.Id AND us.Activo = 1
          AND EXISTS (SELECT 1 FROM UsuariosEmpresas ue WHERE ue.UsuarioId = us.Id AND ue.Activo = 1)
    ) u
    CROSS APPLY (SELECT COUNT(*) AS Total FROM Empresas em WHERE em.CuentaId = c.Id) e;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027215128_20261027_B_LimitesPorCuenta'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261027215128_20261027_B_LimitesPorCuenta'', N''10.0.10'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027222851_20261027_C_PermisosIgualesPorEmpresa'
)
BEGIN
    EXEC(N'INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
    SELECT ue.UsuarioId, ue.EmpresaId, c.PermisoClave, SYSUTCDATETIME(), o.OtorgadoPorUsuarioId
    FROM UsuariosEmpresas ue
    JOIN AspNetUsers u ON u.Id = ue.UsuarioId AND u.EsTitular = 0
    CROSS APPLY (
        SELECT DISTINCT p.PermisoClave FROM UsuariosEmpresasPermisos p WHERE p.UsuarioId = ue.UsuarioId
    ) c
    CROSS APPLY (
        SELECT TOP 1 p.OtorgadoPorUsuarioId FROM UsuariosEmpresasPermisos p
        WHERE p.UsuarioId = ue.UsuarioId AND p.PermisoClave = c.PermisoClave
        ORDER BY p.OtorgadoUtc
    ) o
    WHERE NOT EXISTS (
        SELECT 1 FROM UsuariosEmpresasPermisos x
        WHERE x.UsuarioId = ue.UsuarioId AND x.EmpresaId = ue.EmpresaId AND x.PermisoClave = c.PermisoClave);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027222851_20261027_C_PermisosIgualesPorEmpresa'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261027222851_20261027_C_PermisosIgualesPorEmpresa'', N''10.0.10'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [BolsasTimbres] DROP CONSTRAINT [FK_BolsasTimbres_Empresas_EmpresaId];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Paquetes] DROP CONSTRAINT [FK_Paquetes_Empresas_EmpresaId];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_BolsasTimbres_EmpresaId] ON [BolsasTimbres];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Paquetes_EmpresaId_Activo_Orden] ON [Paquetes];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_MovimientosTimbre_EmpresaId_MomentoUtc] ON [MovimientosTimbre];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_ComprasTimbres_EmpresaId_CreadaUtc] ON [ComprasTimbres];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[BolsasTimbres].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[Paquetes].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [MovimientosTimbre] ADD [CuentaId] uniqueidentifier NOT NULL DEFAULT ''00000000-0000-0000-0000-000000000000'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [ComprasTimbres] ADD [CuentaId] uniqueidentifier NOT NULL DEFAULT ''00000000-0000-0000-0000-000000000000'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'UPDATE p SET p.CuentaId = e.CuentaId
    FROM dbo.Paquetes AS p JOIN dbo.Empresas AS e ON e.Id = p.CuentaId;

    UPDATE m SET m.CuentaId = e.CuentaId
    FROM dbo.MovimientosTimbre AS m JOIN dbo.Empresas AS e ON e.Id = m.EmpresaId;

    UPDATE c SET c.CuentaId = e.CuentaId
    FROM dbo.ComprasTimbres AS c JOIN dbo.Empresas AS e ON e.Id = c.EmpresaId;

    UPDATE b SET b.CuentaId = e.CuentaId
    FROM dbo.BolsasTimbres AS b JOIN dbo.Empresas AS e ON e.Id = b.CuentaId;

    -- La bolsa que se conserva se elige antes de tocar nada: si se eligiera después
    -- de actualizar su fecha, podría empatar con otra y borrarse la equivocada.
    SELECT b.CuentaId,
           SUM(b.Disponibles) AS Disponibles,
           SUM(b.Reservados) AS Reservados,
           MAX(b.ActualizadaUtc) AS ActualizadaUtc,
           (SELECT TOP 1 x.Id FROM dbo.BolsasTimbres AS x
             WHERE x.CuentaId = b.CuentaId ORDER BY x.ActualizadaUtc, x.Id) AS Conservar
    INTO #BolsasPorCuenta
    FROM dbo.BolsasTimbres AS b
    GROUP BY b.CuentaId;

    DELETE b FROM dbo.BolsasTimbres AS b
    JOIN #BolsasPorCuenta AS t ON t.CuentaId = b.CuentaId
    WHERE b.Id <> t.Conservar;

    UPDATE b
       SET b.Disponibles    = t.Disponibles,
           b.Reservados     = t.Reservados,
           b.ActualizadaUtc = t.ActualizadaUtc
    FROM dbo.BolsasTimbres AS b
    JOIN #BolsasPorCuenta AS t ON t.Conservar = b.Id;

    DROP TABLE #BolsasPorCuenta;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_BolsasTimbres_CuentaId] ON [BolsasTimbres] ([CuentaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Paquetes_CuentaId_Activo_Orden] ON [Paquetes] ([CuentaId], [Activo], [Orden]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_MovimientosTimbre_CuentaId_MomentoUtc] ON [MovimientosTimbre] ([CuentaId], [MomentoUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ComprasTimbres_CuentaId_CreadaUtc] ON [ComprasTimbres] ([CuentaId], [CreadaUtc]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [BolsasTimbres] ADD CONSTRAINT [FK_BolsasTimbres_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Paquetes] ADD CONSTRAINT [FK_Paquetes_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.ReservarTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.ReservarTimbre;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.ReservarTimbre
        @EmpresaId     uniqueidentifier,
        @ReservaId     uniqueidentifier,
        @ComprobanteId uniqueidentifier,
        @MomentoUtc    datetime2(7)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @Disponibles int, @Reservados int;
        DECLARE @CuentaId uniqueidentifier = (SELECT CuentaId FROM dbo.Empresas WHERE Id = @EmpresaId);

        BEGIN TRANSACTION;

        -- La condición de saldo va en el WHERE: comprobar y descontar es una sola
        -- operación atómica, aunque dos empresas de la cuenta timbren a la vez.
        UPDATE b WITH (UPDLOCK, ROWLOCK)
           SET b.Disponibles    = b.Disponibles - 1,
               b.Reservados     = b.Reservados + 1,
               b.ActualizadaUtc = @MomentoUtc,
               @Disponibles     = b.Disponibles - 1,
               @Reservados      = b.Reservados + 1
          FROM dbo.BolsasTimbres AS b
         WHERE b.CuentaId = @CuentaId
           AND b.Disponibles >= 1;

        IF @Disponibles IS NULL
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT CAST(0 AS bit) AS Reservado, 0 AS DisponiblesDespues;
            RETURN;
        END

        INSERT INTO dbo.ReservasTimbre
            (Id, EmpresaId, ComprobanteId, Estado, CreadaUtc)
        VALUES
            (@ReservaId, @EmpresaId, @ComprobanteId, ''reservado'', @MomentoUtc);

        INSERT INTO dbo.MovimientosTimbre
            (Id, CuentaId, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
             DisponiblesDespues, ReservadosDespues, ReservaId, MomentoUtc)
        VALUES
            (NEWID(), @CuentaId, @EmpresaId, ''reserva'', -1, 1,
             @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Reservado, @Disponibles AS DisponiblesDespues;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.ConfirmarTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.ConfirmarTimbre;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.ConfirmarTimbre
        @EmpresaId  uniqueidentifier,
        @ReservaId  uniqueidentifier,
        @MomentoUtc datetime2(7)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @Disponibles int, @Reservados int;
        DECLARE @CuentaId uniqueidentifier = (SELECT CuentaId FROM dbo.Empresas WHERE Id = @EmpresaId);

        BEGIN TRANSACTION;

        UPDATE r WITH (UPDLOCK, ROWLOCK)
           SET r.Estado      = ''confirmado'',
               r.ResueltaUtc = @MomentoUtc
          FROM dbo.ReservasTimbre AS r
         WHERE r.Id = @ReservaId
           AND r.EmpresaId = @EmpresaId
           AND r.Estado = ''reservado'';

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT CAST(0 AS bit) AS Resuelto;
            RETURN;
        END

        UPDATE b WITH (UPDLOCK, ROWLOCK)
           SET b.Reservados     = b.Reservados - 1,
               b.ActualizadaUtc = @MomentoUtc,
               @Disponibles     = b.Disponibles,
               @Reservados      = b.Reservados - 1
          FROM dbo.BolsasTimbres AS b
         WHERE b.CuentaId = @CuentaId;

        INSERT INTO dbo.MovimientosTimbre
            (Id, CuentaId, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
             DisponiblesDespues, ReservadosDespues, ReservaId, MomentoUtc)
        VALUES
            (NEWID(), @CuentaId, @EmpresaId, ''consumo'', 0, -1,
             @Disponibles, @Reservados, @ReservaId, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Resuelto;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.DevolverTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.DevolverTimbre;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.DevolverTimbre
        @EmpresaId  uniqueidentifier,
        @ReservaId  uniqueidentifier,
        @Motivo     nvarchar(300),
        @MomentoUtc datetime2(7)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @Disponibles int, @Reservados int;
        DECLARE @CuentaId uniqueidentifier = (SELECT CuentaId FROM dbo.Empresas WHERE Id = @EmpresaId);

        BEGIN TRANSACTION;

        UPDATE r WITH (UPDLOCK, ROWLOCK)
           SET r.Estado           = ''devuelto'',
               r.ResueltaUtc      = @MomentoUtc,
               r.MotivoResolucion = @Motivo
          FROM dbo.ReservasTimbre AS r
         WHERE r.Id = @ReservaId
           AND r.EmpresaId = @EmpresaId
           AND r.Estado = ''reservado'';

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT CAST(0 AS bit) AS Resuelto;
            RETURN;
        END

        UPDATE b WITH (UPDLOCK, ROWLOCK)
           SET b.Disponibles    = b.Disponibles + 1,
               b.Reservados     = b.Reservados - 1,
               b.ActualizadaUtc = @MomentoUtc,
               @Disponibles     = b.Disponibles + 1,
               @Reservados      = b.Reservados - 1
          FROM dbo.BolsasTimbres AS b
         WHERE b.CuentaId = @CuentaId;

        INSERT INTO dbo.MovimientosTimbre
            (Id, CuentaId, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
             DisponiblesDespues, ReservadosDespues, ReservaId, Motivo, MomentoUtc)
        VALUES
            (NEWID(), @CuentaId, @EmpresaId, ''devolucion'', 1, -1,
             @Disponibles, @Reservados, @ReservaId, @Motivo, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Resuelto;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'IF OBJECT_ID(N''dbo.AcreditarCompra'', N''P'') IS NOT NULL DROP PROCEDURE dbo.AcreditarCompra;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.AcreditarCompra
        @CompraId   uniqueidentifier,
        @MomentoUtc datetime2(7)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        DECLARE @CuentaId uniqueidentifier, @EmpresaId uniqueidentifier, @Cantidad int,
                @Disponibles int, @Reservados int;

        BEGIN TRANSACTION;

        UPDATE c WITH (UPDLOCK, ROWLOCK)
           SET c.Estado        = ''pagada'',
               c.AcreditadaUtc = @MomentoUtc,
               c.VenceUtc      = DATEADD(month, c.VigenciaMeses, @MomentoUtc),
               @CuentaId       = c.CuentaId,
               @EmpresaId      = c.EmpresaId,
               @Cantidad       = c.CantidadTimbres
          FROM dbo.ComprasTimbres AS c
         WHERE c.Id = @CompraId
           AND c.Estado = ''pendiente_de_pago'';

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT CAST(0 AS bit) AS Acreditada, NULL AS DisponiblesDespues;
            RETURN;
        END

        -- UPDLOCK + HOLDLOCK sobre una llave que todavía no existe toma un candado de
        -- rango: dos acreditaciones simultáneas de la primera compra de una cuenta no
        -- insertan dos bolsas.
        IF NOT EXISTS (SELECT 1 FROM dbo.BolsasTimbres WITH (UPDLOCK, HOLDLOCK)
                        WHERE CuentaId = @CuentaId)
            INSERT INTO dbo.BolsasTimbres
                (Id, CuentaId, Disponibles, Reservados, ActualizadaUtc)
            VALUES
                (NEWID(), @CuentaId, 0, 0, @MomentoUtc);

        UPDATE b WITH (UPDLOCK, ROWLOCK)
           SET b.Disponibles    = b.Disponibles + @Cantidad,
               b.ActualizadaUtc = @MomentoUtc,
               @Disponibles     = b.Disponibles + @Cantidad,
               @Reservados      = b.Reservados
          FROM dbo.BolsasTimbres AS b
         WHERE b.CuentaId = @CuentaId;

        INSERT INTO dbo.MovimientosTimbre
            (Id, CuentaId, EmpresaId, Tipo, DeltaDisponible, DeltaReservado,
             DisponiblesDespues, ReservadosDespues, CompraId, MomentoUtc)
        VALUES
            (NEWID(), @CuentaId, @EmpresaId, ''compra'', @Cantidad, 0,
             @Disponibles, @Reservados, @CompraId, @MomentoUtc);

        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Acreditada, @Disponibles AS DisponiblesDespues;
    END');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028162231_20261028_A_TimbresPorCuenta'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261028162231_20261028_A_TimbresPorCuenta'', N''10.0.10'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Clientes] DROP CONSTRAINT [FK_Clientes_Empresas_EmpresaId];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] DROP CONSTRAINT [FK_Productos_Empresas_EmpresaId];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Clientes_ClaveInternaPorEmpresa] ON [Clientes];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Clientes_RfcPorEmpresa] ON [Clientes];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Clientes_EmpresaId_Activo_Nombre] ON [Clientes];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Productos_CodigoInternoPorEmpresa] ON [Productos];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'DROP INDEX [IX_Productos_EmpresaId_Activo_Descripcion] ON [Productos];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[Clientes].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[Productos].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'EXEC sp_rename N''[ProductosImpuestos].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'SELECT c.Id, c.CuentaId AS EmpresaId INTO #ClientesOrigen FROM dbo.Clientes AS c;
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
    WHERE c.Rfc <> ''XAXX010101000'' AND c.Rfc <> ''XEXX010101000'';

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
    SELECT d.EmpresaId, d.CuentaId, ''Cliente'', LOWER(CONVERT(nvarchar(36), d.Id)), ''cliente_fusionado'',
           N''{"Rfc":"'' + d.Rfc + N''","ClaveInterna":'' + CAST(d.ClaveInterna AS nvarchar(11)) +
               N'',"EmpresaId":"'' + LOWER(CONVERT(nvarchar(36), d.EmpresaId)) + N''"}'',
           N''{"FusionadoEn":"'' + LOWER(CONVERT(nvarchar(36), d.Conservar)) + N''"}'',
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
    SELECT r.EmpresaId, r.CuentaId, ''Cliente'', LOWER(CONVERT(nvarchar(36), r.Id)), ''cliente_renumerado'',
           N''{"ClaveInterna":'' + CAST(r.Anterior AS nvarchar(11)) + N''}'',
           N''{"ClaveInterna":'' + CAST(r.Nueva AS nvarchar(11)) + N''}'',
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
    SELECT r.EmpresaId, r.CuentaId, ''Producto'', LOWER(CONVERT(nvarchar(36), r.Id)), ''producto_renumerado'',
           N''{"CodigoInterno":'' + CAST(r.Anterior AS nvarchar(11)) + N''}'',
           N''{"CodigoInterno":'' + CAST(r.Nueva AS nvarchar(11)) + N''}'',
           SYSUTCDATETIME()
    FROM #ProductosRenumerados AS r;

    DROP TABLE #ClientesOrigen, #ProductosOrigen, #PorRfc, #ClavesDeClientes,
               #ClientesRenumerados, #CodigosDeProductos, #ProductosRenumerados;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Clientes_ClaveInternaPorCuenta] ON [Clientes] ([CuentaId], [ClaveInterna]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_Clientes_RfcPorCuenta] ON [Clientes] ([CuentaId], [Rfc]) WHERE [Rfc] <> ''''XAXX010101000'''' AND [Rfc] <> ''''XEXX010101000'''''');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Clientes_CuentaId_Activo_Nombre] ON [Clientes] ([CuentaId], [Activo], [Nombre]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Productos_CodigoInternoPorCuenta] ON [Productos] ([CuentaId], [CodigoInterno]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Productos_CuentaId_Activo_Descripcion] ON [Productos] ([CuentaId], [Activo], [Descripcion]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Clientes] ADD CONSTRAINT [FK_Clientes_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD CONSTRAINT [FK_Productos_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028164233_20261028_B_CatalogosPorCuenta'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261028164233_20261028_B_CatalogosPorCuenta'', N''10.0.10'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD [EmpresaAltaId] uniqueidentifier NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'ALTER TABLE [Clientes] ADD [EmpresaAltaId] uniqueidentifier NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'SELECT x.Entidad, x.EntidadId, x.EmpresaId,
           ROW_NUMBER() OVER (PARTITION BY x.Entidad, x.EntidadId
                              ORDER BY CASE WHEN x.Accion LIKE ''%[_]creado'' THEN 0 ELSE 1 END, x.MomentoUtc) AS N
    INTO #Altas
    FROM dbo.Bitacora AS x
    WHERE x.Accion IN (''cliente_creado'', ''cliente_renumerado'', ''producto_creado'', ''producto_renumerado'')
      AND x.EmpresaId IS NOT NULL;

    UPDATE c SET c.EmpresaAltaId = a.EmpresaId
    FROM dbo.Clientes AS c
    JOIN #Altas AS a ON a.Entidad = ''Cliente'' AND a.N = 1 AND a.EntidadId = LOWER(CONVERT(nvarchar(36), c.Id))
    JOIN dbo.Empresas AS e ON e.Id = a.EmpresaId AND e.CuentaId = c.CuentaId;

    UPDATE p SET p.EmpresaAltaId = a.EmpresaId
    FROM dbo.Productos AS p
    JOIN #Altas AS a ON a.Entidad = ''Producto'' AND a.N = 1 AND a.EntidadId = LOWER(CONVERT(nvarchar(36), p.Id))
    JOIN dbo.Empresas AS e ON e.Id = a.EmpresaId AND e.CuentaId = p.CuentaId;

    UPDATE c SET c.EmpresaAltaId = (SELECT MIN(CONVERT(nvarchar(36), e.Id)) FROM dbo.Empresas AS e WHERE e.CuentaId = c.CuentaId)
    FROM dbo.Clientes AS c
    WHERE c.EmpresaAltaId IS NULL
      AND (SELECT COUNT(*) FROM dbo.Empresas AS e WHERE e.CuentaId = c.CuentaId) = 1;

    UPDATE p SET p.EmpresaAltaId = (SELECT MIN(CONVERT(nvarchar(36), e.Id)) FROM dbo.Empresas AS e WHERE e.CuentaId = p.CuentaId)
    FROM dbo.Productos AS p
    WHERE p.EmpresaAltaId IS NULL
      AND (SELECT COUNT(*) FROM dbo.Empresas AS e WHERE e.CuentaId = p.CuentaId) = 1;

    DROP TABLE #Altas;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Productos_EmpresaAltaId] ON [Productos] ([EmpresaAltaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Clientes_EmpresaAltaId] ON [Clientes] ([EmpresaAltaId]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'ALTER TABLE [Clientes] ADD CONSTRAINT [FK_Clientes_Empresas_EmpresaAltaId] FOREIGN KEY ([EmpresaAltaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD CONSTRAINT [FK_Productos_Empresas_EmpresaAltaId] FOREIGN KEY ([EmpresaAltaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261028184729_20261028_C_EmpresaDeAltaEnCatalogos'', N''10.0.10'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028213928_20261029_A_IdInternoYDetalleDeProducto'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD [Detalle] nvarchar(1000) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028213928_20261029_A_IdInternoYDetalleDeProducto'
)
BEGIN
    EXEC(N'ALTER TABLE [Productos] ADD [IdInterno] nvarchar(100) NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028213928_20261029_A_IdInternoYDetalleDeProducto'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Productos_CuentaId_IdInterno] ON [Productos] ([CuentaId], [IdInterno]);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028213928_20261029_A_IdInternoYDetalleDeProducto'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261028213928_20261029_A_IdInternoYDetalleDeProducto'', N''10.0.10'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029220708_20261029_B_MonedasDeEmpresa'
)
BEGIN
    EXEC(N'CREATE TABLE [MonedasDeEmpresa] (
        [EmpresaId] uniqueidentifier NOT NULL,
        [Moneda] varchar(3) NOT NULL,
        [Activa] bit NOT NULL,
        [ModificadaUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_MonedasDeEmpresa] PRIMARY KEY ([EmpresaId], [Moneda]),
        CONSTRAINT [FK_MonedasDeEmpresa_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029220708_20261029_B_MonedasDeEmpresa'
)
BEGIN
    EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] ON;
    EXEC(N''INSERT INTO [Permisos] ([Clave], [Descripcion])
    VALUES (N''''configuracion_monedas'''', N''''Configuración: Monedas'''')'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] OFF;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029220708_20261029_B_MonedasDeEmpresa'
)
BEGIN
    EXEC(N'INSERT INTO MonedasDeEmpresa (EmpresaId, Moneda, Activa, ModificadaUtc)
    SELECT u.EmpresaId, u.Moneda, 1, SYSUTCDATETIME()
    FROM (SELECT EmpresaId, Moneda FROM Comprobantes
          UNION
          SELECT EmpresaId, MonedaP FROM Pagos) AS u
    WHERE u.Moneda NOT IN (''MXN'', ''XXX'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029220708_20261029_B_MonedasDeEmpresa'
)
BEGIN
    EXEC(N'INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
    SELECT p.UsuarioId, p.EmpresaId, ''configuracion_monedas'', p.OtorgadoUtc, p.OtorgadoPorUsuarioId
    FROM UsuariosEmpresasPermisos p
    WHERE p.PermisoClave = ''configuracion_general''
      AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                      WHERE x.UsuarioId = p.UsuarioId AND x.EmpresaId = p.EmpresaId
                        AND x.PermisoClave = ''configuracion_monedas'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029220708_20261029_B_MonedasDeEmpresa'
)
BEGIN
    EXEC(N'INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N''20261029220708_20261029_B_MonedasDeEmpresa'', N''10.0.10'');');
END;

    DECLARE @Objeto sysname, @Principal sysname, @Permiso nvarchar(128), @Estado char(1), @SqlPermiso nvarchar(max);
    DECLARE PermisosProcedimientos CURSOR LOCAL FAST_FORWARD FOR
        SELECT Objeto, Principal, Permiso, Estado FROM @Permisos;
    OPEN PermisosProcedimientos;
    FETCH NEXT FROM PermisosProcedimientos INTO @Objeto, @Principal, @Permiso, @Estado;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @SqlPermiso = CASE WHEN @Estado = 'D' THEN N'DENY ' ELSE N'GRANT ' END
            + @Permiso + N' ON OBJECT::[dbo].' + QUOTENAME(@Objeto) + N' TO ' + QUOTENAME(@Principal)
            + CASE WHEN @Estado = 'W' THEN N' WITH GRANT OPTION' ELSE N'' END + N';';
        EXEC(@SqlPermiso);
        FETCH NEXT FROM PermisosProcedimientos INTO @Objeto, @Principal, @Permiso, @Estado;
    END;
    CLOSE PermisosProcedimientos;
    DEALLOCATE PermisosProcedimientos;

    IF EXISTS (SELECT 1 FROM @Esperadas e
               WHERE NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory h WHERE h.MigrationId = e.MigrationId))
        THROW 51008, N'No se completaron todas las migraciones.', 1;
    IF COL_LENGTH(N'dbo.Clientes', N'CuentaId') IS NULL
        OR COL_LENGTH(N'dbo.Productos', N'IdInterno') IS NULL
        OR COL_LENGTH(N'dbo.Productos', N'Detalle') IS NULL
        OR COL_LENGTH(N'dbo.Cuentas', N'LimiteDeUsuarios') IS NULL
        OR OBJECT_ID(N'dbo.MonedasDeEmpresa', N'U') IS NULL
        THROW 51009, N'La comprobación final del esquema no coincide.', 1;

    COMMIT TRANSACTION;
    PRINT N'Actualización terminada. La base tiene las 67 migraciones de esta versión.';
    SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory
    WHERE MigrationId >= N'20261027180755_20261027_A_SubpermisosPorTipo' ORDER BY MigrationId;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
