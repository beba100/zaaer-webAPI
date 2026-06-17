-- Booking engine settings & media (per tenant database).
-- hotel_id = hotel_settings.zaaer_id (integration id), not hotel_settings.hotel_id PK.
-- room_type_id = room_types.zaaer_id when set; NULL = gallery for all room types.
IF OBJECT_ID(N'dbo.booking_engine_settings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.booking_engine_settings (
        settings_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        is_enabled BIT NOT NULL CONSTRAINT DF_booking_engine_settings_enabled DEFAULT (1),
        public_slug NVARCHAR(80) NULL,
        logo_url NVARCHAR(500) NULL,
        favicon_url NVARCHAR(500) NULL,
        banner_url NVARCHAR(500) NULL,
        show_hotel_picker BIT NOT NULL CONSTRAINT DF_booking_engine_settings_picker DEFAULT (0),
        show_current_branch_only BIT NOT NULL CONSTRAINT DF_booking_engine_settings_branch DEFAULT (1),
        minimum_stay_nights INT NOT NULL CONSTRAINT DF_booking_engine_settings_min_stay DEFAULT (1),
        button_color NVARCHAR(20) NULL,
        border_color NVARCHAR(20) NULL,
        background_color NVARCHAR(20) NULL,
        top_filter_html NVARCHAR(MAX) NULL,
        down_filter_html NVARCHAR(MAX) NULL,
        contact_email NVARCHAR(200) NULL,
        contact_phone NVARCHAR(50) NULL,
        contact_description NVARCHAR(2000) NULL,
        deposit_mode NVARCHAR(20) NOT NULL CONSTRAINT DF_booking_engine_settings_deposit DEFAULT (N'optional'),
        deposit_amount DECIMAL(12,2) NULL,
        deposit_percent DECIMAL(5,2) NULL,
        online_deposit_enabled BIT NOT NULL CONSTRAINT DF_booking_engine_settings_online_dep DEFAULT (0),
        sales_closed BIT NOT NULL CONSTRAINT DF_booking_engine_settings_sales_closed DEFAULT (0),
        sales_closed_message NVARCHAR(1000) NULL,
        rental_type_mode NVARCHAR(20) NOT NULL CONSTRAINT DF_booking_engine_settings_rental_mode DEFAULT (N'both'),
        promo_banner_enabled BIT NOT NULL CONSTRAINT DF_booking_engine_settings_promo_enabled DEFAULT (0),
        promo_banner_image_url NVARCHAR(500) NULL,
        promo_banner_html NVARCHAR(MAX) NULL,
        promo_banner_ends_at DATETIME2 NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_booking_engine_settings_created DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2 NULL,
        CONSTRAINT UQ_booking_engine_settings_hotel UNIQUE (hotel_id)
    );
END
GO

IF OBJECT_ID(N'dbo.booking_engine_media', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.booking_engine_media (
        media_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        room_type_id INT NULL,
        image_url NVARCHAR(500) NOT NULL,
        caption NVARCHAR(300) NULL,
        sort_order INT NOT NULL CONSTRAINT DF_booking_engine_media_sort DEFAULT (0),
        is_primary BIT NOT NULL CONSTRAINT DF_booking_engine_media_primary DEFAULT (0),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_booking_engine_media_created DEFAULT (SYSUTCDATETIME())
    );

    CREATE INDEX IX_booking_engine_media_hotel ON dbo.booking_engine_media (hotel_id, room_type_id, sort_order);
END
GO

PRINT 'CreateBookingEngineTables.sql completed.';
