SET NOCOUNT ON;

/*
  Migrate legacy dbo.TargetSystem → dbo.pms_hotel_monthly_targets (Master DB).
  hotel_zaaer_id links to Tenants.ZaaerId and hotel_settings.zaaer_id.
  Run once on Master DB.
*/

IF OBJECT_ID(N'dbo.TargetSystem', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_hotel_monthly_targets', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.TargetSystem', N'pms_hotel_monthly_targets';
END;

IF OBJECT_ID(N'dbo.pms_hotel_monthly_targets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_hotel_monthly_targets (
        hotel_monthly_target_id INT IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_pms_hotel_monthly_targets PRIMARY KEY,
        hotel_zaaer_id INT NOT NULL,
        branch_name NVARCHAR(200) NULL,
        month_year DATE NOT NULL,
        target_amount DECIMAL(18, 2) NOT NULL
            CONSTRAINT DF_pms_hotel_monthly_targets_target_amount DEFAULT (0),
        commission_before_85 DECIMAL(9, 4) NOT NULL
            CONSTRAINT DF_pms_hotel_monthly_targets_commission_before_85 DEFAULT (0),
        commission_at_85 DECIMAL(9, 4) NOT NULL
            CONSTRAINT DF_pms_hotel_monthly_targets_commission_at_85 DEFAULT (0),
        commission_86_to_100 DECIMAL(9, 4) NOT NULL
            CONSTRAINT DF_pms_hotel_monthly_targets_commission_86_to_100 DEFAULT (0),
        created_at DATETIME2(7) NOT NULL
            CONSTRAINT DF_pms_hotel_monthly_targets_created_at DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2(7) NULL
    );
END;

IF COL_LENGTH(N'dbo.pms_hotel_monthly_targets', N'id') IS NOT NULL
   AND COL_LENGTH(N'dbo.pms_hotel_monthly_targets', N'hotel_monthly_target_id') IS NULL
BEGIN
    EXEC sp_rename N'dbo.pms_hotel_monthly_targets.id', N'hotel_monthly_target_id', N'COLUMN';
END;

IF COL_LENGTH(N'dbo.pms_hotel_monthly_targets', N'hotel_id') IS NOT NULL
   AND COL_LENGTH(N'dbo.pms_hotel_monthly_targets', N'hotel_zaaer_id') IS NULL
BEGIN
    EXEC sp_rename N'dbo.pms_hotel_monthly_targets.hotel_id', N'hotel_zaaer_id', N'COLUMN';
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_pms_hotel_monthly_targets_hotel_month'
      AND object_id = OBJECT_ID(N'dbo.pms_hotel_monthly_targets')
)
BEGIN
    CREATE UNIQUE INDEX UX_pms_hotel_monthly_targets_hotel_month
        ON dbo.pms_hotel_monthly_targets (hotel_zaaer_id, month_year);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_pms_hotel_monthly_targets_month_year'
      AND object_id = OBJECT_ID(N'dbo.pms_hotel_monthly_targets')
)
BEGIN
    CREATE INDEX IX_pms_hotel_monthly_targets_month_year
        ON dbo.pms_hotel_monthly_targets (month_year);
END;

PRINT N'pms_hotel_monthly_targets migration completed.';
