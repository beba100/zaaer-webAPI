IF COL_LENGTH(N'dbo.apartments', N'facilities_json') IS NULL
BEGIN
    ALTER TABLE dbo.apartments ADD facilities_json NVARCHAR(MAX) NULL;
    PRINT N'Added apartments.facilities_json';
END
ELSE
    PRINT N'apartments.facilities_json already exists';
