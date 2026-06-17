-- Hall / venue event booking schema (tenant DB)

IF COL_LENGTH('dbo.room_types', 'hall_gender_type') IS NULL
    ALTER TABLE dbo.room_types ADD hall_gender_type NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.room_types', 'hall_capacity') IS NULL
    ALTER TABLE dbo.room_types ADD hall_capacity INT NULL;
GO

IF COL_LENGTH('dbo.room_types', 'allow_split') IS NULL
    ALTER TABLE dbo.room_types ADD allow_split BIT NULL;
GO

IF COL_LENGTH('dbo.room_types', 'minimum_booking_hours') IS NULL
    ALTER TABLE dbo.room_types ADD minimum_booking_hours INT NULL;
GO

IF COL_LENGTH('dbo.room_types', 'venue_kind') IS NULL
    ALTER TABLE dbo.room_types ADD venue_kind NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.apartments', 'hall_preparation_status') IS NULL
    ALTER TABLE dbo.apartments ADD hall_preparation_status NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.packages', 'price_type') IS NULL
    ALTER TABLE dbo.packages ADD price_type NVARCHAR(30) NOT NULL CONSTRAINT DF_packages_price_type DEFAULT('fixed');
GO

IF COL_LENGTH('dbo.packages', 'package_category') IS NULL
    ALTER TABLE dbo.packages ADD package_category NVARCHAR(50) NULL;
GO

IF OBJECT_ID('dbo.reservation_event_profiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.reservation_event_profiles (
        event_profile_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        reservation_id INT NOT NULL,
        hall_id INT NULL,
        event_type NVARCHAR(50) NOT NULL CONSTRAINT DF_rep_event_type DEFAULT('wedding'),
        event_date DATE NOT NULL,
        event_start_time TIME NOT NULL,
        event_end_time TIME NOT NULL,
        expected_guests INT NOT NULL CONSTRAINT DF_rep_expected_guests DEFAULT(0),
        actual_guests INT NULL,
        occasion_name NVARCHAR(300) NULL,
        occasion_owner NVARCHAR(300) NULL,
        deposit_amount DECIMAL(12,2) NOT NULL CONSTRAINT DF_rep_deposit DEFAULT(0),
        remaining_balance DECIMAL(12,2) NOT NULL CONSTRAINT DF_rep_balance DEFAULT(0),
        contract_signed BIT NOT NULL CONSTRAINT DF_rep_contract DEFAULT(0),
        contract_signed_at DATETIME2 NULL,
        event_status NVARCHAR(50) NOT NULL CONSTRAINT DF_rep_status DEFAULT('inquiry'),
        completion_notes NVARCHAR(MAX) NULL,
        deposit_due_at DATETIME2 NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_rep_created DEFAULT(SYSDATETIME()),
        updated_at DATETIME2 NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_rep_hotel_event_date' AND object_id = OBJECT_ID('dbo.reservation_event_profiles'))
    CREATE INDEX IX_rep_hotel_event_date ON dbo.reservation_event_profiles(hotel_id, event_date);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_rep_hall_event_date' AND object_id = OBJECT_ID('dbo.reservation_event_profiles'))
    CREATE INDEX IX_rep_hall_event_date ON dbo.reservation_event_profiles(hall_id, event_date);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_rep_reservation' AND object_id = OBJECT_ID('dbo.reservation_event_profiles'))
    CREATE INDEX IX_rep_reservation ON dbo.reservation_event_profiles(reservation_id);
GO

IF OBJECT_ID('dbo.event_function_sheets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.event_function_sheets (
        function_sheet_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        reservation_id INT NOT NULL,
        hall_id INT NULL,
        event_date DATE NOT NULL,
        hall_open_time TIME NULL,
        guest_arrival_time TIME NULL,
        service_start_time TIME NULL,
        coffee_type NVARCHAR(200) NULL,
        menu_notes NVARCHAR(MAX) NULL,
        decoration_notes NVARCHAR(MAX) NULL,
        sound_av_notes NVARCHAR(MAX) NULL,
        coordinator_user_id INT NULL,
        client_special_requests NVARCHAR(MAX) NULL,
        execution_status NVARCHAR(30) NOT NULL CONSTRAINT DF_efs_status DEFAULT('draft'),
        printed_at DATETIME2 NULL,
        approved_by INT NULL,
        approved_at DATETIME2 NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_efs_created DEFAULT(SYSDATETIME()),
        updated_at DATETIME2 NULL
    );
END;
GO

IF OBJECT_ID('dbo.event_function_sheet_items', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.event_function_sheet_items (
        item_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        function_sheet_id INT NOT NULL,
        category NVARCHAR(50) NULL,
        item_name NVARCHAR(300) NOT NULL,
        quantity DECIMAL(12,2) NOT NULL CONSTRAINT DF_efsi_qty DEFAULT(1),
        notes NVARCHAR(MAX) NULL,
        sort_order INT NOT NULL CONSTRAINT DF_efsi_sort DEFAULT(0)
    );
END;
GO

IF OBJECT_ID('dbo.hall_event_alerts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.hall_event_alerts (
        alert_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        reservation_id INT NOT NULL,
        alert_type NVARCHAR(50) NOT NULL,
        message NVARCHAR(500) NOT NULL,
        severity NVARCHAR(20) NOT NULL CONSTRAINT DF_hea_severity DEFAULT('info'),
        is_read BIT NOT NULL CONSTRAINT DF_hea_read DEFAULT(0),
        due_at DATETIME2 NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_hea_created DEFAULT(SYSDATETIME())
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_hea_hotel_unread' AND object_id = OBJECT_ID('dbo.hall_event_alerts'))
    CREATE INDEX IX_hea_hotel_unread ON dbo.hall_event_alerts(hotel_id, is_read, created_at DESC);
GO

-- Backfill NULL integer columns on legacy/manual apartment rows (prevents EF SqlNullValueException).
IF COL_LENGTH('dbo.apartments', 'bathrooms_count') IS NOT NULL
    UPDATE dbo.apartments SET bathrooms_count = 0 WHERE bathrooms_count IS NULL;
GO

IF COL_LENGTH('dbo.apartments', 'single_beds_count') IS NOT NULL
    UPDATE dbo.apartments SET single_beds_count = 0 WHERE single_beds_count IS NULL;
GO

IF COL_LENGTH('dbo.apartments', 'double_beds_count') IS NOT NULL
    UPDATE dbo.apartments SET double_beds_count = 0 WHERE double_beds_count IS NULL;
GO

IF COL_LENGTH('dbo.apartments', 'is_active') IS NOT NULL
    UPDATE dbo.apartments SET is_active = 1 WHERE is_active IS NULL;
GO

IF COL_LENGTH('dbo.room_types', 'room_count') IS NOT NULL
    UPDATE dbo.room_types SET room_count = 0 WHERE room_count IS NULL;
GO

IF COL_LENGTH('dbo.room_types', 'sort_order') IS NOT NULL
    UPDATE dbo.room_types SET sort_order = 0 WHERE sort_order IS NULL;
GO
