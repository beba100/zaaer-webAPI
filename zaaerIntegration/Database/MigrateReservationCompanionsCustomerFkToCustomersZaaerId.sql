-- OPTIONAL tenant migration: point reservation_companions.customer_id FK at customers(zaaer_id).
--
-- SQL Server requires the referenced column to be PRIMARY KEY or have a UNIQUE constraint.
-- Run the duplicate check first; do not create the unique index if duplicates exist.
--
-- After this migration, application code must persist the Zaaer id in reservation_companions.customer_id
-- (and configure EF with HasPrincipalKey / alternate key on Customer.ZaaerId).
--
-- Default build: companion.customer_id -> customers.customer_id; Zaaer from customers in code (no companion.customer_zaaer_id required).
-- Optional denormalized column: AddReservationCompanionsCustomerZaaerId.sql. Drop customer FK: DropReservationCompanionsCustomerForeignKey.sql.

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.reservation_companions', N'U') IS NULL
BEGIN
    RAISERROR('Table dbo.reservation_companions does not exist.', 16, 1);
    RETURN;
END

-- 1) Fail fast if duplicate non-null zaaer_id values would block a unique index
IF EXISTS (
    SELECT zaaer_id
    FROM dbo.customers
    WHERE zaaer_id IS NOT NULL AND zaaer_id > 0
    GROUP BY zaaer_id
    HAVING COUNT(*) > 1
)
BEGIN
    RAISERROR('customers.zaaer_id has duplicates — create a unique index / fix data before this migration.', 16, 1);
    RETURN;
END

-- 2) Unique index on zaaer_id (non-null) — required for FK reference
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.customers')
      AND name = N'UQ_customers_zaaer_id_notnull'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_customers_zaaer_id_notnull]
        ON [dbo].[customers] ([zaaer_id] ASC)
        WHERE [zaaer_id] IS NOT NULL AND [zaaer_id] > 0;
END
GO

-- 3) Drop old FK on customer_id -> customers(customer_id)
DECLARE @fkName SYSNAME =
(
    SELECT fk.name
    FROM sys.foreign_keys AS fk
    INNER JOIN sys.foreign_key_columns AS fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.columns AS cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
    INNER JOIN sys.tables AS tp ON fk.parent_object_id = tp.object_id
    WHERE tp.schema_id = SCHEMA_ID(N'dbo')
      AND tp.name = N'reservation_companions'
      AND cp.name = N'customer_id'
      AND fk.name LIKE N'FK%companions%customer%'
);

IF @fkName IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(400) = N'ALTER TABLE dbo.reservation_companions DROP CONSTRAINT ' + QUOTENAME(@fkName) + N';';
    EXEC sp_executesql @sql;
END
GO

-- 4) Repoint data: store Zaaer id in customer_id (only rows that have a single matching customer)
UPDATE rc
SET rc.customer_id = c.zaaer_id
FROM dbo.reservation_companions AS rc
INNER JOIN dbo.customers AS c ON c.customer_id = rc.customer_id
WHERE c.zaaer_id IS NOT NULL
  AND c.zaaer_id > 0;

-- Rows where customer has no zaaer_id cannot satisfy FK to customers(zaaer_id) — fix data manually.
IF EXISTS (
    SELECT 1
    FROM dbo.reservation_companions AS rc
    LEFT JOIN dbo.customers AS c ON c.zaaer_id = rc.customer_id AND c.zaaer_id IS NOT NULL AND c.zaaer_id > 0
    WHERE c.customer_id IS NULL
)
BEGIN
    RAISERROR('Some reservation_companions rows have no matching customers.zaaer_id after remap — fix before adding FK.', 16, 1);
    RETURN;
END
GO

-- 5) New FK: customer_id -> customers(zaaer_id)
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.reservation_companions')
      AND referenced_object_id = OBJECT_ID(N'dbo.customers')
      AND name = N'FK_reservation_companions_customers_zaaer'
)
BEGIN
    ALTER TABLE [dbo].[reservation_companions] WITH CHECK
        ADD CONSTRAINT [FK_reservation_companions_customers_zaaer]
            FOREIGN KEY ([customer_id]) REFERENCES [dbo].[customers] ([zaaer_id]);
END
GO
