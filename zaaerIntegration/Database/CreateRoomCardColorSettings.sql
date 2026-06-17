-- Tenant DB: per-room occupied-card color settings for PMS Room Board.
-- Link: room_card_color_settings.apartment_zaaer_id = apartments.zaaer_id
-- If apartments.zaaer_id is NULL, application falls back to apartments.apartment_id.

IF OBJECT_ID(N'dbo.room_card_color_settings', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[room_card_color_settings] (
        [setting_id] INT IDENTITY(1, 1) NOT NULL,
        [hotel_id] INT NOT NULL,
        [apartment_zaaer_id] INT NOT NULL,
        [occupied_card_back_color] NVARCHAR(40) NULL,
        [occupied_header_back_color] NVARCHAR(40) NULL,
        [occupied_guest_back_color] NVARCHAR(40) NULL,
        [occupied_dates_back_color] NVARCHAR(40) NULL,
        [occupied_text_color] NVARCHAR(40) NULL,
        [is_active] BIT NOT NULL CONSTRAINT [DF_room_card_color_settings_is_active] DEFAULT (1),
        [created_at] DATETIME2(0) NOT NULL CONSTRAINT [DF_room_card_color_settings_created_at] DEFAULT (SYSUTCDATETIME()),
        [updated_at] DATETIME2(0) NULL,
        CONSTRAINT [PK_room_card_color_settings] PRIMARY KEY CLUSTERED ([setting_id] ASC)
    );

    CREATE UNIQUE INDEX [UX_room_card_color_settings_hotel_apartment]
        ON [dbo].[room_card_color_settings] ([hotel_id], [apartment_zaaer_id]);
END
GO
