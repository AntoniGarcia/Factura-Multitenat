SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Conceptos',N'U') IS NULL OR OBJECT_ID(N'dbo.DatosObra',N'U') IS NULL
    THROW 50000,N'Faltan las tablas de conceptos u obra.',1;
IF COL_LENGTH(N'dbo.Conceptos',N'DatosFiscalesJson') IS NULL
    ALTER TABLE dbo.Conceptos ADD DatosFiscalesJson nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.DatosObra',N'ServicioParcialJson') IS NULL
    ALTER TABLE dbo.DatosObra ADD ServicioParcialJson nvarchar(max) NULL;
IF EXISTS(SELECT 1 FROM sys.columns WHERE
    ((object_id=OBJECT_ID(N'dbo.Conceptos') AND name=N'DatosFiscalesJson') OR
     (object_id=OBJECT_ID(N'dbo.DatosObra') AND name=N'ServicioParcialJson'))
    AND (system_type_id<>231 OR max_length<>-1 OR is_nullable<>1))
    THROW 50000,N'Las columnas fiscales existentes no tienen el tipo esperado.',1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        IF EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261105041146_20261104_B_CadenaSpeiPago'')
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261105125228_20261105_B_DatosFiscalesConceptos'')
                INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES(N''20261105125228_20261105_B_DatosFiscalesConceptos'',N''10.0.14'');
            IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261105131603_20261105_B_ServiciosParcialesConstruccion'')
                INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES(N''20261105131603_20261105_B_ServiciosParcialesConstruccion'',N''10.0.14'');
        END;';
COMMIT;
