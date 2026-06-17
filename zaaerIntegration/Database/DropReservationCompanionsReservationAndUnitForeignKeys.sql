-- Tenant DB: drop FKs that bind reservation_companions.reservation_id / unit_id to internal PKs.
-- Required when reservation_id holds reservations.zaaer_id and unit_id holds apartments.zaaer_id (or apt id).

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.reservation_companions', N'U') IS NULL
BEGIN
    RAISERROR(N'dbo.reservation_companions not found.', 16, 1);
    RETURN;
END

DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql + N'ALTER TABLE dbo.reservation_companions DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(10)
FROM sys.foreign_keys AS fk
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.reservation_companions')
  AND fk.referenced_object_id IN (OBJECT_ID(N'dbo.reservations'), OBJECT_ID(N'dbo.reservation_units'));

IF @sql = N''
BEGIN
    PRINT N'No FK from reservation_companions to reservations / reservation_units was found.';
    RETURN;
END

EXEC sp_executesql @sql;
PRINT N'Dropped reservation_companions -> reservations / reservation_units foreign key(s).';
GO
