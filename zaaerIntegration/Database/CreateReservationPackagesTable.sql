-- Tenant DB: package / add-on catalog used by reservation extras (table name: packages).
-- Run once per tenant database before using the package dropdown create flow.

IF OBJECT_ID(N'[dbo].[packages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[packages]
    (
        [package_id]   INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_packages] PRIMARY KEY,
        [hotel_id]     INT NULL,
        [name]         NVARCHAR(400) NOT NULL,
        [name_ar]      NVARCHAR(400) NULL,
        [description]  NVARCHAR(1000) NULL,
        [unit_price]   DECIMAL(12,2) NOT NULL CONSTRAINT [DF_packages_unit_price] DEFAULT (0),
        [is_active]    BIT NOT NULL CONSTRAINT [DF_packages_is_active] DEFAULT (1),
        [sort_order]   INT NOT NULL CONSTRAINT [DF_packages_sort_order] DEFAULT (100),
        [created_at]   DATETIME2(0) NOT NULL CONSTRAINT [DF_packages_created_at] DEFAULT (SYSUTCDATETIME()),
        [updated_at]   DATETIME2(0) NULL
    );

    CREATE INDEX [IX_packages_hotel_active_sort]
        ON [dbo].[packages] ([hotel_id], [is_active], [sort_order], [name]);
END;

IF OBJECT_ID(N'[dbo].[reservation_extras]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[packages]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE [name] = N'FK_reservation_extras_packages'
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[reservation_extras]')
   )
BEGIN
    ALTER TABLE [dbo].[reservation_extras]
    ADD CONSTRAINT [FK_reservation_extras_packages]
        FOREIGN KEY ([package_id]) REFERENCES [dbo].[packages] ([package_id])
        ON DELETE SET NULL;
END;
