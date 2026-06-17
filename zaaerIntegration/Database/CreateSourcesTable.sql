-- Tenant DB: reservation channels for dropdown + reservations.source string.
-- Run on each hotel database. Reception uses sort_order = 0 (listed first by API).

IF OBJECT_ID(N'dbo.sources', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[sources] (
        [source_id]      INT IDENTITY(1, 1) NOT NULL,
        [code]           NVARCHAR(100) NOT NULL,
        [name]           NVARCHAR(200) NOT NULL,
        [name_ar]        NVARCHAR(200) NULL,
        [source_type]    NVARCHAR(50) NOT NULL CONSTRAINT [DF_sources_source_type] DEFAULT (N'OTA'),
        [report_name]    NVARCHAR(200) NULL,
        [commission_pct] DECIMAL(6, 2) NOT NULL CONSTRAINT [DF_sources_commission] DEFAULT (0),
        [url]            NVARCHAR(500) NULL,
        [is_active]      BIT NOT NULL CONSTRAINT [DF_sources_active] DEFAULT (1),
        [sort_order]     INT NOT NULL CONSTRAINT [DF_sources_sort] DEFAULT (100),
        CONSTRAINT [PK_sources] PRIMARY KEY CLUSTERED ([source_id] ASC),
        CONSTRAINT [UQ_sources_code] UNIQUE ([code])
    );
END
GO

/* Seed (idempotent): Reception primary first (sort_order 0); then OTAs; legacy codes last */
IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Reception')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Reception', N'Reception', N'الاستقبال', N'primary', N'Reception', 0, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Booking.com')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Booking.com', N'Booking.com', NULL, N'OTA', N'Booking.com', 0, 1, 10);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Airbnb')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Airbnb', N'Airbnb', NULL, N'OTA', N'AirBNB', 0, 1, 20);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Almosafer Web')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Almosafer Web', N'Almosafer Web', NULL, N'OTA', N'Almosafer', 0, 1, 30);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Almatar')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Almatar', N'Almatar', NULL, N'OTA', N'Almatar', 0, 1, 40);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Agoda.com')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Agoda.com', N'Agoda.com', NULL, N'OTA', N'Agoda', 0, 1, 50);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Hotels.com')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Hotels.com', N'Hotels.com', NULL, N'OTA', N'Hotels.com', 0, 1, 60);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Expedia')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Expedia', N'Expedia', NULL, N'OTA', N'Expedia', 0, 1, 70);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Gathern')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Gathern', N'Gathern', NULL, N'OTA', N'Gathern', 0, 1, 80);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Website')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Website', N'Website', N'الموقع', N'OTA', N'Website', 0, 1, 900);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'Phone')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'Phone', N'Phone', N'الهاتف', N'OTA', N'Phone', 0, 1, 910);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'TravelAgent')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'TravelAgent', N'Travel agent', N'وكيل سفر', N'OTA', N'Travel agent', 0, 1, 920);

IF NOT EXISTS (SELECT 1 FROM [dbo].[sources] WHERE [code] = N'WalkIn')
    INSERT INTO [dbo].[sources] ([code], [name], [name_ar], [source_type], [report_name], [commission_pct], [is_active], [sort_order])
    VALUES (N'WalkIn', N'Walk-in', N'حضوري', N'OTA', N'Walk-in', 0, 1, 930);
GO
