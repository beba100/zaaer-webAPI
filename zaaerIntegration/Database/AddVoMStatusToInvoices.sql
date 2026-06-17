-- =====================================================================
-- Add status_vom and vom_payload to invoices table
-- =====================================================================
-- Purpose: Track VoM sync status directly on invoice records
-- status_vom: 'pending' (default) | 'sent' | 'failed'
-- vom_payload: JSON payload sent to VoM (for audit/retry)
-- =====================================================================

USE [YOUR_TENANT_DATABASE_NAME]; -- Replace with actual tenant database name
GO

PRINT '========================================';
PRINT 'Adding status_vom and vom_payload to invoices table';
PRINT '========================================';
PRINT '';

-- Step 1: Add status_vom column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[invoices]') 
               AND name = 'status_vom')
BEGIN
    ALTER TABLE [dbo].[invoices]
    ADD [status_vom] NVARCHAR(20) NOT NULL DEFAULT 'pending';
    
    PRINT '✅ Added status_vom column to invoices table (default: pending)';
END
ELSE
BEGIN
    PRINT '⚠️ status_vom column already exists - skipping';
END
GO

-- Step 2: Add vom_payload column (JSON)
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[invoices]') 
               AND name = 'vom_payload')
BEGIN
    ALTER TABLE [dbo].[invoices]
    ADD [vom_payload] NVARCHAR(MAX) NULL;
    
    PRINT '✅ Added vom_payload column to invoices table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_payload column already exists - skipping';
END
GO

-- Step 3: Add vom_sent_at column (timestamp when sent to VoM)
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[invoices]') 
               AND name = 'vom_sent_at')
BEGIN
    ALTER TABLE [dbo].[invoices]
    ADD [vom_sent_at] DATETIME NULL;
    
    PRINT '✅ Added vom_sent_at column to invoices table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_sent_at column already exists - skipping';
END
GO

-- Step 4: Add vom_error column (error message if failed)
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[invoices]') 
               AND name = 'vom_error')
BEGIN
    ALTER TABLE [dbo].[invoices]
    ADD [vom_error] NVARCHAR(MAX) NULL;
    
    PRINT '✅ Added vom_error column to invoices table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_error column already exists - skipping';
END
GO

-- Step 5: Add vom_retry_count column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[invoices]') 
               AND name = 'vom_retry_count')
BEGIN
    ALTER TABLE [dbo].[invoices]
    ADD [vom_retry_count] INT NOT NULL DEFAULT 0;
    
    PRINT '✅ Added vom_retry_count column to invoices table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_retry_count column already exists - skipping';
END
GO

-- Step 6: Create index on status_vom for fast queries
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE object_id = OBJECT_ID('[dbo].[invoices]') 
               AND name = 'IX_Invoices_StatusVoM')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoices_StatusVoM]
    ON [dbo].[invoices] ([status_vom])
    INCLUDE ([invoice_id], [invoice_no], [zaaer_id], [invoice_date], [total_amount]);
    
    PRINT '✅ Created index IX_Invoices_StatusVoM for fast queries';
END
ELSE
BEGIN
    PRINT '⚠️ Index IX_Invoices_StatusVoM already exists - skipping';
END
GO

-- Step 7: Update existing invoices that have been sent to VoM
-- Mark as 'sent' if they exist in invoice_journal_entries with status 'Sent'
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'invoice_journal_entries')
BEGIN
    UPDATE i
    SET i.status_vom = 'sent',
        i.vom_sent_at = ije.created_at
    FROM invoices i
    INNER JOIN invoice_journal_entries ije ON i.invoice_id = ije.invoice_id
    WHERE ije.status = 'Sent';
    
    PRINT '✅ Updated existing invoices that were already sent to VoM';
END
GO

-- Step 8: Verify changes
PRINT '';
PRINT '========================================';
PRINT 'Verification:';
PRINT '========================================';

SELECT 
    'invoices' AS TableName,
    status_vom AS Status,
    COUNT(*) AS RecordCount
FROM invoices
GROUP BY status_vom
ORDER BY status_vom;
GO

PRINT '';
PRINT '========================================';
PRINT 'Summary:';
PRINT '========================================';
PRINT 'Columns added to invoices table:';
PRINT '  - status_vom (NVARCHAR(20), default: pending)';
PRINT '  - vom_payload (NVARCHAR(MAX), nullable)';
PRINT '  - vom_sent_at (DATETIME, nullable)';
PRINT '  - vom_error (NVARCHAR(MAX), nullable)';
PRINT '  - vom_retry_count (INT, default: 0)';
PRINT '';
PRINT 'Index created: IX_Invoices_StatusVoM';
PRINT '';
PRINT '✅ Migration completed successfully!';
PRINT '';
PRINT 'Usage:';
PRINT '  - Query pending invoices: SELECT * FROM invoices WHERE status_vom = ''pending''';
PRINT '  - Query failed invoices: SELECT * FROM invoices WHERE status_vom = ''failed''';
PRINT '  - Query sent invoices: SELECT * FROM invoices WHERE status_vom = ''sent''';
GO

