-- =============================================
-- Alter Customer MobileNo Column Length
-- تحديث طول عمود mobile_no في جدول customers
-- =============================================
-- Purpose: Increase MobileNo length from 20 to 50 to support international phone numbers
-- الغرض: زيادة طول MobileNo من 20 إلى 50 لدعم أرقام الهواتف الدولية

PRINT '========================================';
PRINT 'Altering customers.mobile_no column length';
PRINT '========================================';
PRINT '';

-- Check current column length
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'customers' 
           AND COLUMN_NAME = 'mobile_no')
BEGIN
    DECLARE @CurrentLength INT;
    SELECT @CurrentLength = CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'customers' 
    AND COLUMN_NAME = 'mobile_no';
    
    PRINT 'Current mobile_no length: ' + CAST(@CurrentLength AS VARCHAR(10));
    
    -- Only alter if current length is less than 50
    IF @CurrentLength < 50
    BEGIN
        ALTER TABLE [dbo].[customers]
        ALTER COLUMN [mobile_no] NVARCHAR(50) NULL;
        
        PRINT '✓ Successfully altered customers.mobile_no to NVARCHAR(50)';
    END
    ELSE
    BEGIN
        PRINT '✓ customers.mobile_no is already NVARCHAR(50) or larger. No changes needed.';
    END
END
ELSE
BEGIN
    PRINT '✗ Column customers.mobile_no not found.';
END

PRINT '';
PRINT '========================================';
PRINT 'Alteration completed.';
PRINT '========================================';

