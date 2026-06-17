-- Close online sales on public booking engine (per tenant DB).
IF COL_LENGTH(N'dbo.booking_engine_settings', N'sales_closed') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings
        ADD sales_closed BIT NOT NULL
            CONSTRAINT DF_booking_engine_settings_sales_closed DEFAULT (0);
END
GO

IF COL_LENGTH(N'dbo.booking_engine_settings', N'sales_closed_message') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings
        ADD sales_closed_message NVARCHAR(1000) NULL;
END
GO

PRINT 'AlterBookingEngineSalesClosed.sql completed.';
