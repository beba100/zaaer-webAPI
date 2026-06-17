-- Controls public booking search rental-type dropdown: both | daily_only | monthly_only | hidden
IF COL_LENGTH(N'dbo.booking_engine_settings', N'rental_type_mode') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings
        ADD rental_type_mode NVARCHAR(20) NOT NULL
            CONSTRAINT DF_booking_engine_settings_rental_mode DEFAULT (N'both');
END
GO

PRINT 'AlterBookingEngineRentalTypeMode.sql completed.';
