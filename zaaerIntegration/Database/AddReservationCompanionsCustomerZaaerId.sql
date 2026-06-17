-- OPTIONAL tenant migration: denormalized customer_zaaer_id on reservation_companions.
-- The application does not require this column; it resolves Zaaer from dbo.customers using customer_id.
-- Run only if you want the extra column for reporting or legacy tooling.

IF COL_LENGTH(N'dbo.reservation_companions', N'customer_zaaer_id') IS NULL
BEGIN
    ALTER TABLE [dbo].[reservation_companions]
        ADD [customer_zaaer_id] INT NULL;
END
GO

-- Backfill from customers where the FK column already points at the internal row.
UPDATE rc
SET rc.[customer_zaaer_id] = c.[zaaer_id]
FROM [dbo].[reservation_companions] AS rc
INNER JOIN [dbo].[customers] AS c ON c.[customer_id] = rc.[customer_id]
WHERE rc.[customer_zaaer_id] IS NULL
  AND c.[zaaer_id] IS NOT NULL
  AND c.[zaaer_id] > 0;
GO
