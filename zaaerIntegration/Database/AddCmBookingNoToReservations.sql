-- Add CmBookingNo column to reservations table
-- إضافة عمود CmBookingNo إلى جدول reservations

USE [zaaerIntegration]

IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'reservations'
    AND COLUMN_NAME = 'cm_booking_no'
)
BEGIN
    ALTER TABLE reservations
    ADD cm_booking_no NVARCHAR(100) NULL;

    PRINT '✅ Column cm_booking_no added to reservations table successfully';
END
ELSE
BEGIN
    PRINT '⚠️ Column cm_booking_no already exists in reservations table';
END

-- Add index for better performance if needed
IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE object_id = OBJECT_ID('reservations')
    AND name = 'IX_reservations_cm_booking_no'
)
BEGIN
    CREATE INDEX IX_reservations_cm_booking_no
    ON reservations(cm_booking_no);

    PRINT '✅ Index IX_reservations_cm_booking_no created successfully';
END
ELSE
BEGIN
    PRINT '⚠️ Index IX_reservations_cm_booking_no already exists';
END