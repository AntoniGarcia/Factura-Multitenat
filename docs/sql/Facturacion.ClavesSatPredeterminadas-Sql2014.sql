SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.Paquetes', N'ClaveProdServ') IS NULL OR
       COL_LENGTH(N'dbo.Paquetes', N'ClaveUnidad') IS NULL OR OBJECT_ID(N'dbo.Bitacora', N'U') IS NULL
        THROW 51000, N'Falta aplicar la actualización de claves SAT de paquetes.', 1;
    DECLARE @Defecto sysname, @Sql nvarchar(500);
    SELECT @Defecto = d.name FROM sys.default_constraints d JOIN sys.columns c
        ON c.default_object_id = d.object_id WHERE c.object_id = OBJECT_ID(N'dbo.Paquetes') AND c.name = N'ClaveProdServ';
    IF @Defecto IS NOT NULL
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.Paquetes DROP CONSTRAINT ' + QUOTENAME(@Defecto);
        EXEC(@Sql);
    END;
    SET @Defecto = NULL;
    SELECT @Defecto = d.name FROM sys.default_constraints d JOIN sys.columns c
        ON c.default_object_id = d.object_id WHERE c.object_id = OBJECT_ID(N'dbo.Paquetes') AND c.name = N'ClaveUnidad';
    IF @Defecto IS NOT NULL
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.Paquetes DROP CONSTRAINT ' + QUOTENAME(@Defecto);
        EXEC(@Sql);
    END;
    ALTER TABLE dbo.Paquetes ADD CONSTRAINT DF_Paquetes_ClaveProdServ DEFAULT N'81112106' FOR ClaveProdServ;
    ALTER TABLE dbo.Paquetes ADD CONSTRAINT DF_Paquetes_ClaveUnidad DEFAULT N'E48' FOR ClaveUnidad;
DECLARE @Cambios TABLE (
    Id uniqueidentifier, CuentaId uniqueidentifier,
    ProductoAnterior nvarchar(8), UnidadAnterior nvarchar(3),
    ProductoNuevo nvarchar(8), UnidadNueva nvarchar(3));
UPDATE dbo.Paquetes
SET ClaveProdServ = CASE WHEN NULLIF(LTRIM(RTRIM(ClaveProdServ)), N'') IS NULL THEN N'81112106' ELSE ClaveProdServ END,
    ClaveUnidad = CASE WHEN NULLIF(LTRIM(RTRIM(ClaveUnidad)), N'') IS NULL THEN N'E48' ELSE ClaveUnidad END
OUTPUT inserted.Id, inserted.CuentaId, deleted.ClaveProdServ, deleted.ClaveUnidad,
    inserted.ClaveProdServ, inserted.ClaveUnidad INTO @Cambios
WHERE NULLIF(LTRIM(RTRIM(ClaveProdServ)), N'') IS NULL OR NULLIF(LTRIM(RTRIM(ClaveUnidad)), N'') IS NULL;
INSERT dbo.Bitacora (CuentaId, Entidad, EntidadId, Accion, ValorAnterior, ValorNuevo, MomentoUtc)
SELECT CuentaId, N'Paquete', LOWER(CONVERT(nvarchar(36), Id)), N'paquete_claves_predeterminadas',
    N'{"ClaveProdServ":' + CASE WHEN ProductoAnterior IS NULL THEN N'null' ELSE N'"' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ProductoAnterior, N'\', N'\\'), N'"', N'\"'), NCHAR(13), N'\r'), NCHAR(10), N'\n'), NCHAR(9), N'\t') + N'"' END + N',"ClaveUnidad":' + CASE WHEN UnidadAnterior IS NULL THEN N'null' ELSE N'"' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UnidadAnterior, N'\', N'\\'), N'"', N'\"'), NCHAR(13), N'\r'), NCHAR(10), N'\n'), NCHAR(9), N'\t') + N'"' END + N'}' ,
    N'{"ClaveProdServ":' + CASE WHEN ProductoNuevo IS NULL THEN N'null' ELSE N'"' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ProductoNuevo, N'\', N'\\'), N'"', N'\"'), NCHAR(13), N'\r'), NCHAR(10), N'\n'), NCHAR(9), N'\t') + N'"' END + N',"ClaveUnidad":' + CASE WHEN UnidadNueva IS NULL THEN N'null' ELSE N'"' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UnidadNueva, N'\', N'\\'), N'"', N'\"'), NCHAR(13), N'\r'), NCHAR(10), N'\n'), NCHAR(9), N'\t') + N'"' END + N'}' , SYSUTCDATETIME()
FROM @Cambios;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
