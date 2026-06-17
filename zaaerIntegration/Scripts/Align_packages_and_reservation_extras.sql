-- Align tenant DB with PMS app: packages rename, extras package FK, extras reservation key + unit FK.
-- Run on each tenant database (e.g. db32462_jizan3).

-- 1) Rename catalog table reservation_packages -> packages (if not already renamed)
IF OBJECT_ID(N'[dbo].[reservation_packages]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[packages]', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.reservation_packages', N'packages';
END
GO

-- 2) Point FK from reservation_extras.package_id to packages (if still referencing old name)
IF OBJECT_ID(N'[dbo].[reservation_extras]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[packages]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_reservation_extras_packages'
          AND parent_object_id = OBJECT_ID(N'[dbo].[reservation_extras]')
    )
    BEGIN
        ALTER TABLE [dbo].[reservation_extras] DROP CONSTRAINT [FK_reservation_extras_packages];
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_reservation_extras_packages'
          AND parent_object_id = OBJECT_ID(N'[dbo].[reservation_extras]')
    )
    BEGIN
        ALTER TABLE [dbo].[reservation_extras]
            ADD CONSTRAINT [FK_reservation_extras_packages]
            FOREIGN KEY ([package_id]) REFERENCES [dbo].[packages] ([package_id]);
    END
END
GO

-- 3) reservation_extras: drop FK to reservations; backfill reservation_id to zaaer_id when available
IF OBJECT_ID(N'[dbo].[reservation_extras]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_reservation_extras_reservations'
          AND parent_object_id = OBJECT_ID(N'[dbo].[reservation_extras]')
    )
    BEGIN
        ALTER TABLE [dbo].[reservation_extras] DROP CONSTRAINT [FK_reservation_extras_reservations];
    END

    UPDATE e
    SET e.[reservation_id] = r.[zaaer_id]
    FROM [dbo].[reservation_extras] AS e
    INNER JOIN [dbo].[reservations] AS r ON r.[reservation_id] = e.[reservation_id]
    WHERE r.[zaaer_id] IS NOT NULL
      AND r.[zaaer_id] > 0
      AND e.[reservation_id] = r.[reservation_id];
END
GO

-- 4) unit_id must reference reservation_units.unit_id. Clear invalid ids, then ensure FK exists.
IF OBJECT_ID(N'[dbo].[reservation_extras]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[reservation_units]', N'U') IS NOT NULL
BEGIN
    UPDATE e
    SET e.[unit_id] = NULL
    FROM [dbo].[reservation_extras] AS e
    WHERE e.[unit_id] IS NOT NULL
      AND NOT EXISTS (
          SELECT 1 FROM [dbo].[reservation_units] AS ru WHERE ru.[unit_id] = e.[unit_id]
      );

    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = N'FK_reservation_extras_units'
          AND parent_object_id = OBJECT_ID(N'[dbo].[reservation_extras]')
    )
    BEGIN
        ALTER TABLE [dbo].[reservation_extras] DROP CONSTRAINT [FK_reservation_extras_units];
    END

    ALTER TABLE [dbo].[reservation_extras]
        ADD CONSTRAINT [FK_reservation_extras_units]
        FOREIGN KEY ([unit_id]) REFERENCES [dbo].[reservation_units] ([unit_id]) ON DELETE SET NULL;
END
GO
