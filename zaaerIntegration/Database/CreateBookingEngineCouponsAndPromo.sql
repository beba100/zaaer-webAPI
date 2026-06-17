-- Tenant DB — booking engine coupons + seasonal promo banner on settings.
-- Run after CreateBookingEngineTables.sql on each tenant database.

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.booking_engine_coupons', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.booking_engine_coupons (
        coupon_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        coupon_no NVARCHAR(50) NOT NULL,
        promo_code NVARCHAR(40) NOT NULL,
        title NVARCHAR(200) NULL,
        discount_type NVARCHAR(20) NOT NULL CONSTRAINT CK_booking_engine_coupons_discount_type
            CHECK (discount_type IN (N'percent', N'fixed')),
        discount_value DECIMAL(12,2) NOT NULL CONSTRAINT CK_booking_engine_coupons_discount_value CHECK (discount_value > 0),
        min_stay_nights INT NULL,
        min_booking_amount DECIMAL(12,2) NULL,
        max_redemptions INT NULL,
        redemption_count INT NOT NULL CONSTRAINT DF_booking_engine_coupons_redemptions DEFAULT (0),
        valid_from DATE NULL,
        valid_to DATE NULL,
        room_type_ids NVARCHAR(500) NULL,
        is_active BIT NOT NULL CONSTRAINT DF_booking_engine_coupons_active DEFAULT (1),
        notes NVARCHAR(1000) NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_booking_engine_coupons_created DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2 NULL,
        CONSTRAINT UQ_booking_engine_coupons_no UNIQUE (coupon_no),
        CONSTRAINT UQ_booking_engine_coupons_hotel_promo UNIQUE (hotel_id, promo_code)
    );

    CREATE INDEX IX_booking_engine_coupons_hotel_active
        ON dbo.booking_engine_coupons (hotel_id, is_active, valid_to);
END
GO

IF COL_LENGTH(N'dbo.booking_engine_settings', N'promo_banner_enabled') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings ADD promo_banner_enabled BIT NOT NULL
        CONSTRAINT DF_booking_engine_settings_promo_enabled DEFAULT (0);
END
GO

IF COL_LENGTH(N'dbo.booking_engine_settings', N'promo_banner_image_url') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings ADD promo_banner_image_url NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'dbo.booking_engine_settings', N'promo_banner_html') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings ADD promo_banner_html NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH(N'dbo.booking_engine_settings', N'promo_banner_ends_at') IS NULL
BEGIN
    ALTER TABLE dbo.booking_engine_settings ADD promo_banner_ends_at DATETIME2 NULL;
END
GO

IF COL_LENGTH(N'dbo.reservations', N'booking_coupon_id') IS NULL
BEGIN
    ALTER TABLE dbo.reservations ADD booking_coupon_id INT NULL;
END
GO

IF COL_LENGTH(N'dbo.reservations', N'coupon_promo_code') IS NULL
BEGIN
    ALTER TABLE dbo.reservations ADD coupon_promo_code NVARCHAR(40) NULL;
END
GO

PRINT 'CreateBookingEngineCouponsAndPromo.sql completed.';
GO
