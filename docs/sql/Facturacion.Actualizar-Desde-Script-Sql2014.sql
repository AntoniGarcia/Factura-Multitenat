SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF @@TRANCOUNT<>0
    THROW 51100, N'Ejecuta el archivo en una ventana sin transacciones abiertas.', 1;
BEGIN TRY
    IF DB_ID()<=4 THROW 51101, N'Selecciona la base del sistema, no una base de SQL Server.', 1;
    IF CONVERT(int,SERVERPROPERTY('ProductMajorVersion'))<12
        THROW 51102, N'Se requiere SQL Server 2014 o posterior.', 1;
    IF ISNULL(FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'),0)<>1
        THROW 51110, N'Instala la característica de búsqueda de texto completo de SQL Server antes de actualizar.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_languages WHERE lcid=3082)
        THROW 51111, N'Falta el idioma español 3082 de búsqueda de texto completo.', 1;
    IF OBJECT_ID(N'dbo.ConfiguracionesEmpresa',N'U') IS NULL
        THROW 51112, N'Falta la tabla ConfiguracionesEmpresa.', 1;
    IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name=N'CatalogoTextoCompleto' AND is_accent_sensitivity_on<>1)
        THROW 51113, N'CatalogoTextoCompleto tiene una configuración distinta a la principal.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name=N'CatalogoTextoCompleto')
       AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CREATE FULLTEXT CATALOG'),0)<>1
        THROW 51114, N'El usuario SQL necesita permiso para crear el catálogo de texto completo.', 1;
    DECLARE @Texto TABLE (Tabla sysname, Indice sysname);
    INSERT INTO @Texto VALUES
        (N'SatClaveProdServ',N'PK_SatClaveProdServ'),
        (N'SatColonia',N'IX_SatColonia_IdInterno'),
        (N'SatMunicipio',N'IX_SatMunicipio_IdInterno');
    DECLARE @ColumnasTexto TABLE (Tabla sysname, Columna sysname);
    INSERT INTO @ColumnasTexto VALUES
        (N'SatClaveProdServ',N'Descripcion'),(N'SatClaveProdServ',N'PalabrasSimilares'),
        (N'SatColonia',N'Nombre'),(N'SatMunicipio',N'Descripcion');
    IF EXISTS (SELECT 1 FROM @Texto t
        WHERE OBJECT_ID(N'dbo.'+QUOTENAME(t.Tabla),N'U') IS NULL
           OR NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Tabla))
                          AND i.name=t.Indice AND i.is_unique=1 AND i.is_disabled=0))
        THROW 51115, N'Falta una tabla o llave única de los catálogos SAT.', 1;
    IF EXISTS (SELECT 1 FROM @Texto t WHERE NOT EXISTS
        (SELECT 1 FROM sys.fulltext_indexes f WHERE f.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Tabla)))
        AND ISNULL(HAS_PERMS_BY_NAME(N'dbo.'+t.Tabla,N'OBJECT',N'ALTER'),0)<>1)
        THROW 51116, N'El usuario SQL necesita permiso ALTER en los catálogos SAT.', 1;
    IF EXISTS (SELECT 1 FROM @Texto t
        JOIN sys.fulltext_indexes f ON f.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Tabla))
        JOIN sys.fulltext_catalogs g ON g.fulltext_catalog_id=f.fulltext_catalog_id
        JOIN sys.indexes i ON i.object_id=f.object_id AND i.index_id=f.unique_index_id
        WHERE g.name<>N'CatalogoTextoCompleto' OR i.name<>t.Indice
           OR EXISTS (SELECT 1 FROM @ColumnasTexto e WHERE e.Tabla=t.Tabla AND NOT EXISTS
                (SELECT 1 FROM sys.fulltext_index_columns x JOIN sys.columns c ON c.object_id=x.object_id AND c.column_id=x.column_id
                 WHERE x.object_id=f.object_id AND c.name=e.Columna AND x.language_id=3082 AND ISNULL(x.type_column_id,0)=0))
           OR EXISTS (SELECT 1 FROM sys.fulltext_index_columns x JOIN sys.columns c ON c.object_id=x.object_id AND c.column_id=x.column_id
                WHERE x.object_id=f.object_id AND NOT EXISTS
                    (SELECT 1 FROM @ColumnasTexto e WHERE e.Tabla=t.Tabla AND e.Columna=c.name)))
        THROW 51117, N'Un índice de texto completo tiene una definición distinta a la principal.', 1;
    DECLARE @Bloqueo int;
    EXEC @Bloqueo=sys.sp_getapplock @Resource=N'Facturacion.ActualizarEsquema.v1',
        @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0;
    IF @Bloqueo<0 THROW 51103, N'Hay otra actualización en curso.', 1;
    BEGIN TRANSACTION;
    DECLARE @Tablas TABLE (Version tinyint, Tabla sysname);
    DECLARE @Columnas TABLE (Version tinyint, Tabla sysname, Columna sysname, Tipo sysname,
        Longitud smallint, Precision tinyint, Escala tinyint, Nula bit);
    DECLARE @Indices TABLE (Version tinyint, Tabla sysname, Nombre sysname, Unico bit,
        Columnas nvarchar(1000), Filtrado bit, Primaria bit);
    DECLARE @Claves TABLE (Version tinyint, Tabla sysname, Nombre sysname, Columnas nvarchar(1000),
        Principal sysname, ColumnasPrincipal nvarchar(1000), Borrado tinyint);
    DECLARE @Problemas TABLE (Version tinyint, Tipo nvarchar(50), Objeto nvarchar(500));
    DECLARE @PermisosEsperados TABLE (Clave nvarchar(100));
INSERT INTO @Tablas VALUES
    (0, N'BolsasTimbres'),
    (0, N'Clientes'),
    (0, N'ComprasTimbres'),
    (0, N'Cuentas'),
    (0, N'MovimientosTimbre'),
    (0, N'Paquetes'),
    (0, N'Productos'),
    (0, N'ProductosImpuestos'),
    (1, N'BolsasTimbres'),
    (1, N'Clientes'),
    (1, N'ComprasTimbres'),
    (1, N'Cuentas'),
    (1, N'MonedasDeEmpresa'),
    (1, N'MovimientosTimbre'),
    (1, N'Paquetes'),
    (1, N'Productos'),
    (1, N'ProductosImpuestos');
INSERT INTO @Columnas VALUES
    (0, N'BolsasTimbres', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'BolsasTimbres', N'ActualizadaUtc', N'datetime2', NULL, NULL, 7, 0),
    (0, N'BolsasTimbres', N'Disponibles', N'int', NULL, NULL, NULL, 0),
    (0, N'BolsasTimbres', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'BolsasTimbres', N'Reservados', N'int', NULL, NULL, NULL, 0),
    (0, N'Clientes', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'Clientes', N'Activo', N'bit', NULL, NULL, NULL, 0),
    (0, N'Clientes', N'Calle', N'nvarchar', 256, NULL, NULL, 1),
    (0, N'Clientes', N'ClaveInterna', N'int', NULL, NULL, NULL, 0),
    (0, N'Clientes', N'CodigoPostal', N'nvarchar', 10, NULL, NULL, 1),
    (0, N'Clientes', N'Colonia', N'nvarchar', 256, NULL, NULL, 1),
    (0, N'Clientes', N'CorreoPrincipal', N'nvarchar', 508, NULL, NULL, 1),
    (0, N'Clientes', N'DomicilioFiscalCp', N'nvarchar', 10, NULL, NULL, 0),
    (0, N'Clientes', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'Clientes', N'Estado', N'nvarchar', 256, NULL, NULL, 1),
    (0, N'Clientes', N'FechaAltaUtc', N'datetime2', NULL, NULL, 7, 0),
    (0, N'Clientes', N'FechaModificacionUtc', N'datetime2', NULL, NULL, 7, 1),
    (0, N'Clientes', N'FormaPagoPreferida', N'nvarchar', 8, NULL, NULL, 1),
    (0, N'Clientes', N'Localidad', N'nvarchar', 256, NULL, NULL, 1),
    (0, N'Clientes', N'MetodoPagoPreferido', N'nvarchar', 6, NULL, NULL, 1),
    (0, N'Clientes', N'Municipio', N'nvarchar', 256, NULL, NULL, 1),
    (0, N'Clientes', N'Nombre', N'nvarchar', 508, NULL, NULL, 0),
    (0, N'Clientes', N'NumRegIdTrib', N'nvarchar', 80, NULL, NULL, 1),
    (0, N'Clientes', N'NumeroExterior', N'nvarchar', 64, NULL, NULL, 1),
    (0, N'Clientes', N'NumeroInterior', N'nvarchar', 64, NULL, NULL, 1),
    (0, N'Clientes', N'Pais', N'nvarchar', 128, NULL, NULL, 1),
    (0, N'Clientes', N'Referencia', N'nvarchar', 512, NULL, NULL, 1),
    (0, N'Clientes', N'RegimenFiscal', N'nvarchar', 6, NULL, NULL, 0),
    (0, N'Clientes', N'ResidenciaFiscal', N'nvarchar', 6, NULL, NULL, 1),
    (0, N'Clientes', N'Rfc', N'nvarchar', 26, NULL, NULL, 0),
    (0, N'Clientes', N'Telefono', N'nvarchar', 64, NULL, NULL, 1),
    (0, N'Clientes', N'UsoCfdiPreferido', N'nvarchar', 8, NULL, NULL, 1),
    (0, N'ComprasTimbres', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'ComprasTimbres', N'AcreditadaUtc', N'datetime2', NULL, NULL, 7, 1),
    (0, N'ComprasTimbres', N'CanceladaUtc', N'datetime2', NULL, NULL, 7, 1),
    (0, N'ComprasTimbres', N'CantidadTimbres', N'int', NULL, NULL, NULL, 0),
    (0, N'ComprasTimbres', N'CreadaUtc', N'datetime2', NULL, NULL, 7, 0),
    (0, N'ComprasTimbres', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'ComprasTimbres', N'EmpresaNombreAlComprar', N'nvarchar', 508, NULL, NULL, 0),
    (0, N'ComprasTimbres', N'EmpresaRfcAlComprar', N'nvarchar', 26, NULL, NULL, 0),
    (0, N'ComprasTimbres', N'Estado', N'nvarchar', 40, NULL, NULL, 0),
    (0, N'ComprasTimbres', N'Iva', N'decimal', NULL, 18, 6, 0),
    (0, N'ComprasTimbres', N'MotivoCancelacion', N'nvarchar', 600, NULL, NULL, 1),
    (0, N'ComprasTimbres', N'NombrePaquete', N'nvarchar', 200, NULL, NULL, 0),
    (0, N'ComprasTimbres', N'PaqueteId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'ComprasTimbres', N'PrecioPorTimbre', N'decimal', NULL, 18, 6, 0),
    (0, N'ComprasTimbres', N'PrecioTotal', N'decimal', NULL, 18, 6, 0),
    (0, N'ComprasTimbres', N'Subtotal', N'decimal', NULL, 18, 6, 0),
    (0, N'ComprasTimbres', N'TasaIva', N'decimal', NULL, 18, 6, 0),
    (0, N'ComprasTimbres', N'UsuarioId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'ComprasTimbres', N'VenceUtc', N'datetime2', NULL, NULL, 7, 1),
    (0, N'ComprasTimbres', N'VigenciaMeses', N'int', NULL, NULL, NULL, 0),
    (0, N'Cuentas', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'Cuentas', N'Activa', N'bit', NULL, NULL, NULL, 0),
    (0, N'Cuentas', N'CorreoContacto', N'nvarchar', 508, NULL, NULL, 0),
    (0, N'Cuentas', N'FechaAltaUtc', N'datetime2', NULL, NULL, 7, 0),
    (0, N'Cuentas', N'Nombre', N'nvarchar', 508, NULL, NULL, 0),
    (0, N'MovimientosTimbre', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'MovimientosTimbre', N'CompraId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (0, N'MovimientosTimbre', N'DeltaDisponible', N'int', NULL, NULL, NULL, 0),
    (0, N'MovimientosTimbre', N'DeltaReservado', N'int', NULL, NULL, NULL, 0),
    (0, N'MovimientosTimbre', N'DisponiblesDespues', N'int', NULL, NULL, NULL, 0),
    (0, N'MovimientosTimbre', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'MovimientosTimbre', N'MomentoUtc', N'datetime2', NULL, NULL, 7, 0),
    (0, N'MovimientosTimbre', N'Motivo', N'nvarchar', 600, NULL, NULL, 1),
    (0, N'MovimientosTimbre', N'ReservaId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (0, N'MovimientosTimbre', N'ReservadosDespues', N'int', NULL, NULL, NULL, 0),
    (0, N'MovimientosTimbre', N'Tipo', N'nvarchar', 40, NULL, NULL, 0),
    (0, N'MovimientosTimbre', N'UsuarioId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (0, N'Paquetes', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'Paquetes', N'Activo', N'bit', NULL, NULL, NULL, 0),
    (0, N'Paquetes', N'CantidadTimbres', N'int', NULL, NULL, NULL, 0),
    (0, N'Paquetes', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (0, N'Paquetes', N'Nombre', N'nvarchar', 200, NULL, NULL, 0),
    (0, N'Paquetes', N'Orden', N'int', NULL, NULL, NULL, 0),
    (0, N'Paquetes', N'PrecioPorTimbre', N'decimal', NULL, 18, 6, 0),
    (0, N'Paquetes', N'PrecioTotal', N'decimal', NULL, 18, 6, 0),
    (0, N'Paquetes', N'VigenciaMeses', N'int', NULL, NULL, NULL, 0),
    (0, N'Productos', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'Productos', N'Activo', N'bit', NULL, NULL, NULL, 0),
    (0, N'Productos', N'ClaveProdServ', N'nvarchar', 16, NULL, NULL, 0),
    (0, N'Productos', N'ClaveUnidad', N'nvarchar', 40, NULL, NULL, 0),
    (0, N'Productos', N'CodigoInterno', N'int', NULL, NULL, NULL, 0),
    (0, N'Productos', N'Descripcion', N'nvarchar', 2000, NULL, NULL, 0),
    (0, N'Productos', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'Productos', N'FechaAltaUtc', N'datetime2', NULL, NULL, 7, 0),
    (0, N'Productos', N'FechaModificacionUtc', N'datetime2', NULL, NULL, 7, 1),
    (0, N'Productos', N'ObjetoImp', N'nvarchar', 4, NULL, NULL, 0),
    (0, N'Productos', N'PesoKg', N'decimal', NULL, 18, 6, 1),
    (0, N'Productos', N'UnidadTexto', N'nvarchar', 128, NULL, NULL, 0),
    (0, N'Productos', N'ValorUnitario', N'decimal', NULL, 18, 6, 0),
    (0, N'ProductosImpuestos', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'ProductosImpuestos', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'ProductosImpuestos', N'EsRetencion', N'bit', NULL, NULL, NULL, 0),
    (0, N'ProductosImpuestos', N'Impuesto', N'nvarchar', 6, NULL, NULL, 0),
    (0, N'ProductosImpuestos', N'ProductoId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (0, N'ProductosImpuestos', N'TasaOCuota', N'decimal', NULL, 18, 6, 1),
    (0, N'ProductosImpuestos', N'TipoFactor', N'nvarchar', 32, NULL, NULL, 0),
    (1, N'BolsasTimbres', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'BolsasTimbres', N'ActualizadaUtc', N'datetime2', NULL, NULL, 7, 0),
    (1, N'BolsasTimbres', N'CuentaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'BolsasTimbres', N'Disponibles', N'int', NULL, NULL, NULL, 0),
    (1, N'BolsasTimbres', N'Reservados', N'int', NULL, NULL, NULL, 0),
    (1, N'Clientes', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'Clientes', N'Activo', N'bit', NULL, NULL, NULL, 0),
    (1, N'Clientes', N'Calle', N'nvarchar', 256, NULL, NULL, 1),
    (1, N'Clientes', N'ClaveInterna', N'int', NULL, NULL, NULL, 0),
    (1, N'Clientes', N'CodigoPostal', N'nvarchar', 10, NULL, NULL, 1),
    (1, N'Clientes', N'Colonia', N'nvarchar', 256, NULL, NULL, 1),
    (1, N'Clientes', N'CorreoPrincipal', N'nvarchar', 508, NULL, NULL, 1),
    (1, N'Clientes', N'CuentaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'Clientes', N'DomicilioFiscalCp', N'nvarchar', 10, NULL, NULL, 0),
    (1, N'Clientes', N'EmpresaAltaId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (1, N'Clientes', N'Estado', N'nvarchar', 256, NULL, NULL, 1),
    (1, N'Clientes', N'FechaAltaUtc', N'datetime2', NULL, NULL, 7, 0),
    (1, N'Clientes', N'FechaModificacionUtc', N'datetime2', NULL, NULL, 7, 1),
    (1, N'Clientes', N'FormaPagoPreferida', N'nvarchar', 8, NULL, NULL, 1),
    (1, N'Clientes', N'Localidad', N'nvarchar', 256, NULL, NULL, 1),
    (1, N'Clientes', N'MetodoPagoPreferido', N'nvarchar', 6, NULL, NULL, 1),
    (1, N'Clientes', N'Municipio', N'nvarchar', 256, NULL, NULL, 1),
    (1, N'Clientes', N'Nombre', N'nvarchar', 508, NULL, NULL, 0),
    (1, N'Clientes', N'NumRegIdTrib', N'nvarchar', 80, NULL, NULL, 1),
    (1, N'Clientes', N'NumeroExterior', N'nvarchar', 64, NULL, NULL, 1),
    (1, N'Clientes', N'NumeroInterior', N'nvarchar', 64, NULL, NULL, 1),
    (1, N'Clientes', N'Pais', N'nvarchar', 128, NULL, NULL, 1),
    (1, N'Clientes', N'Referencia', N'nvarchar', 512, NULL, NULL, 1),
    (1, N'Clientes', N'RegimenFiscal', N'nvarchar', 6, NULL, NULL, 0),
    (1, N'Clientes', N'ResidenciaFiscal', N'nvarchar', 6, NULL, NULL, 1),
    (1, N'Clientes', N'Rfc', N'nvarchar', 26, NULL, NULL, 0),
    (1, N'Clientes', N'Telefono', N'nvarchar', 64, NULL, NULL, 1),
    (1, N'Clientes', N'UsoCfdiPreferido', N'nvarchar', 8, NULL, NULL, 1),
    (1, N'ComprasTimbres', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'AcreditadaUtc', N'datetime2', NULL, NULL, 7, 1),
    (1, N'ComprasTimbres', N'CanceladaUtc', N'datetime2', NULL, NULL, 7, 1),
    (1, N'ComprasTimbres', N'CantidadTimbres', N'int', NULL, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'CreadaUtc', N'datetime2', NULL, NULL, 7, 0),
    (1, N'ComprasTimbres', N'CuentaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'EmpresaNombreAlComprar', N'nvarchar', 508, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'EmpresaRfcAlComprar', N'nvarchar', 26, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'Estado', N'nvarchar', 40, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'Iva', N'decimal', NULL, 18, 6, 0),
    (1, N'ComprasTimbres', N'MotivoCancelacion', N'nvarchar', 600, NULL, NULL, 1),
    (1, N'ComprasTimbres', N'NombrePaquete', N'nvarchar', 200, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'PaqueteId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'PrecioPorTimbre', N'decimal', NULL, 18, 6, 0),
    (1, N'ComprasTimbres', N'PrecioTotal', N'decimal', NULL, 18, 6, 0),
    (1, N'ComprasTimbres', N'Subtotal', N'decimal', NULL, 18, 6, 0),
    (1, N'ComprasTimbres', N'TasaIva', N'decimal', NULL, 18, 6, 0),
    (1, N'ComprasTimbres', N'UsuarioId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'ComprasTimbres', N'VenceUtc', N'datetime2', NULL, NULL, 7, 1),
    (1, N'ComprasTimbres', N'VigenciaMeses', N'int', NULL, NULL, NULL, 0),
    (1, N'Cuentas', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'Cuentas', N'Activa', N'bit', NULL, NULL, NULL, 0),
    (1, N'Cuentas', N'CorreoContacto', N'nvarchar', 508, NULL, NULL, 0),
    (1, N'Cuentas', N'FechaAltaUtc', N'datetime2', NULL, NULL, 7, 0),
    (1, N'Cuentas', N'LimiteDeEmpresas', N'int', NULL, NULL, NULL, 0),
    (1, N'Cuentas', N'LimiteDeUsuarios', N'int', NULL, NULL, NULL, 0),
    (1, N'Cuentas', N'Nombre', N'nvarchar', 508, NULL, NULL, 0),
    (1, N'MonedasDeEmpresa', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'MonedasDeEmpresa', N'Moneda', N'varchar', 3, NULL, NULL, 0),
    (1, N'MonedasDeEmpresa', N'Activa', N'bit', NULL, NULL, NULL, 0),
    (1, N'MonedasDeEmpresa', N'ModificadaUtc', N'datetime2', NULL, NULL, 7, 0),
    (1, N'MovimientosTimbre', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'MovimientosTimbre', N'CompraId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (1, N'MovimientosTimbre', N'CuentaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'MovimientosTimbre', N'DeltaDisponible', N'int', NULL, NULL, NULL, 0),
    (1, N'MovimientosTimbre', N'DeltaReservado', N'int', NULL, NULL, NULL, 0),
    (1, N'MovimientosTimbre', N'DisponiblesDespues', N'int', NULL, NULL, NULL, 0),
    (1, N'MovimientosTimbre', N'EmpresaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'MovimientosTimbre', N'MomentoUtc', N'datetime2', NULL, NULL, 7, 0),
    (1, N'MovimientosTimbre', N'Motivo', N'nvarchar', 600, NULL, NULL, 1),
    (1, N'MovimientosTimbre', N'ReservaId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (1, N'MovimientosTimbre', N'ReservadosDespues', N'int', NULL, NULL, NULL, 0),
    (1, N'MovimientosTimbre', N'Tipo', N'nvarchar', 40, NULL, NULL, 0),
    (1, N'MovimientosTimbre', N'UsuarioId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (1, N'Paquetes', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'Paquetes', N'Activo', N'bit', NULL, NULL, NULL, 0),
    (1, N'Paquetes', N'CantidadTimbres', N'int', NULL, NULL, NULL, 0),
    (1, N'Paquetes', N'CuentaId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (1, N'Paquetes', N'Nombre', N'nvarchar', 200, NULL, NULL, 0),
    (1, N'Paquetes', N'Orden', N'int', NULL, NULL, NULL, 0),
    (1, N'Paquetes', N'PrecioPorTimbre', N'decimal', NULL, 18, 6, 0),
    (1, N'Paquetes', N'PrecioTotal', N'decimal', NULL, 18, 6, 0),
    (1, N'Paquetes', N'VigenciaMeses', N'int', NULL, NULL, NULL, 0),
    (1, N'Productos', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'Productos', N'Activo', N'bit', NULL, NULL, NULL, 0),
    (1, N'Productos', N'ClaveProdServ', N'nvarchar', 16, NULL, NULL, 0),
    (1, N'Productos', N'ClaveUnidad', N'nvarchar', 40, NULL, NULL, 0),
    (1, N'Productos', N'CodigoInterno', N'int', NULL, NULL, NULL, 0),
    (1, N'Productos', N'CuentaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'Productos', N'Descripcion', N'nvarchar', 2000, NULL, NULL, 0),
    (1, N'Productos', N'Detalle', N'nvarchar', 2000, NULL, NULL, 1),
    (1, N'Productos', N'EmpresaAltaId', N'uniqueidentifier', NULL, NULL, NULL, 1),
    (1, N'Productos', N'FechaAltaUtc', N'datetime2', NULL, NULL, 7, 0),
    (1, N'Productos', N'FechaModificacionUtc', N'datetime2', NULL, NULL, 7, 1),
    (1, N'Productos', N'IdInterno', N'nvarchar', 200, NULL, NULL, 1),
    (1, N'Productos', N'ObjetoImp', N'nvarchar', 4, NULL, NULL, 0),
    (1, N'Productos', N'PesoKg', N'decimal', NULL, 18, 6, 1),
    (1, N'Productos', N'UnidadTexto', N'nvarchar', 128, NULL, NULL, 0),
    (1, N'Productos', N'ValorUnitario', N'decimal', NULL, 18, 6, 0),
    (1, N'ProductosImpuestos', N'Id', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'ProductosImpuestos', N'CuentaId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'ProductosImpuestos', N'EsRetencion', N'bit', NULL, NULL, NULL, 0),
    (1, N'ProductosImpuestos', N'Impuesto', N'nvarchar', 6, NULL, NULL, 0),
    (1, N'ProductosImpuestos', N'ProductoId', N'uniqueidentifier', NULL, NULL, NULL, 0),
    (1, N'ProductosImpuestos', N'TasaOCuota', N'decimal', NULL, 18, 6, 1),
    (1, N'ProductosImpuestos', N'TipoFactor', N'nvarchar', 32, NULL, NULL, 0);
INSERT INTO @Indices VALUES
    (0, N'BolsasTimbres', N'IX_BolsasTimbres_EmpresaId', 1, N'EmpresaId', 0, 0),
    (0, N'BolsasTimbres', N'PK_BolsasTimbres', 1, N'Id', 0, 1),
    (0, N'Clientes', N'IX_Clientes_ClaveInternaPorEmpresa', 1, N'EmpresaId|ClaveInterna', 0, 0),
    (0, N'Clientes', N'IX_Clientes_EmpresaId_Activo_Nombre', 0, N'EmpresaId|Activo|Nombre', 0, 0),
    (0, N'Clientes', N'IX_Clientes_RfcPorEmpresa', 1, N'EmpresaId|Rfc', 1, 0),
    (0, N'Clientes', N'PK_Clientes', 1, N'Id', 0, 1),
    (0, N'ComprasTimbres', N'IX_ComprasTimbres_EmpresaId_CreadaUtc', 0, N'EmpresaId|CreadaUtc', 0, 0),
    (0, N'ComprasTimbres', N'IX_ComprasTimbres_Estado', 0, N'Estado', 1, 0),
    (0, N'ComprasTimbres', N'PK_ComprasTimbres', 1, N'Id', 0, 1),
    (0, N'Cuentas', N'PK_Cuentas', 1, N'Id', 0, 1),
    (0, N'MovimientosTimbre', N'IX_MovimientosTimbre_EmpresaId_MomentoUtc', 0, N'EmpresaId|MomentoUtc', 0, 0),
    (0, N'MovimientosTimbre', N'PK_MovimientosTimbre', 1, N'Id', 0, 1),
    (0, N'Paquetes', N'IX_Paquetes_EmpresaId_Activo_Orden', 0, N'EmpresaId|Activo|Orden', 0, 0),
    (0, N'Paquetes', N'PK_Paquetes', 1, N'Id', 0, 1),
    (0, N'Productos', N'IX_Productos_CodigoInternoPorEmpresa', 1, N'EmpresaId|CodigoInterno', 0, 0),
    (0, N'Productos', N'IX_Productos_EmpresaId_Activo_Descripcion', 0, N'EmpresaId|Activo|Descripcion', 0, 0),
    (0, N'Productos', N'PK_Productos', 1, N'Id', 0, 1),
    (0, N'ProductosImpuestos', N'IX_ProductosImpuestos_SinImpuestoRepetido', 1, N'ProductoId|Impuesto|EsRetencion', 0, 0),
    (0, N'ProductosImpuestos', N'PK_ProductosImpuestos', 1, N'Id', 0, 1),
    (1, N'BolsasTimbres', N'IX_BolsasTimbres_CuentaId', 1, N'CuentaId', 0, 0),
    (1, N'BolsasTimbres', N'PK_BolsasTimbres', 1, N'Id', 0, 1),
    (1, N'Clientes', N'IX_Clientes_ClaveInternaPorCuenta', 1, N'CuentaId|ClaveInterna', 0, 0),
    (1, N'Clientes', N'IX_Clientes_CuentaId_Activo_Nombre', 0, N'CuentaId|Activo|Nombre', 0, 0),
    (1, N'Clientes', N'IX_Clientes_EmpresaAltaId', 0, N'EmpresaAltaId', 0, 0),
    (1, N'Clientes', N'IX_Clientes_RfcPorCuenta', 1, N'CuentaId|Rfc', 1, 0),
    (1, N'Clientes', N'PK_Clientes', 1, N'Id', 0, 1),
    (1, N'ComprasTimbres', N'IX_ComprasTimbres_CuentaId_CreadaUtc', 0, N'CuentaId|CreadaUtc', 0, 0),
    (1, N'ComprasTimbres', N'IX_ComprasTimbres_Estado', 0, N'Estado', 1, 0),
    (1, N'ComprasTimbres', N'PK_ComprasTimbres', 1, N'Id', 0, 1),
    (1, N'Cuentas', N'PK_Cuentas', 1, N'Id', 0, 1),
    (1, N'MonedasDeEmpresa', N'PK_MonedasDeEmpresa', 1, N'EmpresaId|Moneda', 0, 1),
    (1, N'MovimientosTimbre', N'IX_MovimientosTimbre_CuentaId_MomentoUtc', 0, N'CuentaId|MomentoUtc', 0, 0),
    (1, N'MovimientosTimbre', N'PK_MovimientosTimbre', 1, N'Id', 0, 1),
    (1, N'Paquetes', N'IX_Paquetes_CuentaId_Activo_Orden', 0, N'CuentaId|Activo|Orden', 0, 0),
    (1, N'Paquetes', N'PK_Paquetes', 1, N'Id', 0, 1),
    (1, N'Productos', N'IX_Productos_CodigoInternoPorCuenta', 1, N'CuentaId|CodigoInterno', 0, 0),
    (1, N'Productos', N'IX_Productos_CuentaId_Activo_Descripcion', 0, N'CuentaId|Activo|Descripcion', 0, 0),
    (1, N'Productos', N'IX_Productos_CuentaId_IdInterno', 0, N'CuentaId|IdInterno', 0, 0),
    (1, N'Productos', N'IX_Productos_EmpresaAltaId', 0, N'EmpresaAltaId', 0, 0),
    (1, N'Productos', N'PK_Productos', 1, N'Id', 0, 1),
    (1, N'ProductosImpuestos', N'IX_ProductosImpuestos_SinImpuestoRepetido', 1, N'ProductoId|Impuesto|EsRetencion', 0, 0),
    (1, N'ProductosImpuestos', N'PK_ProductosImpuestos', 1, N'Id', 0, 1);
INSERT INTO @Claves VALUES
    (0, N'BolsasTimbres', N'FK_BolsasTimbres_Empresas_EmpresaId', N'EmpresaId', N'Empresas', N'Id', 0),
    (0, N'Clientes', N'FK_Clientes_Empresas_EmpresaId', N'EmpresaId', N'Empresas', N'Id', 0),
    (0, N'Paquetes', N'FK_Paquetes_Empresas_EmpresaId', N'EmpresaId', N'Empresas', N'Id', 0),
    (0, N'Productos', N'FK_Productos_Empresas_EmpresaId', N'EmpresaId', N'Empresas', N'Id', 0),
    (0, N'ProductosImpuestos', N'FK_ProductosImpuestos_Productos_ProductoId', N'ProductoId', N'Productos', N'Id', 1),
    (1, N'BolsasTimbres', N'FK_BolsasTimbres_Cuentas_CuentaId', N'CuentaId', N'Cuentas', N'Id', 0),
    (1, N'Clientes', N'FK_Clientes_Cuentas_CuentaId', N'CuentaId', N'Cuentas', N'Id', 0),
    (1, N'Clientes', N'FK_Clientes_Empresas_EmpresaAltaId', N'EmpresaAltaId', N'Empresas', N'Id', 0),
    (1, N'MonedasDeEmpresa', N'FK_MonedasDeEmpresa_Empresas_EmpresaId', N'EmpresaId', N'Empresas', N'Id', 0),
    (1, N'Paquetes', N'FK_Paquetes_Cuentas_CuentaId', N'CuentaId', N'Cuentas', N'Id', 0),
    (1, N'Productos', N'FK_Productos_Cuentas_CuentaId', N'CuentaId', N'Cuentas', N'Id', 0),
    (1, N'Productos', N'FK_Productos_Empresas_EmpresaAltaId', N'EmpresaAltaId', N'Empresas', N'Id', 0),
    (1, N'ProductosImpuestos', N'FK_ProductosImpuestos_Productos_ProductoId', N'ProductoId', N'Productos', N'Id', 1);
INSERT INTO @PermisosEsperados VALUES
    (N'carta_porte_cancelar'),
    (N'carta_porte_crear'),
    (N'carta_porte_eliminar'),
    (N'carta_porte_enviar'),
    (N'carta_porte_modificar'),
    (N'carta_porte_timbrar'),
    (N'clientes_crear'),
    (N'clientes_desactivar'),
    (N'clientes_eliminar'),
    (N'clientes_modificar'),
    (N'comercio_exterior_cancelar'),
    (N'comercio_exterior_crear'),
    (N'comercio_exterior_eliminar'),
    (N'comercio_exterior_enviar'),
    (N'comercio_exterior_modificar'),
    (N'comercio_exterior_timbrar'),
    (N'configuracion_correo'),
    (N'configuracion_general'),
    (N'configuracion_impuestos'),
    (N'documentos_descarga'),
    (N'documentos_eliminar'),
    (N'documentos_listado'),
    (N'empresa_certificados'),
    (N'empresa_datos'),
    (N'factura_cancelar'),
    (N'factura_crear'),
    (N'factura_eliminar'),
    (N'factura_enviar'),
    (N'factura_modificar'),
    (N'factura_timbrar'),
    (N'figuras_crear'),
    (N'figuras_desactivar'),
    (N'figuras_eliminar'),
    (N'figuras_modificar'),
    (N'notaria_cancelar'),
    (N'notaria_crear'),
    (N'notaria_eliminar'),
    (N'notaria_enviar'),
    (N'notaria_modificar'),
    (N'notaria_timbrar'),
    (N'obra_cancelar'),
    (N'obra_crear'),
    (N'obra_eliminar'),
    (N'obra_enviar'),
    (N'obra_modificar'),
    (N'obra_timbrar'),
    (N'pago_cancelar'),
    (N'pago_crear'),
    (N'pago_eliminar'),
    (N'pago_enviar'),
    (N'pago_modificar'),
    (N'pago_timbrar'),
    (N'productos_crear'),
    (N'productos_desactivar'),
    (N'productos_eliminar'),
    (N'productos_modificar'),
    (N'series_crear'),
    (N'series_desactivar'),
    (N'series_eliminar'),
    (N'series_modificar'),
    (N'vehiculos_crear'),
    (N'vehiculos_desactivar'),
    (N'vehiculos_eliminar'),
    (N'vehiculos_modificar'),
    (N'comprar_timbres'),
    (N'configuracion_monedas');
    DELETE FROM @Problemas;
    INSERT INTO @Problemas
    SELECT e.Version, N'Tabla', e.Tabla FROM @Tablas e
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U') IS NULL;
    INSERT INTO @Problemas
    SELECT e.Version, N'Columna o tipo', e.Tabla + N'.' + e.Columna
    FROM @Columnas e
    LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U') AND c.name=e.Columna
    LEFT JOIN sys.types t ON t.user_type_id=c.user_type_id
    WHERE c.column_id IS NULL OR t.name<>e.Tipo OR c.is_nullable<>e.Nula
       OR (e.Longitud IS NOT NULL AND c.max_length<>e.Longitud)
       OR (e.Precision IS NOT NULL AND c.precision<>e.Precision)
       OR (e.Escala IS NOT NULL AND c.scale<>e.Escala);
    INSERT INTO @Problemas
    SELECT e.Version, N'Columna adicional', e.Tabla + N'.' + c.name
    FROM @Tablas e JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U')
    WHERE NOT EXISTS (SELECT 1 FROM @Columnas x WHERE x.Version=e.Version AND x.Tabla=e.Tabla AND x.Columna=c.name);
    INSERT INTO @Problemas
    SELECT v.Version, N'Tabla de otra versión', x.Tabla
    FROM (VALUES (0),(1)) v(Version)
    CROSS JOIN (SELECT DISTINCT Tabla FROM @Tablas) x
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(x.Tabla), N'U') IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM @Tablas e WHERE e.Version=v.Version AND e.Tabla=x.Tabla);
    INSERT INTO @Problemas
    SELECT e.Version, N'Índice o primaria', e.Tabla + N'.' + e.Nombre
    FROM @Indices e
    LEFT JOIN sys.indexes i ON i.object_id=OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U') AND i.name=e.Nombre
    OUTER APPLY (SELECT STUFF((SELECT N'|' + c.name FROM sys.index_columns ic
        JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0
        ORDER BY ic.key_ordinal FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'),1,1,N'') AS Columnas) k
    WHERE i.index_id IS NULL OR i.is_unique<>e.Unico OR i.has_filter<>e.Filtrado
       OR i.is_primary_key<>e.Primaria OR i.is_disabled=1 OR ISNULL(k.Columnas,N'')<>e.Columnas;
    INSERT INTO @Problemas
    SELECT e.Version, N'Llave foránea', e.Tabla + N'.' + e.Nombre
    FROM @Claves e
    LEFT JOIN sys.foreign_keys f ON f.parent_object_id=OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U') AND f.name=e.Nombre
    OUTER APPLY (SELECT STUFF((SELECT N'|' + c.name FROM sys.foreign_key_columns fc
        JOIN sys.columns c ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id
        WHERE fc.constraint_object_id=f.object_id ORDER BY fc.constraint_column_id
        FOR XML PATH(''),TYPE).value('.', 'nvarchar(max)'),1,1,N'') AS Columnas) k
    OUTER APPLY (SELECT STUFF((SELECT N'|' + c.name FROM sys.foreign_key_columns fc
        JOIN sys.columns c ON c.object_id=fc.referenced_object_id AND c.column_id=fc.referenced_column_id
        WHERE fc.constraint_object_id=f.object_id ORDER BY fc.constraint_column_id
        FOR XML PATH(''),TYPE).value('.', 'nvarchar(max)'),1,1,N'') AS Columnas) r
    WHERE f.object_id IS NULL OR OBJECT_NAME(f.referenced_object_id)<>e.Principal
       OR f.delete_referential_action<>e.Borrado OR f.is_disabled=1 OR f.is_not_trusted=1
       OR ISNULL(k.Columnas,N'')<>e.Columnas OR ISNULL(r.Columnas,N'')<>e.ColumnasPrincipal;
    DECLARE @Marca nvarchar(128), @Repetida bit=0;
    SELECT @Marca=CONVERT(nvarchar(128),value) FROM sys.extended_properties
    WHERE class=0 AND name=N'Facturacion.EstructuraCuenta.v1';
    IF @Marca IS NOT NULL AND @Marca<>N'20261031-Cuentas-Monedas'
        THROW 51104, N'Existe una actualización diferente con la misma marca. Revisar antes de continuar.', 1;
    IF @Marca IS NOT NULL SET @Repetida=1;
    IF EXISTS (SELECT 1 FROM @Problemas WHERE Version=CASE WHEN @Repetida=1 THEN 1 ELSE 0 END)
    BEGIN
        SELECT TOP (30) Tipo,Objeto FROM @Problemas
        WHERE Version=CASE WHEN @Repetida=1 THEN 1 ELSE 0 END ORDER BY Tipo,Objeto;
        THROW 51105, N'La estructura no coincide con script.sql ni con una actualización completa de este archivo. No se aplicaron cambios.', 1;
    END;
    IF @Repetida=0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave NOT IN (N'administrar_clientes',N'administrar_figuras',N'administrar_productos',N'administrar_vehiculos',N'cancelar',N'configuracion',N'emitir_carta_porte',N'emitir_comercio_exterior',N'emitir_factura',N'emitir_notaria',N'emitir_obra',N'emitir_pago',N'enviar_correo',N'mi_empresa',N'ver_documentos',N'comprar_timbres'))
            THROW 51106, N'Los permisos pertenecen a otra versión. Revisar antes de actualizar.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave=N'comprar_timbres')
            INSERT INTO dbo.Permisos (Clave,Descripcion) VALUES (N'comprar_timbres',N'Timbres');
        DECLARE @Permisos TABLE (Objeto sysname, Principal sysname, Permiso nvarchar(128), Estado char(1));
        INSERT INTO @Permisos
        SELECT o.name,u.name,p.permission_name,p.state
        FROM sys.database_permissions p JOIN sys.objects o ON o.object_id=p.major_id
        JOIN sys.database_principals u ON u.principal_id=p.grantee_principal_id
        WHERE p.class=1 AND p.minor_id=0 AND o.schema_id=SCHEMA_ID(N'dbo')
          AND o.name IN (N'ReservarTimbre',N'ConfirmarTimbre',N'DevolverTimbre',N'AcreditarCompra')
          AND p.state IN ('G','W','D');
EXEC(N'EXEC(N''UPDATE [Permisos] SET [Descripcion] = N''''Timbres''''
    WHERE [Clave] = N''''comprar_timbres'''';
    SELECT @@ROWCOUNT'');');
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
EXEC(N'DELETE FROM UsuariosEmpresasPermisos WHERE PermisoClave IN (''administrar_clientes'', ''administrar_figuras'', ''administrar_productos'', ''administrar_vehiculos'', ''cancelar'', ''configuracion'', ''emitir_carta_porte'', ''emitir_comercio_exterior'', ''emitir_factura'', ''emitir_notaria'', ''emitir_obra'', ''emitir_pago'', ''enviar_correo'', ''mi_empresa'', ''ver_documentos'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_clientes'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_figuras'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_productos'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''administrar_vehiculos'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''cancelar'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''configuracion'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_carta_porte'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_comercio_exterior'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_factura'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_notaria'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_obra'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''emitir_pago'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''enviar_correo'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''mi_empresa'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'EXEC(N''DELETE FROM [Permisos]
    WHERE [Clave] = N''''ver_documentos'''';
    SELECT @@ROWCOUNT'');');
EXEC(N'ALTER TABLE [Cuentas] ADD [LimiteDeEmpresas] int NOT NULL DEFAULT 1;');
EXEC(N'ALTER TABLE [Cuentas] ADD [LimiteDeUsuarios] int NOT NULL DEFAULT 3;');
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
EXEC(N'ALTER TABLE [BolsasTimbres] DROP CONSTRAINT [FK_BolsasTimbres_Empresas_EmpresaId];');
EXEC(N'ALTER TABLE [Paquetes] DROP CONSTRAINT [FK_Paquetes_Empresas_EmpresaId];');
EXEC(N'DROP INDEX [IX_BolsasTimbres_EmpresaId] ON [BolsasTimbres];');
EXEC(N'DROP INDEX [IX_Paquetes_EmpresaId_Activo_Orden] ON [Paquetes];');
EXEC(N'DROP INDEX [IX_MovimientosTimbre_EmpresaId_MomentoUtc] ON [MovimientosTimbre];');
EXEC(N'DROP INDEX [IX_ComprasTimbres_EmpresaId_CreadaUtc] ON [ComprasTimbres];');
EXEC(N'EXEC sp_rename N''[BolsasTimbres].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
EXEC(N'EXEC sp_rename N''[Paquetes].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
EXEC(N'ALTER TABLE [MovimientosTimbre] ADD [CuentaId] uniqueidentifier NOT NULL DEFAULT ''00000000-0000-0000-0000-000000000000'';');
EXEC(N'ALTER TABLE [ComprasTimbres] ADD [CuentaId] uniqueidentifier NOT NULL DEFAULT ''00000000-0000-0000-0000-000000000000'';');
EXEC(N'UPDATE p SET p.CuentaId = e.CuentaId
    FROM dbo.Paquetes AS p JOIN dbo.Empresas AS e ON e.Id = p.CuentaId;
    UPDATE m SET m.CuentaId = e.CuentaId
    FROM dbo.MovimientosTimbre AS m JOIN dbo.Empresas AS e ON e.Id = m.EmpresaId;
    UPDATE c SET c.CuentaId = e.CuentaId
    FROM dbo.ComprasTimbres AS c JOIN dbo.Empresas AS e ON e.Id = c.EmpresaId;
    UPDATE b SET b.CuentaId = e.CuentaId
    FROM dbo.BolsasTimbres AS b JOIN dbo.Empresas AS e ON e.Id = b.CuentaId;
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
EXEC(N'CREATE UNIQUE INDEX [IX_BolsasTimbres_CuentaId] ON [BolsasTimbres] ([CuentaId]);');
EXEC(N'CREATE INDEX [IX_Paquetes_CuentaId_Activo_Orden] ON [Paquetes] ([CuentaId], [Activo], [Orden]);');
EXEC(N'CREATE INDEX [IX_MovimientosTimbre_CuentaId_MomentoUtc] ON [MovimientosTimbre] ([CuentaId], [MomentoUtc]);');
EXEC(N'CREATE INDEX [IX_ComprasTimbres_CuentaId_CreadaUtc] ON [ComprasTimbres] ([CuentaId], [CreadaUtc]);');
EXEC(N'ALTER TABLE [BolsasTimbres] ADD CONSTRAINT [FK_BolsasTimbres_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
EXEC(N'ALTER TABLE [Paquetes] ADD CONSTRAINT [FK_Paquetes_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
EXEC(N'IF OBJECT_ID(N''dbo.ReservarTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.ReservarTimbre;');
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
EXEC(N'IF OBJECT_ID(N''dbo.ConfirmarTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.ConfirmarTimbre;');
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
EXEC(N'IF OBJECT_ID(N''dbo.DevolverTimbre'', N''P'') IS NOT NULL DROP PROCEDURE dbo.DevolverTimbre;');
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
EXEC(N'IF OBJECT_ID(N''dbo.AcreditarCompra'', N''P'') IS NOT NULL DROP PROCEDURE dbo.AcreditarCompra;');
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
EXEC(N'ALTER TABLE [Clientes] DROP CONSTRAINT [FK_Clientes_Empresas_EmpresaId];');
EXEC(N'ALTER TABLE [Productos] DROP CONSTRAINT [FK_Productos_Empresas_EmpresaId];');
EXEC(N'DROP INDEX [IX_Clientes_ClaveInternaPorEmpresa] ON [Clientes];');
EXEC(N'DROP INDEX [IX_Clientes_RfcPorEmpresa] ON [Clientes];');
EXEC(N'DROP INDEX [IX_Clientes_EmpresaId_Activo_Nombre] ON [Clientes];');
EXEC(N'DROP INDEX [IX_Productos_CodigoInternoPorEmpresa] ON [Productos];');
EXEC(N'DROP INDEX [IX_Productos_EmpresaId_Activo_Descripcion] ON [Productos];');
EXEC(N'EXEC sp_rename N''[Clientes].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
EXEC(N'EXEC sp_rename N''[Productos].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
EXEC(N'EXEC sp_rename N''[ProductosImpuestos].[EmpresaId]'', N''CuentaId'', ''COLUMN'';');
EXEC(N'SELECT c.Id, c.CuentaId AS EmpresaId INTO #ClientesOrigen FROM dbo.Clientes AS c;
    SELECT p.Id, p.CuentaId AS EmpresaId INTO #ProductosOrigen FROM dbo.Productos AS p;
    UPDATE c SET c.CuentaId = e.CuentaId
    FROM dbo.Clientes AS c JOIN dbo.Empresas AS e ON e.Id = c.CuentaId;
    UPDATE p SET p.CuentaId = e.CuentaId
    FROM dbo.Productos AS p JOIN dbo.Empresas AS e ON e.Id = p.CuentaId;
    UPDATE i SET i.CuentaId = p.CuentaId
    FROM dbo.ProductosImpuestos AS i JOIN dbo.Productos AS p ON p.Id = i.ProductoId;
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
EXEC(N'CREATE UNIQUE INDEX [IX_Clientes_ClaveInternaPorCuenta] ON [Clientes] ([CuentaId], [ClaveInterna]);');
EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_Clientes_RfcPorCuenta] ON [Clientes] ([CuentaId], [Rfc]) WHERE [Rfc] <> ''''XAXX010101000'''' AND [Rfc] <> ''''XEXX010101000'''''');');
EXEC(N'CREATE INDEX [IX_Clientes_CuentaId_Activo_Nombre] ON [Clientes] ([CuentaId], [Activo], [Nombre]);');
EXEC(N'CREATE UNIQUE INDEX [IX_Productos_CodigoInternoPorCuenta] ON [Productos] ([CuentaId], [CodigoInterno]);');
EXEC(N'CREATE INDEX [IX_Productos_CuentaId_Activo_Descripcion] ON [Productos] ([CuentaId], [Activo], [Descripcion]);');
EXEC(N'ALTER TABLE [Clientes] ADD CONSTRAINT [FK_Clientes_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
EXEC(N'ALTER TABLE [Productos] ADD CONSTRAINT [FK_Productos_Cuentas_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [Cuentas] ([Id]) ON DELETE NO ACTION;');
EXEC(N'ALTER TABLE [Productos] ADD [EmpresaAltaId] uniqueidentifier NULL;');
EXEC(N'ALTER TABLE [Clientes] ADD [EmpresaAltaId] uniqueidentifier NULL;');
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
EXEC(N'CREATE INDEX [IX_Productos_EmpresaAltaId] ON [Productos] ([EmpresaAltaId]);');
EXEC(N'CREATE INDEX [IX_Clientes_EmpresaAltaId] ON [Clientes] ([EmpresaAltaId]);');
EXEC(N'ALTER TABLE [Clientes] ADD CONSTRAINT [FK_Clientes_Empresas_EmpresaAltaId] FOREIGN KEY ([EmpresaAltaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION;');
EXEC(N'ALTER TABLE [Productos] ADD CONSTRAINT [FK_Productos_Empresas_EmpresaAltaId] FOREIGN KEY ([EmpresaAltaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION;');
EXEC(N'ALTER TABLE [Productos] ADD [Detalle] nvarchar(1000) NULL;');
EXEC(N'ALTER TABLE [Productos] ADD [IdInterno] nvarchar(100) NULL;');
EXEC(N'CREATE INDEX [IX_Productos_CuentaId_IdInterno] ON [Productos] ([CuentaId], [IdInterno]);');
EXEC(N'CREATE TABLE [MonedasDeEmpresa] (
        [EmpresaId] uniqueidentifier NOT NULL,
        [Moneda] varchar(3) NOT NULL,
        [Activa] bit NOT NULL,
        [ModificadaUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_MonedasDeEmpresa] PRIMARY KEY ([EmpresaId], [Moneda]),
        CONSTRAINT [FK_MonedasDeEmpresa_Empresas_EmpresaId] FOREIGN KEY ([EmpresaId]) REFERENCES [Empresas] ([Id]) ON DELETE NO ACTION
    );');
EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] ON;
    EXEC(N''INSERT INTO [Permisos] ([Clave], [Descripcion])
    VALUES (N''''configuracion_monedas'''', N''''Configuración: Monedas'''')'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Clave'', N''Descripcion'') AND [object_id] = OBJECT_ID(N''[Permisos]''))
        SET IDENTITY_INSERT [Permisos] OFF;');
EXEC(N'INSERT INTO MonedasDeEmpresa (EmpresaId, Moneda, Activa, ModificadaUtc)
    SELECT u.EmpresaId, u.Moneda, 1, SYSUTCDATETIME()
    FROM (SELECT EmpresaId, Moneda FROM Comprobantes
          UNION
          SELECT EmpresaId, MonedaP FROM Pagos) AS u
    WHERE u.Moneda NOT IN (''MXN'', ''XXX'');');
EXEC(N'INSERT INTO UsuariosEmpresasPermisos (UsuarioId, EmpresaId, PermisoClave, OtorgadoUtc, OtorgadoPorUsuarioId)
    SELECT p.UsuarioId, p.EmpresaId, ''configuracion_monedas'', p.OtorgadoUtc, p.OtorgadoPorUsuarioId
    FROM UsuariosEmpresasPermisos p
    WHERE p.PermisoClave = ''configuracion_general''
      AND NOT EXISTS (SELECT 1 FROM UsuariosEmpresasPermisos x
                      WHERE x.UsuarioId = p.UsuarioId AND x.EmpresaId = p.EmpresaId
                        AND x.PermisoClave = ''configuracion_monedas'');');
        DECLARE @Objeto sysname,@Principal sysname,@Permiso nvarchar(128),@Estado char(1),@SqlPermiso nvarchar(max);
        DECLARE PermisosProcedimientos CURSOR LOCAL FAST_FORWARD FOR SELECT Objeto,Principal,Permiso,Estado FROM @Permisos;
        OPEN PermisosProcedimientos;
        FETCH NEXT FROM PermisosProcedimientos INTO @Objeto,@Principal,@Permiso,@Estado;
        WHILE @@FETCH_STATUS=0
        BEGIN
            SET @SqlPermiso=CASE WHEN @Estado='D' THEN N'DENY ' ELSE N'GRANT ' END
                + @Permiso + N' ON OBJECT::[dbo].' + QUOTENAME(@Objeto) + N' TO ' + QUOTENAME(@Principal)
                + CASE WHEN @Estado='W' THEN N' WITH GRANT OPTION' ELSE N'' END + N';';
            EXEC(@SqlPermiso);
            FETCH NEXT FROM PermisosProcedimientos INTO @Objeto,@Principal,@Permiso,@Estado;
        END;
        CLOSE PermisosProcedimientos;
        DEALLOCATE PermisosProcedimientos;
    END;
    DECLARE @Correo TABLE (Columna sysname, Tipo sysname, Longitud smallint, Escala tinyint, Nula bit);
    INSERT INTO @Correo VALUES
        (N'CodigoCorreoRespuestaExpiraUtc',N'datetime2',8,7,1),
        (N'CodigoCorreoRespuestaSolicitadoUtc',N'datetime2',8,7,1),
        (N'CorreoRespuestaPendiente',N'nvarchar',508,0,1),
        (N'CorreoRespuestaVerificado',N'nvarchar',508,0,1),
        (N'HashCodigoCorreoRespuesta',N'nvarchar',512,0,1),
        (N'IntentosCodigoCorreoRespuesta',N'int',4,0,0),
        (N'SolicitudesCorreoRespuestaEnVentana',N'int',4,0,0),
        (N'VentanaSolicitudesCorreoRespuestaUtc',N'datetime2',8,7,1),
        (N'VersionFila',N'timestamp',8,0,0);
    IF EXISTS (SELECT 1 FROM @Correo e
        JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.ConfiguracionesEmpresa') AND c.name=e.Columna
        JOIN sys.types t ON t.user_type_id=c.user_type_id
        WHERE t.name<>e.Tipo OR c.max_length<>e.Longitud OR c.scale<>e.Escala OR c.is_nullable<>e.Nula)
        THROW 51118, N'Una columna de configuración de correo tiene un tipo distinto a la principal.', 1;
    IF COL_LENGTH(N'dbo.ConfiguracionesEmpresa',N'CodigoCorreoRespuestaExpiraUtc') IS NULL
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD [CodigoCorreoRespuestaExpiraUtc] datetime2(7) NULL;');
    IF COL_LENGTH(N'dbo.ConfiguracionesEmpresa',N'CodigoCorreoRespuestaSolicitadoUtc') IS NULL
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD [CodigoCorreoRespuestaSolicitadoUtc] datetime2(7) NULL;');
    IF COL_LENGTH(N'dbo.ConfiguracionesEmpresa',N'CorreoRespuestaPendiente') IS NULL
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD [CorreoRespuestaPendiente] nvarchar(254) NULL;');
    IF COL_LENGTH(N'dbo.ConfiguracionesEmpresa',N'CorreoRespuestaVerificado') IS NULL
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD [CorreoRespuestaVerificado] nvarchar(254) NULL;');
    IF COL_LENGTH(N'dbo.ConfiguracionesEmpresa',N'HashCodigoCorreoRespuesta') IS NULL
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD [HashCodigoCorreoRespuesta] nvarchar(256) NULL;');
    IF COL_LENGTH(N'dbo.ConfiguracionesEmpresa',N'IntentosCodigoCorreoRespuesta') IS NULL
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD [IntentosCodigoCorreoRespuesta] int NOT NULL CONSTRAINT [DF_ConfiguracionesEmpresa_IntentosCodigoCorreoRespuesta] DEFAULT ((0)) WITH VALUES;');
    IF COL_LENGTH(N'dbo.ConfiguracionesEmpresa',N'SolicitudesCorreoRespuestaEnVentana') IS NULL
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD [SolicitudesCorreoRespuestaEnVentana] int NOT NULL CONSTRAINT [DF_ConfiguracionesEmpresa_SolicitudesCorreoRespuestaEnVentana] DEFAULT ((0)) WITH VALUES;');
    IF COL_LENGTH(N'dbo.ConfiguracionesEmpresa',N'VentanaSolicitudesCorreoRespuestaUtc') IS NULL
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD [VentanaSolicitudesCorreoRespuestaUtc] datetime2(7) NULL;');
    IF COL_LENGTH(N'dbo.ConfiguracionesEmpresa',N'VersionFila') IS NULL
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD [VersionFila] rowversion NOT NULL;');
    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c
        ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id
        WHERE c.object_id=OBJECT_ID(N'dbo.ConfiguracionesEmpresa') AND c.name=N'IntentosCodigoCorreoRespuesta')
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD CONSTRAINT [DF_ConfiguracionesEmpresa_IntentosCodigoCorreoRespuesta] DEFAULT ((0)) FOR [IntentosCodigoCorreoRespuesta];');
    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c
        ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id
        WHERE c.object_id=OBJECT_ID(N'dbo.ConfiguracionesEmpresa') AND c.name=N'SolicitudesCorreoRespuestaEnVentana')
        EXEC(N'ALTER TABLE dbo.ConfiguracionesEmpresa ADD CONSTRAINT [DF_ConfiguracionesEmpresa_SolicitudesCorreoRespuestaEnVentana] DEFAULT ((0)) FOR [SolicitudesCorreoRespuestaEnVentana];');
    IF EXISTS (SELECT 1 FROM @Correo e
        JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.ConfiguracionesEmpresa') AND c.name=e.Columna
        JOIN sys.types t ON t.user_type_id=c.user_type_id
        WHERE t.name<>e.Tipo OR c.max_length<>e.Longitud OR c.scale<>e.Escala OR c.is_nullable<>e.Nula)
        THROW 51118, N'Una columna de configuración de correo tiene un tipo distinto a la principal.', 1;
    IF EXISTS (SELECT 1 FROM @Correo e WHERE NOT EXISTS
        (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.ConfiguracionesEmpresa') AND c.name=e.Columna))
        THROW 51119, N'No se completaron las columnas de configuración de correo.', 1;
    IF EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.columns c
        ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id
        WHERE c.object_id=OBJECT_ID(N'dbo.ConfiguracionesEmpresa')
          AND c.name IN (N'IntentosCodigoCorreoRespuesta',N'SolicitudesCorreoRespuestaEnVentana')
          AND REPLACE(REPLACE(REPLACE(d.definition,N'(',N''),N')',N''),N' ',N'')<>N'0')
        THROW 51120, N'Un valor predeterminado de configuración de correo es distinto a cero.', 1;
    DELETE FROM @Problemas;
    INSERT INTO @Problemas
    SELECT e.Version, N'Tabla', e.Tabla FROM @Tablas e
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U') IS NULL;
    INSERT INTO @Problemas
    SELECT e.Version, N'Columna o tipo', e.Tabla + N'.' + e.Columna
    FROM @Columnas e
    LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U') AND c.name=e.Columna
    LEFT JOIN sys.types t ON t.user_type_id=c.user_type_id
    WHERE c.column_id IS NULL OR t.name<>e.Tipo OR c.is_nullable<>e.Nula
       OR (e.Longitud IS NOT NULL AND c.max_length<>e.Longitud)
       OR (e.Precision IS NOT NULL AND c.precision<>e.Precision)
       OR (e.Escala IS NOT NULL AND c.scale<>e.Escala);
    INSERT INTO @Problemas
    SELECT e.Version, N'Columna adicional', e.Tabla + N'.' + c.name
    FROM @Tablas e JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U')
    WHERE NOT EXISTS (SELECT 1 FROM @Columnas x WHERE x.Version=e.Version AND x.Tabla=e.Tabla AND x.Columna=c.name);
    INSERT INTO @Problemas
    SELECT v.Version, N'Tabla de otra versión', x.Tabla
    FROM (VALUES (0),(1)) v(Version)
    CROSS JOIN (SELECT DISTINCT Tabla FROM @Tablas) x
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(x.Tabla), N'U') IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM @Tablas e WHERE e.Version=v.Version AND e.Tabla=x.Tabla);
    INSERT INTO @Problemas
    SELECT e.Version, N'Índice o primaria', e.Tabla + N'.' + e.Nombre
    FROM @Indices e
    LEFT JOIN sys.indexes i ON i.object_id=OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U') AND i.name=e.Nombre
    OUTER APPLY (SELECT STUFF((SELECT N'|' + c.name FROM sys.index_columns ic
        JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0
        ORDER BY ic.key_ordinal FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'),1,1,N'') AS Columnas) k
    WHERE i.index_id IS NULL OR i.is_unique<>e.Unico OR i.has_filter<>e.Filtrado
       OR i.is_primary_key<>e.Primaria OR i.is_disabled=1 OR ISNULL(k.Columnas,N'')<>e.Columnas;
    INSERT INTO @Problemas
    SELECT e.Version, N'Llave foránea', e.Tabla + N'.' + e.Nombre
    FROM @Claves e
    LEFT JOIN sys.foreign_keys f ON f.parent_object_id=OBJECT_ID(N'dbo.' + QUOTENAME(e.Tabla), N'U') AND f.name=e.Nombre
    OUTER APPLY (SELECT STUFF((SELECT N'|' + c.name FROM sys.foreign_key_columns fc
        JOIN sys.columns c ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id
        WHERE fc.constraint_object_id=f.object_id ORDER BY fc.constraint_column_id
        FOR XML PATH(''),TYPE).value('.', 'nvarchar(max)'),1,1,N'') AS Columnas) k
    OUTER APPLY (SELECT STUFF((SELECT N'|' + c.name FROM sys.foreign_key_columns fc
        JOIN sys.columns c ON c.object_id=fc.referenced_object_id AND c.column_id=fc.referenced_column_id
        WHERE fc.constraint_object_id=f.object_id ORDER BY fc.constraint_column_id
        FOR XML PATH(''),TYPE).value('.', 'nvarchar(max)'),1,1,N'') AS Columnas) r
    WHERE f.object_id IS NULL OR OBJECT_NAME(f.referenced_object_id)<>e.Principal
       OR f.delete_referential_action<>e.Borrado OR f.is_disabled=1 OR f.is_not_trusted=1
       OR ISNULL(k.Columnas,N'')<>e.Columnas OR ISNULL(r.Columnas,N'')<>e.ColumnasPrincipal;
    IF EXISTS (SELECT 1 FROM @Problemas WHERE Version=1)
    BEGIN
        SELECT TOP (30) Tipo,Objeto FROM @Problemas WHERE Version=1 ORDER BY Tipo,Objeto;
        THROW 51107, N'La comprobación final del esquema no coincide. Se revierte la actualización.', 1;
    END;
    IF EXISTS (SELECT 1 FROM @PermisosEsperados e WHERE NOT EXISTS (SELECT 1 FROM dbo.Permisos p WHERE p.Clave=e.Clave))
        OR EXISTS (SELECT 1 FROM dbo.Permisos p WHERE NOT EXISTS (SELECT 1 FROM @PermisosEsperados e WHERE e.Clave=p.Clave))
        THROW 51108, N'La lista final de permisos no coincide con los 66 esperados.', 1;
    IF @Repetida=0
        EXEC sys.sp_addextendedproperty @name=N'Facturacion.EstructuraCuenta.v1',@value=N'20261031-Cuentas-Monedas';
    COMMIT TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name=N'CatalogoTextoCompleto')
        EXEC(N'CREATE FULLTEXT CATALOG CatalogoTextoCompleto WITH ACCENT_SENSITIVITY=ON AS DEFAULT;');
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatClaveProdServ'))
        EXEC(N'CREATE FULLTEXT INDEX ON dbo.[SatClaveProdServ] ([Descripcion] LANGUAGE 3082,[PalabrasSimilares] LANGUAGE 3082) KEY INDEX [PK_SatClaveProdServ] ON CatalogoTextoCompleto WITH STOPLIST=SYSTEM, CHANGE_TRACKING AUTO;');
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatClaveProdServ') AND is_enabled=0)
        EXEC(N'ALTER FULLTEXT INDEX ON dbo.[SatClaveProdServ] ENABLE;');
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatClaveProdServ') AND change_tracking_state<>N'A')
        EXEC(N'ALTER FULLTEXT INDEX ON dbo.[SatClaveProdServ] SET CHANGE_TRACKING AUTO;');
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatClaveProdServ') AND (stoplist_id IS NULL OR stoplist_id<>0))
        EXEC(N'ALTER FULLTEXT INDEX ON dbo.[SatClaveProdServ] SET STOPLIST=SYSTEM;');
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatColonia'))
        EXEC(N'CREATE FULLTEXT INDEX ON dbo.[SatColonia] ([Nombre] LANGUAGE 3082) KEY INDEX [IX_SatColonia_IdInterno] ON CatalogoTextoCompleto WITH STOPLIST=SYSTEM, CHANGE_TRACKING AUTO;');
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatColonia') AND is_enabled=0)
        EXEC(N'ALTER FULLTEXT INDEX ON dbo.[SatColonia] ENABLE;');
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatColonia') AND change_tracking_state<>N'A')
        EXEC(N'ALTER FULLTEXT INDEX ON dbo.[SatColonia] SET CHANGE_TRACKING AUTO;');
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatColonia') AND (stoplist_id IS NULL OR stoplist_id<>0))
        EXEC(N'ALTER FULLTEXT INDEX ON dbo.[SatColonia] SET STOPLIST=SYSTEM;');
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatMunicipio'))
        EXEC(N'CREATE FULLTEXT INDEX ON dbo.[SatMunicipio] ([Descripcion] LANGUAGE 3082) KEY INDEX [IX_SatMunicipio_IdInterno] ON CatalogoTextoCompleto WITH STOPLIST=SYSTEM, CHANGE_TRACKING AUTO;');
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatMunicipio') AND is_enabled=0)
        EXEC(N'ALTER FULLTEXT INDEX ON dbo.[SatMunicipio] ENABLE;');
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatMunicipio') AND change_tracking_state<>N'A')
        EXEC(N'ALTER FULLTEXT INDEX ON dbo.[SatMunicipio] SET CHANGE_TRACKING AUTO;');
    IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SatMunicipio') AND (stoplist_id IS NULL OR stoplist_id<>0))
        EXEC(N'ALTER FULLTEXT INDEX ON dbo.[SatMunicipio] SET STOPLIST=SYSTEM;');
    IF EXISTS (SELECT 1 FROM @Texto t
        JOIN sys.fulltext_indexes f ON f.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Tabla))
        JOIN sys.fulltext_catalogs g ON g.fulltext_catalog_id=f.fulltext_catalog_id
        JOIN sys.indexes i ON i.object_id=f.object_id AND i.index_id=f.unique_index_id
        WHERE g.name<>N'CatalogoTextoCompleto' OR i.name<>t.Indice
           OR EXISTS (SELECT 1 FROM @ColumnasTexto e WHERE e.Tabla=t.Tabla AND NOT EXISTS
                (SELECT 1 FROM sys.fulltext_index_columns x JOIN sys.columns c ON c.object_id=x.object_id AND c.column_id=x.column_id
                 WHERE x.object_id=f.object_id AND c.name=e.Columna AND x.language_id=3082 AND ISNULL(x.type_column_id,0)=0))
           OR EXISTS (SELECT 1 FROM sys.fulltext_index_columns x JOIN sys.columns c ON c.object_id=x.object_id AND c.column_id=x.column_id
                WHERE x.object_id=f.object_id AND NOT EXISTS
                    (SELECT 1 FROM @ColumnasTexto e WHERE e.Tabla=t.Tabla AND e.Columna=c.name)))
        THROW 51117, N'Un índice de texto completo tiene una definición distinta a la principal.', 1;
    IF EXISTS (SELECT 1 FROM @Texto t WHERE NOT EXISTS
        (SELECT 1 FROM sys.fulltext_indexes f WHERE f.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Tabla))
         AND f.is_enabled=1 AND f.change_tracking_state=N'A' AND f.stoplist_id=0))
        THROW 51121, N'No se completaron los índices de búsqueda SAT. Ejecuta de nuevo el archivo.', 1;
    EXEC sys.sp_releaseapplock @Resource=N'Facturacion.ActualizarEsquema.v1',@LockOwner='Session';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    IF @Bloqueo>=0
        EXEC sys.sp_releaseapplock @Resource=N'Facturacion.ActualizarEsquema.v1',@LockOwner='Session';
    THROW;
END CATCH;
