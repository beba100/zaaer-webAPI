-- =============================================
-- Create Credit Note Journal Entries Table
-- تتبع القيود المحاسبية لإشعارات الدائن
-- =============================================

-- Drop table if exists (for clean migration)
IF OBJECT_ID('credit_note_journal_entries', 'U') IS NOT NULL
BEGIN
    DROP TABLE credit_note_journal_entries;
    PRINT '✅ Dropped existing credit_note_journal_entries table';
END

-- Create table
CREATE TABLE credit_note_journal_entries
(
    id INT PRIMARY KEY IDENTITY(1,1),
    
    -- Credit Note Reference
    credit_note_id INT NOT NULL,
    credit_note_zaaer_id INT NULL,
    
    -- VoM Journal Entry Details
    vom_journal_entry_id INT NULL,
    journal_entry_code NVARCHAR(50) NOT NULL,
    journal_date DATETIME2 NOT NULL,
    
    -- Amounts
    total_debit DECIMAL(12,2) NOT NULL DEFAULT 0,
    total_credit DECIMAL(12,2) NOT NULL DEFAULT 0,
    
    -- Status Tracking
    status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    -- Values: Pending, Sent, Failed, Cancelled
    
    -- Response Data
    vom_response NVARCHAR(MAX) NULL,
    error_message NVARCHAR(1000) NULL,
    
    -- Retry Logic
    retry_count INT NOT NULL DEFAULT 0,
    last_retry_at DATETIME2 NULL,
    
    -- Audit Fields
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    updated_at DATETIME2 NULL,
    
    -- Foreign Key Constraint
    CONSTRAINT FK_CreditNoteJournalEntries_CreditNotes 
        FOREIGN KEY (credit_note_id) 
        REFERENCES credit_notes(credit_note_id)
        ON DELETE CASCADE
);

-- Create Indexes for Performance
CREATE INDEX IX_CreditNoteJournalEntries_CreditNoteId 
    ON credit_note_journal_entries(credit_note_id);

CREATE INDEX IX_CreditNoteJournalEntries_CreditNoteZaaerId 
    ON credit_note_journal_entries(credit_note_zaaer_id);

CREATE INDEX IX_CreditNoteJournalEntries_Status 
    ON credit_note_journal_entries(status);

CREATE INDEX IX_CreditNoteJournalEntries_JournalDate 
    ON credit_note_journal_entries(journal_date);

CREATE INDEX IX_CreditNoteJournalEntries_VomJournalEntryId 
    ON credit_note_journal_entries(vom_journal_entry_id);

-- Index for finding records to retry
CREATE INDEX IX_CreditNoteJournalEntries_StatusRetryCount 
    ON credit_note_journal_entries(status, retry_count);

-- Unique constraint to prevent duplicate entries
CREATE UNIQUE INDEX UQ_CreditNoteJournalEntries_CreditNoteZaaerId_Sent
    ON credit_note_journal_entries(credit_note_zaaer_id)
    WHERE status = 'Sent' AND credit_note_zaaer_id IS NOT NULL;

PRINT '✅ Created credit_note_journal_entries table successfully';
PRINT '✅ Created indexes for performance';
PRINT '✅ Created unique constraint to prevent duplicates';
GO

-- Verify table creation
IF OBJECT_ID('credit_note_journal_entries', 'U') IS NOT NULL
BEGIN
    PRINT '========================================';
    PRINT '✅ Migration completed successfully!';
    PRINT '========================================';
    
    SELECT 
        COUNT(*) as RowCount,
        'credit_note_journal_entries' as TableName
    FROM credit_note_journal_entries;
END
ELSE
BEGIN
    PRINT '❌ Error: Table was not created!';
END
GO

