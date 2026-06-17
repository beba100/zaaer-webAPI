-- =====================================================================
-- Add status_vom and vom_payload to payment_receipts table
-- =====================================================================
-- Purpose: Track VoM sync status directly on payment receipt records
-- status_vom: 'pending' (default) | 'sent' | 'failed'
-- vom_payload: JSON payload sent to VoM (for audit/retry)
-- =====================================================================

USE [YOUR_TENANT_DATABASE_NAME]; -- Replace with actual tenant database name
GO

PRINT '========================================';
PRINT 'Adding status_vom and vom_payload to payment_receipts table';
PRINT '========================================';
PRINT '';

-- Step 1: Add status_vom column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[payment_receipts]') 
               AND name = 'status_vom')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [status_vom] NVARCHAR(20) NOT NULL DEFAULT 'pending';
    
    PRINT '✅ Added status_vom column to payment_receipts table (default: pending)';
END
ELSE
BEGIN
    PRINT '⚠️ status_vom column already exists - skipping';
END
GO

-- Step 2: Add vom_payload column (JSON)
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[payment_receipts]') 
               AND name = 'vom_payload')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [vom_payload] NVARCHAR(MAX) NULL;
    
    PRINT '✅ Added vom_payload column to payment_receipts table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_payload column already exists - skipping';
END
GO

-- Step 3: Add vom_sent_at column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[payment_receipts]') 
               AND name = 'vom_sent_at')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [vom_sent_at] DATETIME NULL;
    
    PRINT '✅ Added vom_sent_at column to payment_receipts table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_sent_at column already exists - skipping';
END
GO

-- Step 4: Add vom_error column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[payment_receipts]') 
               AND name = 'vom_error')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [vom_error] NVARCHAR(MAX) NULL;
    
    PRINT '✅ Added vom_error column to payment_receipts table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_error column already exists - skipping';
END
GO

-- Step 5: Add vom_retry_count column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[payment_receipts]') 
               AND name = 'vom_retry_count')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [vom_retry_count] INT NOT NULL DEFAULT 0;
    
    PRINT '✅ Added vom_retry_count column to payment_receipts table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_retry_count column already exists - skipping';
END
GO

-- Step 6: Create index on status_vom
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE object_id = OBJECT_ID('[dbo].[payment_receipts]') 
               AND name = 'IX_PaymentReceipts_StatusVoM')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceipts_StatusVoM]
    ON [dbo].[payment_receipts] ([status_vom])
    INCLUDE ([receipt_id], [receipt_no], [zaaer_id], [receipt_date], [amount_paid]);
    
    PRINT '✅ Created index IX_PaymentReceipts_StatusVoM';
END
ELSE
BEGIN
    PRINT '⚠️ Index IX_PaymentReceipts_StatusVoM already exists - skipping';
END
GO

-- Step 7: Update existing payment receipts that have been sent
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'payment_receipt_journal_entries')
BEGIN
    UPDATE pr
    SET pr.status_vom = 'sent',
        pr.vom_sent_at = prje.created_at
    FROM payment_receipts pr
    INNER JOIN payment_receipt_journal_entries prje ON pr.receipt_id = prje.receipt_id
    WHERE prje.status = 'Sent';
    
    PRINT '✅ Updated existing payment receipts that were already sent to VoM';
END
GO

-- Step 8: Verify
PRINT '';
PRINT '========================================';
PRINT 'Verification:';
PRINT '========================================';

SELECT 
    'payment_receipts' AS TableName,
    status_vom AS Status,
    COUNT(*) AS RecordCount
FROM payment_receipts
GROUP BY status_vom
ORDER BY status_vom;
GO

PRINT '';
PRINT '✅ Migration completed successfully!';
GO

