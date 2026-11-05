SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Pagos', N'U') IS NULL
    THROW 50000, N'Falta la tabla de pagos.', 1;
IF COL_LENGTH(N'dbo.Pagos', N'TipoCadPago') IS NULL
    ALTER TABLE dbo.Pagos ADD TipoCadPago nvarchar(2) NULL;
IF COL_LENGTH(N'dbo.Pagos', N'CertPago') IS NULL
    ALTER TABLE dbo.Pagos ADD CertPago nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.Pagos', N'CadPago') IS NULL
    ALTER TABLE dbo.Pagos ADD CadPago nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.Pagos', N'SelloPago') IS NULL
    ALTER TABLE dbo.Pagos ADD SelloPago nvarchar(max) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Pagos')
    AND name IN (N'TipoCadPago', N'CertPago', N'CadPago', N'SelloPago')
    AND (system_type_id<>231 OR is_nullable<>1 OR
        (name=N'TipoCadPago' AND max_length<>4) OR
        (name<>N'TipoCadPago' AND max_length<>-1)))
    THROW 50000, N'Las columnas SPEI existentes no tienen el tipo esperado.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261104233416_20261104_B_EquivalenciasDePago'')
           AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N''20261105041146_20261104_B_CadenaSpeiPago'')
            INSERT dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
            VALUES (N''20261105041146_20261104_B_CadenaSpeiPago'', N''10.0.14'');';
COMMIT;
