-- Tenant DB: remove FK from reservation_companions.customer_id -> customers
-- Use when you hit: FK_reservation_companions_customers / 547 conflicts (e.g. zaaer vs internal id).
-- Run once per hotel database. Application still validates customers in code; DB no longer enforces the link.

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
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.customers');

IF @sql = N''
BEGIN
    PRINT N'No foreign key from reservation_companions to customers was found.';
    RETURN;
END

EXEC sp_executesql @sql;
PRINT N'Dropped companion -> customer foreign key(s).';
GO
