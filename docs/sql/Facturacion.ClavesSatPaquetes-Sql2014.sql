SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Paquetes', N'U') IS NULL OR OBJECT_ID(N'dbo.ComprasTimbres', N'U') IS NULL
        THROW 51000, N'No se encontraron las tablas de paquetes y compras.', 1;
    IF COL_LENGTH(N'dbo.Paquetes', N'ClaveProdServ') IS NOT NULL OR
       COL_LENGTH(N'dbo.Paquetes', N'ClaveUnidad') IS NOT NULL OR
       COL_LENGTH(N'dbo.ComprasTimbres', N'ClaveProdServAlComprar') IS NOT NULL OR
       COL_LENGTH(N'dbo.ComprasTimbres', N'ClaveUnidadAlComprar') IS NOT NULL
        THROW 51000, N'La actualización ya existe o está incompleta. No se aplicaron cambios.', 1;
    ALTER TABLE dbo.Paquetes ADD ClaveProdServ nvarchar(8) NULL, ClaveUnidad nvarchar(3) NULL;
    ALTER TABLE dbo.ComprasTimbres ADD ClaveProdServAlComprar nvarchar(8) NULL, ClaveUnidadAlComprar nvarchar(3) NULL;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
