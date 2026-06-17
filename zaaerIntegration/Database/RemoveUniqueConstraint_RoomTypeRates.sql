-- =============================================
-- Remove Old Unique Constraint and Add Smart Filtered Unique Index
-- إزالة القيد القديم وإضافة فهرس فريد ذكي
-- Purpose: Ensure one rate per (ZaaerId + RoomTypeId + HotelId) combination
-- القاعدة الذهبية: لكل (ZaaerId + RoomTypeId + HotelId) يجب أن يكون سجل واحد فقط
-- =============================================

-- Step 1: Drop the old unique constraint/index if it exists
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RoomTypeRates_RoomTypeId_HotelId' AND object_id = OBJECT_ID('room_type_rates'))
BEGIN
    DROP INDEX [IX_RoomTypeRates_RoomTypeId_HotelId] ON [dbo].[room_type_rates];
    PRINT '✅ Old unique constraint/index [IX_RoomTypeRates_RoomTypeId_HotelId] dropped successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Old index [IX_RoomTypeRates_RoomTypeId_HotelId] does not exist.';
END
GO

-- Step 2: Also check for unique constraint with different name (in case it was created differently)
IF EXISTS (SELECT * FROM sys.indexes WHERE name LIKE '%RoomTypeRates%RoomTypeId%HotelId%' AND object_id = OBJECT_ID('room_type_rates') AND name != 'UX_RoomTypeRates_Zaaer_Composite')
BEGIN
    DECLARE @IndexName NVARCHAR(255);
    SELECT @IndexName = name FROM sys.indexes WHERE name LIKE '%RoomTypeRates%RoomTypeId%HotelId%' AND object_id = OBJECT_ID('room_type_rates') AND name != 'UX_RoomTypeRates_Zaaer_Composite';
    EXEC('DROP INDEX [' + @IndexName + '] ON [dbo].[room_type_rates]');
    PRINT '✅ Old unique constraint/index [' + @IndexName + '] dropped successfully.';
END
GO

-- Step 3: Create new Filtered Unique Index for (ZaaerId + RoomTypeId + HotelId)
-- This index only applies when ZaaerId IS NOT NULL, allowing old records without ZaaerId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_RoomTypeRates_Zaaer_Composite' AND object_id = OBJECT_ID('room_type_rates'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_RoomTypeRates_Zaaer_Composite]
        ON [dbo].[room_type_rates] ([zaaer_id] ASC, [roomtype_id] ASC, [hotel_id] ASC)
        WHERE [zaaer_id] IS NOT NULL;
    
    PRINT '✅ Smart filtered unique index [UX_RoomTypeRates_Zaaer_Composite] created successfully.';
    PRINT '   This index ensures one rate per (ZaaerId + RoomTypeId + HotelId) combination.';
    PRINT '   Records with NULL zaaer_id are not affected by this constraint.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [UX_RoomTypeRates_Zaaer_Composite] already exists.';
END
GO

