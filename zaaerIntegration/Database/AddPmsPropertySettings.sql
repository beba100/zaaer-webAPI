/*
  PMS property settings: extend buildings, floors, room_types, apartments; create facilities.
  Run on each tenant database after backup.
*/

SET NOCOUNT ON;

-- buildings
IF COL_LENGTH('dbo.buildings', 'is_active') IS NULL
BEGIN
    ALTER TABLE dbo.buildings ADD is_active BIT NOT NULL CONSTRAINT DF_buildings_is_active DEFAULT (1);
    PRINT 'Added buildings.is_active';
END

IF COL_LENGTH('dbo.buildings', 'description') IS NULL
BEGIN
    ALTER TABLE dbo.buildings ADD description NVARCHAR(500) NULL;
    PRINT 'Added buildings.description';
END

-- floors
IF COL_LENGTH('dbo.floors', 'sort_order') IS NULL
BEGIN
    ALTER TABLE dbo.floors ADD sort_order INT NOT NULL CONSTRAINT DF_floors_sort_order DEFAULT (0);
    PRINT 'Added floors.sort_order';
END

IF COL_LENGTH('dbo.floors', 'is_active') IS NULL
BEGIN
    ALTER TABLE dbo.floors ADD is_active BIT NOT NULL CONSTRAINT DF_floors_is_active DEFAULT (1);
    PRINT 'Added floors.is_active';
END

-- Drop FK so building_id can store buildings.zaaer_id
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_Floors_Buildings' AND parent_object_id = OBJECT_ID('dbo.floors'))
BEGIN
    ALTER TABLE dbo.floors DROP CONSTRAINT FK_Floors_Buildings;
    PRINT 'Dropped FK_Floors_Buildings';
END

-- room_types
IF COL_LENGTH('dbo.room_types', 'roomtype_name_en') IS NULL
    ALTER TABLE dbo.room_types ADD roomtype_name_en NVARCHAR(200) NULL;

IF COL_LENGTH('dbo.room_types', 'room_category') IS NULL
    ALTER TABLE dbo.room_types ADD room_category NVARCHAR(100) NULL;

IF COL_LENGTH('dbo.room_types', 'room_count') IS NULL
    ALTER TABLE dbo.room_types ADD room_count INT NOT NULL CONSTRAINT DF_room_types_room_count DEFAULT (0);

IF COL_LENGTH('dbo.room_types', 'sort_order') IS NULL
    ALTER TABLE dbo.room_types ADD sort_order INT NOT NULL CONSTRAINT DF_room_types_sort_order DEFAULT (0);

IF COL_LENGTH('dbo.room_types', 'is_active') IS NULL
    ALTER TABLE dbo.room_types ADD is_active BIT NOT NULL CONSTRAINT DF_room_types_is_active DEFAULT (1);

IF COL_LENGTH('dbo.room_types', 'image_url') IS NULL
    ALTER TABLE dbo.room_types ADD image_url NVARCHAR(500) NULL;

-- apartments detail fields
IF COL_LENGTH('dbo.apartments', 'telephone_extension') IS NULL
    ALTER TABLE dbo.apartments ADD telephone_extension NVARCHAR(50) NULL;

IF COL_LENGTH('dbo.apartments', 'bathrooms_count') IS NULL
    ALTER TABLE dbo.apartments ADD bathrooms_count INT NOT NULL CONSTRAINT DF_apartments_bathrooms_count DEFAULT (0);

IF COL_LENGTH('dbo.apartments', 'kitchen_type') IS NULL
    ALTER TABLE dbo.apartments ADD kitchen_type NVARCHAR(50) NULL;

IF COL_LENGTH('dbo.apartments', 'hall_type') IS NULL
    ALTER TABLE dbo.apartments ADD hall_type NVARCHAR(50) NULL;

IF COL_LENGTH('dbo.apartments', 'single_beds_count') IS NULL
    ALTER TABLE dbo.apartments ADD single_beds_count INT NOT NULL CONSTRAINT DF_apartments_single_beds DEFAULT (0);

IF COL_LENGTH('dbo.apartments', 'double_beds_count') IS NULL
    ALTER TABLE dbo.apartments ADD double_beds_count INT NOT NULL CONSTRAINT DF_apartments_double_beds DEFAULT (0);

IF COL_LENGTH('dbo.apartments', 'area') IS NULL
    ALTER TABLE dbo.apartments ADD area DECIMAL(12, 2) NULL;

IF COL_LENGTH('dbo.apartments', 'description') IS NULL
    ALTER TABLE dbo.apartments ADD description NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.apartments', 'is_active') IS NULL
    ALTER TABLE dbo.apartments ADD is_active BIT NOT NULL CONSTRAINT DF_apartments_is_active DEFAULT (1);

IF COL_LENGTH('dbo.apartments', 'services_json') IS NULL
    ALTER TABLE dbo.apartments ADD services_json NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.apartments', 'facilities_json') IS NULL
    ALTER TABLE dbo.apartments ADD facilities_json NVARCHAR(MAX) NULL;

-- facilities
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'facilities' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.facilities (
        facility_id     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id        INT NOT NULL,
        zaaer_id        INT NULL,
        facility_name   NVARCHAR(200) NOT NULL,
        description     NVARCHAR(MAX) NULL,
        building_id     INT NULL,
        floor_id        INT NULL,
        image_urls_json NVARCHAR(MAX) NULL,
        is_active       BIT NOT NULL CONSTRAINT DF_facilities_is_active DEFAULT (1),
        created_at      DATETIME2 NULL,
        updated_at      DATETIME2 NULL,
        CONSTRAINT FK_facilities_hotel_settings FOREIGN KEY (hotel_id)
            REFERENCES dbo.hotel_settings(hotel_id)
    );

    CREATE INDEX IX_facilities_hotel_id ON dbo.facilities(hotel_id);
    CREATE INDEX IX_facilities_zaaer_id ON dbo.facilities(zaaer_id) WHERE zaaer_id IS NOT NULL;

    PRINT 'Created facilities table';
END
ELSE
    PRINT 'facilities table already exists';

IF COL_LENGTH('dbo.facilities', 'facility_name_en') IS NULL
    ALTER TABLE dbo.facilities ADD facility_name_en NVARCHAR(200) NULL;

PRINT 'AddPmsPropertySettings.sql completed.';
