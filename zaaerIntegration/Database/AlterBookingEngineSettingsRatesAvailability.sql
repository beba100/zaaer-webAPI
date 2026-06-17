IF COL_LENGTH('dbo.booking_engine_settings', 'availability_mode') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings
        ADD availability_mode NVARCHAR(20) NOT NULL
            CONSTRAINT DF_BeSettings_AvailabilityMode DEFAULT N'actual';
    PRINT 'Added booking_engine_settings.availability_mode';
END

IF COL_LENGTH('dbo.booking_engine_settings', 'rate_fallback_mode') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings
        ADD rate_fallback_mode NVARCHAR(20) NOT NULL
            CONSTRAINT DF_BeSettings_RateFallbackMode DEFAULT N'standard';
    PRINT 'Added booking_engine_settings.rate_fallback_mode';
END

IF COL_LENGTH('dbo.booking_engine_settings', 'rate_fallback_min') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings
        ADD rate_fallback_min DECIMAL(12,2) NULL;
    PRINT 'Added booking_engine_settings.rate_fallback_min';
END

IF COL_LENGTH('dbo.booking_engine_settings', 'rate_fallback_max') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings
        ADD rate_fallback_max DECIMAL(12,2) NULL;
    PRINT 'Added booking_engine_settings.rate_fallback_max';
END

GO
