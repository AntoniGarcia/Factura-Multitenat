SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.PagosDocumentos', N'U') IS NULL OR OBJECT_ID(N'dbo.SatMoneda', N'U') IS NULL
    THROW 50000, N'Faltan las tablas de pagos o monedas.', 1;
IF EXISTS (SELECT 1 FROM PagosDocumentos d LEFT JOIN SatMoneda m ON m.Clave=d.MonedaDR
    WHERE m.Clave IS NULL OR m.Decimales NOT BETWEEN 0 AND 6)
    THROW 50000, N'Revisa las monedas de los documentos pagados antes de actualizar.', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.PagosDocumentos')
    AND name=N'EquivalenciaDR' AND (precision<>28 OR scale<>10))
    ALTER TABLE dbo.PagosDocumentos ALTER COLUMN EquivalenciaDR decimal(28,10) NOT NULL;
IF COL_LENGTH(N'dbo.PagosDocumentos', N'DecimalesMonedaDR') IS NULL
BEGIN
    ALTER TABLE dbo.PagosDocumentos ADD DecimalesMonedaDR int NOT NULL
        CONSTRAINT DF_PagosDocumentos_DecimalesMonedaDR DEFAULT 2;
    EXEC sys.sp_executesql N'UPDATE d SET DecimalesMonedaDR=m.Decimales FROM PagosDocumentos d JOIN SatMoneda m ON m.Clave=d.MonedaDR;';
END;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104230741_20261104_B_MaterialPeligrosoCartaPorte'')
           AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104233416_20261104_B_EquivalenciasDePago'')
            INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES (N''20261104233416_20261104_B_EquivalenciasDePago'', N''10.0.14'');';
COMMIT;
