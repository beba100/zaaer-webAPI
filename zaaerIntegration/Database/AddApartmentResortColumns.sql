-- Run on EVERY tenant database (hotel, resort, hall).
-- Columns are nullable; hotels keep NULL and behavior stays the same.
-- Idempotent: safe to run multiple times.

IF COL_LENGTH('dbo.apartments', 'resort_area_type') IS NULL
BEGIN
    ALTER TABLE dbo.apartments ADD resort_area_type NVARCHAR(50) NULL;
    PRINT 'Column apartments.resort_area_type added.';
END
ELSE
    PRINT 'Column apartments.resort_area_type already exists.';
GO

IF COL_LENGTH('dbo.apartments', 'parent_apartment_id') IS NULL
BEGIN
    ALTER TABLE dbo.apartments ADD parent_apartment_id INT NULL;
    PRINT 'Column apartments.parent_apartment_id added.';
END
ELSE
    PRINT 'Column apartments.parent_apartment_id already exists.';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_apartments_parent_apartment'
      AND object_id = OBJECT_ID('dbo.apartments')
)
BEGIN
    CREATE INDEX IX_apartments_parent_apartment
        ON dbo.apartments(hotel_id, parent_apartment_id);
    PRINT 'Index IX_apartments_parent_apartment created.';
END
GO

-- Room board reads maintenance_categories on all property types.
IF OBJECT_ID('dbo.maintenances', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.maintenances', 'maintenance_categories') IS NULL
BEGIN
    ALTER TABLE dbo.maintenances ADD maintenance_categories NVARCHAR(200) NULL;
    PRINT 'Column maintenances.maintenance_categories added.';
END
GO
