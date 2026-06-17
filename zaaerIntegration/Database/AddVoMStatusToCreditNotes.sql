-- =====================================================================
-- Add status_vom and vom_payload to credit_notes table
-- =====================================================================
-- Purpose: Track VoM sync status directly on credit note records
-- status_vom: 'pending' (default) | 'sent' | 'failed'
-- vom_payload: JSON payload sent to VoM (for audit/retry)
-- =====================================================================

USE [YOUR_TENANT_DATABASE_NAME]; -- Replace with actual tenant database name
GO

PRINT '========================================';
PRINT 'Adding status_vom and vom_payload to credit_notes table';
PRINT '========================================';
PRINT '';

-- Step 1: Add status_vom column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[credit_notes]') 
               AND name = 'status_vom')
BEGIN
    ALTER TABLE [dbo].[credit_notes]
    ADD [status_vom] NVARCHAR(20) NOT NULL DEFAULT 'pending';
    
    PRINT '✅ Added status_vom column to credit_notes table (default: pending)';
END
ELSE
BEGIN
    PRINT '⚠️ status_vom column already exists - skipping';
END
GO

-- Step 2: Add vom_payload column (JSON)
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[credit_notes]') 
               AND name = 'vom_payload')
BEGIN
    ALTER TABLE [dbo].[credit_notes]
    ADD [vom_payload] NVARCHAR(MAX) NULL;
    
    PRINT '✅ Added vom_payload column to credit_notes table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_payload column already exists - skipping';
END
GO

-- Step 3: Add vom_sent_at column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[credit_notes]') 
               AND name = 'vom_sent_at')
BEGIN
    ALTER TABLE [dbo].[credit_notes]
    ADD [vom_sent_at] DATETIME NULL;
    
    PRINT '✅ Added vom_sent_at column to credit_notes table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_sent_at column already exists - skipping';
END
GO

-- Step 4: Add vom_error column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[credit_notes]') 
               AND name = 'vom_error')
BEGIN
    ALTER TABLE [dbo].[credit_notes]
    ADD [vom_error] NVARCHAR(MAX) NULL;
    
    PRINT '✅ Added vom_error column to credit_notes table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_error column already exists - skipping';
END
GO

-- Step 5: Add vom_retry_count column
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[credit_notes]') 
               AND name = 'vom_retry_count')
BEGIN
    ALTER TABLE [dbo].[credit_notes]
    ADD [vom_retry_count] INT NOT NULL DEFAULT 0;
    
    PRINT '✅ Added vom_retry_count column to credit_notes table';
END
ELSE
BEGIN
    PRINT '⚠️ vom_retry_count column already exists - skipping';
END
GO

-- Step 6: Create index on status_vom
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE object_id = OBJECT_ID('[dbo].[credit_notes]') 
               AND name = 'IX_CreditNotes_StatusVoM')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CreditNotes_StatusVoM]
    ON [dbo].[credit_notes] ([status_vom])
    INCLUDE ([credit_note_id], [credit_note_no], [zaaer_id], [credit_note_date], [credit_amount]);
    
    PRINT '✅ Created index IX_CreditNotes_StatusVoM';
END
ELSE
BEGIN
    PRINT '⚠️ Index IX_CreditNotes_StatusVoM already exists - skipping';
END
GO

-- Step 7: Update existing credit notes that have been sent
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'credit_note_journal_entries')
BEGIN
    UPDATE cn
    SET cn.status_vom = 'sent',
        cn.vom_sent_at = cnje.created_at
    FROM credit_notes cn
    INNER JOIN credit_note_journal_entries cnje ON cn.credit_note_id = cnje.credit_note_id
    WHERE cnje.status = 'Sent';
    
    PRINT '✅ Updated existing credit notes that were already sent to VoM';
END
GO

-- Step 8: Verify
PRINT '';
PRINT '========================================';
PRINT 'Verification:';
PRINT '========================================';

SELECT 
    'credit_notes' AS TableName,
    status_vom AS Status,
    COUNT(*) AS RecordCount
FROM credit_notes
GROUP BY status_vom
ORDER BY status_vom;
GO

PRINT '';
PRINT '✅ Migration completed successfully!';
GO

