-- Idempotent: maintenance work-type tags (ac, paint, pest_control, other) as comma-separated keys.
IF COL_LENGTH('dbo.maintenances', 'maintenance_categories') IS NULL
BEGIN
    ALTER TABLE dbo.maintenances
        ADD maintenance_categories NVARCHAR(200) NULL;
    PRINT 'Column maintenances.maintenance_categories added.';
END
ELSE
BEGIN
    PRINT 'Column maintenances.maintenance_categories already exists.';
END
GO
