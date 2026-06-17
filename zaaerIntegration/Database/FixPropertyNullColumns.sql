-- One-time cleanup for legacy NULL integer/bit columns on property tables.
-- Run on tenant DB if SqlNullValueException persists on property/hall lookups.

IF COL_LENGTH('dbo.apartments', 'bathrooms_count') IS NOT NULL
    UPDATE dbo.apartments SET bathrooms_count = 0 WHERE bathrooms_count IS NULL;

IF COL_LENGTH('dbo.apartments', 'single_beds_count') IS NOT NULL
    UPDATE dbo.apartments SET single_beds_count = 0 WHERE single_beds_count IS NULL;

IF COL_LENGTH('dbo.apartments', 'double_beds_count') IS NOT NULL
    UPDATE dbo.apartments SET double_beds_count = 0 WHERE double_beds_count IS NULL;

IF COL_LENGTH('dbo.apartments', 'is_active') IS NOT NULL
    UPDATE dbo.apartments SET is_active = 1 WHERE is_active IS NULL;

IF COL_LENGTH('dbo.room_types', 'room_count') IS NOT NULL
    UPDATE dbo.room_types SET room_count = 0 WHERE room_count IS NULL;

IF COL_LENGTH('dbo.room_types', 'sort_order') IS NOT NULL
    UPDATE dbo.room_types SET sort_order = 0 WHERE sort_order IS NULL;

IF COL_LENGTH('dbo.room_types', 'is_active') IS NOT NULL
    UPDATE dbo.room_types SET is_active = 1 WHERE is_active IS NULL;

IF COL_LENGTH('dbo.floors', 'sort_order') IS NOT NULL
    UPDATE dbo.floors SET sort_order = 0 WHERE sort_order IS NULL;

IF COL_LENGTH('dbo.floors', 'floor_number') IS NOT NULL
    UPDATE dbo.floors SET floor_number = 0 WHERE floor_number IS NULL;

IF COL_LENGTH('dbo.floors', 'is_active') IS NOT NULL
    UPDATE dbo.floors SET is_active = 1 WHERE is_active IS NULL;

IF COL_LENGTH('dbo.buildings', 'is_active') IS NOT NULL
    UPDATE dbo.buildings SET is_active = 1 WHERE is_active IS NULL;

PRINT 'Property NULL column cleanup complete.';
