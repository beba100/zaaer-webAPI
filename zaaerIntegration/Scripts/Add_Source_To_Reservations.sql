-- =============================================
-- Add source column to reservations table
-- إضافة عمود المصدر لجدول الحجوزات
-- =============================================
-- Description: 
--   Adds source column to track the platform/source point of the reservation
--   This column stores the source of the reservation (e.g., "المطار", "الموقع", etc.)
--
-- This script is safe to run multiple times (idempotent)
-- =============================================

-- Check if column already exists, then add it if not
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'dbo' 
    AND TABLE_NAME = 'reservations' 
    AND COLUMN_NAME = 'source'
)
BEGIN
    ALTER TABLE [dbo].[reservations]
    ADD [source] NVARCHAR(255) NULL;
    
    PRINT 'Column source added successfully to reservations table.';
END
ELSE
BEGIN
    PRINT 'Column source already exists in reservations table.';
END
GO

