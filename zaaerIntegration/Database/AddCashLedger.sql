-- Enterprise cash ledger schema (tenant DB)

DECLARE @legacyProc sysname = N'sp_' + N'Cash' + N'Daily' + N'Report';
IF OBJECT_ID(N'dbo.' + @legacyProc, 'P') IS NOT NULL
    EXEC(N'DROP PROCEDURE dbo.' + @legacyProc);
GO

DECLARE @legacyTestProc sysname = N'sp_' + N'Test' + N'Cash' + N'Transactions';
IF OBJECT_ID(N'dbo.' + @legacyTestProc, 'P') IS NOT NULL
    EXEC(N'DROP PROCEDURE dbo.' + @legacyTestProc);
GO

DECLARE @legacyOptimizedView sysname = N'vw_' + N'Cash' + N'Transactions' + N'_Optimized';
IF OBJECT_ID(N'dbo.' + @legacyOptimizedView, 'V') IS NOT NULL
    EXEC(N'DROP VIEW dbo.' + @legacyOptimizedView);
GO

DECLARE @legacyView sysname = N'vw_' + N'Cash' + N'Transactions';
IF OBJECT_ID(N'dbo.' + @legacyView, 'V') IS NOT NULL
    EXEC(N'DROP VIEW dbo.' + @legacyView);
GO

IF OBJECT_ID('dbo.cash_opening_balance', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.cash_opening_balance
    (
        opening_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        opening_date DATE NOT NULL,
        opening_amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_cash_opening_balance_amount DEFAULT(0),
        notes NVARCHAR(255) NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_cash_opening_balance_created DEFAULT(SYSDATETIME())
    );
END;
GO

IF OBJECT_ID('dbo.cash_ledger', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.cash_ledger
    (
        ledger_id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        cash_box_id INT NULL,
        branch_id INT NULL,
        transaction_date DATETIME2 NOT NULL,
        source_type VARCHAR(50) NOT NULL,
        source_subtype VARCHAR(50) NULL,
        source_id BIGINT NULL,
        source_zaaer_id BIGINT NULL,
        source_no NVARCHAR(50) NULL,
        movement_label NVARCHAR(100) NULL,
        debit_amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_cash_ledger_debit DEFAULT(0),
        credit_amount DECIMAL(18,2) NOT NULL CONSTRAINT DF_cash_ledger_credit DEFAULT(0),
        balance_amount AS (credit_amount - debit_amount) PERSISTED,
        description NVARCHAR(500) NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_cash_ledger_created DEFAULT(SYSDATETIME()),
        created_by INT NULL,
        status VARCHAR(20) NOT NULL CONSTRAINT DF_cash_ledger_status DEFAULT('posted'),
        reversal_of_ledger_id BIGINT NULL,
        idempotency_key NVARCHAR(200) NOT NULL,
        CONSTRAINT CK_cash_ledger_amount
            CHECK (debit_amount >= 0 AND credit_amount >= 0 AND (debit_amount > 0 OR credit_amount > 0))
    );
END;
GO

IF COL_LENGTH('dbo.cash_ledger', 'source_zaaer_id') IS NULL
    ALTER TABLE dbo.cash_ledger ADD source_zaaer_id BIGINT NULL;
GO

IF COL_LENGTH('dbo.cash_ledger', 'cash_box_id') IS NULL
    ALTER TABLE dbo.cash_ledger ADD cash_box_id INT NULL;
GO

IF COL_LENGTH('dbo.cash_ledger', 'branch_id') IS NULL
    ALTER TABLE dbo.cash_ledger ADD branch_id INT NULL;
GO

IF COL_LENGTH('dbo.cash_ledger', 'source_subtype') IS NULL
    ALTER TABLE dbo.cash_ledger ADD source_subtype VARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.cash_ledger', 'movement_label') IS NULL
    ALTER TABLE dbo.cash_ledger ADD movement_label NVARCHAR(100) NULL;
GO

IF COL_LENGTH('dbo.cash_ledger', 'reversal_of_ledger_id') IS NULL
    ALTER TABLE dbo.cash_ledger ADD reversal_of_ledger_id BIGINT NULL;
GO

IF COL_LENGTH('dbo.cash_ledger', 'idempotency_key') IS NULL
    ALTER TABLE dbo.cash_ledger ADD idempotency_key NVARCHAR(200) NULL;
GO

UPDATE dbo.cash_ledger
SET idempotency_key = CONCAT('legacy:', ledger_id)
WHERE idempotency_key IS NULL;
GO

ALTER TABLE dbo.cash_ledger ALTER COLUMN idempotency_key NVARCHAR(200) NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_cash_ledger_idempotency' AND object_id = OBJECT_ID('dbo.cash_ledger'))
    CREATE UNIQUE INDEX UX_cash_ledger_idempotency ON dbo.cash_ledger(idempotency_key);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cash_ledger_hotel_date' AND object_id = OBJECT_ID('dbo.cash_ledger'))
    CREATE INDEX IX_cash_ledger_hotel_date
    ON dbo.cash_ledger(hotel_id, transaction_date)
    INCLUDE(debit_amount, credit_amount, source_type, source_subtype, source_no, source_zaaer_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cash_ledger_source' AND object_id = OBJECT_ID('dbo.cash_ledger'))
    CREATE INDEX IX_cash_ledger_source
    ON dbo.cash_ledger(hotel_id, source_type, source_id, source_zaaer_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cash_opening_balance_hotel_date' AND object_id = OBJECT_ID('dbo.cash_opening_balance'))
    CREATE INDEX IX_cash_opening_balance_hotel_date
    ON dbo.cash_opening_balance(hotel_id, opening_date DESC)
    INCLUDE(opening_amount);
GO
