-- =============================================
-- VoM Integration - Insert Chart of Accounts Children
-- تكامل VoM - إدراج الحسابات الفرعية في دليل الحسابات
-- =============================================
-- This script inserts child accounts into the chart_of_accounts table
-- following the hierarchical structure from the PDF: accounts_12-12-2025__11_16_21.pdf
-- =============================================

USE db32357_MasterDB;
GO

-- =============================================
-- Helper Function: Get Parent ID from Code
-- =============================================
-- This function determines the parent account ID based on the account code pattern
-- Code patterns:
--   Level 2: 3 digits (e.g., 011, 012) -> Parent is root (01, 02, etc.)
--   Level 3: Code with dash (e.g., 011-1) -> Parent is the 3-digit code (011)
--   Level 4+: Deeper nesting -> Parent is the code without the last segment

IF OBJECT_ID('dbo.GetParentAccountId', 'FN') IS NOT NULL
    DROP FUNCTION dbo.GetParentAccountId;
GO

CREATE FUNCTION dbo.GetParentAccountId(@AccountCode NVARCHAR(50))
RETURNS INT
AS
BEGIN
    DECLARE @ParentId INT = NULL;
    DECLARE @ParentCode NVARCHAR(50) = NULL;
    
    -- If code contains a dash, parent is the part before the last dash segment
    IF CHARINDEX('-', @AccountCode) > 0
    BEGIN
        -- Get parent code by removing the last segment after the last dash
        DECLARE @LastDashPos INT = LEN(@AccountCode) - CHARINDEX('-', REVERSE(@AccountCode)) + 1;
        SET @ParentCode = LEFT(@AccountCode, @LastDashPos - 1);
    END
    ELSE IF LEN(@AccountCode) = 3
    BEGIN
        -- Level 2 account: parent is the 2-digit root code
        SET @ParentCode = LEFT(@AccountCode, 2);
    END
    
    IF @ParentCode IS NOT NULL
    BEGIN
        SELECT @ParentId = id 
        FROM chart_of_accounts 
        WHERE code = @ParentCode;
    END
    
    RETURN @ParentId;
END
GO

-- =============================================
-- Helper Function: Calculate Account Level
-- =============================================
IF OBJECT_ID('dbo.CalculateAccountLevel', 'FN') IS NOT NULL
    DROP FUNCTION dbo.CalculateAccountLevel;
GO

CREATE FUNCTION dbo.CalculateAccountLevel(@AccountCode NVARCHAR(50))
RETURNS INT
AS
BEGIN
    DECLARE @Level INT = 1;
    
    -- Count the number of dashes + 1 to determine level
    -- Level 1: No dash, 2 digits (root)
    -- Level 2: No dash, 3 digits
    -- Level 3: One dash (e.g., 011-1)
    -- Level 4: Two dashes (e.g., 011-1-1)
    -- etc.
    
    IF LEN(@AccountCode) = 2
        SET @Level = 1; -- Root account
    ELSE IF LEN(@AccountCode) = 3 AND CHARINDEX('-', @AccountCode) = 0
        SET @Level = 2; -- Level 2 (3-digit code)
    ELSE
        SET @Level = 2 + (LEN(@AccountCode) - LEN(REPLACE(@AccountCode, '-', ''))); -- Level based on dashes
    
    RETURN @Level;
END
GO

-- =============================================
-- Helper Function: Build Account Path
-- =============================================
IF OBJECT_ID('dbo.BuildAccountPath', 'FN') IS NOT NULL
    DROP FUNCTION dbo.BuildAccountPath;
GO

CREATE FUNCTION dbo.BuildAccountPath(@AccountCode NVARCHAR(50))
RETURNS NVARCHAR(500)
AS
BEGIN
    DECLARE @Path NVARCHAR(500) = @AccountCode;
    DECLARE @ParentId INT = dbo.GetParentAccountId(@AccountCode);
    
    IF @ParentId IS NOT NULL
    BEGIN
        DECLARE @ParentPath NVARCHAR(500);
        SELECT @ParentPath = path 
        FROM chart_of_accounts 
        WHERE id = @ParentId;
        
        IF @ParentPath IS NOT NULL
            SET @Path = @ParentPath + '/' + @AccountCode;
    END
    
    RETURN @Path;
END
GO

-- =============================================
-- Stored Procedure: Insert Account with Auto-Calculated Fields
-- =============================================
IF OBJECT_ID('dbo.InsertChartAccount', 'P') IS NOT NULL
    DROP PROCEDURE dbo.InsertChartAccount;
GO

CREATE PROCEDURE dbo.InsertChartAccount
    @Code NVARCHAR(50),
    @NameAr NVARCHAR(200),
    @NameEn NVARCHAR(200),
    @AccountType NVARCHAR(50),
    @AccountClassification NVARCHAR(100) = NULL,
    @AccountSubClassification NVARCHAR(100) = NULL,
    @DefaultTransactionType NVARCHAR(20) = NULL,
    @IsMain BIT = 0,
    @Description NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Check if account already exists
    IF EXISTS (SELECT 1 FROM chart_of_accounts WHERE code = @Code)
    BEGIN
        PRINT '⚠️ Account ' + @Code + ' already exists. Skipping...';
        RETURN;
    END
    
    -- Calculate parent_id, level, and path
    DECLARE @ParentId INT = dbo.GetParentAccountId(@Code);
    DECLARE @Level INT = dbo.CalculateAccountLevel(@Code);
    DECLARE @Path NVARCHAR(500);
    
    -- Build path
    IF @ParentId IS NOT NULL
    BEGIN
        DECLARE @ParentPath NVARCHAR(500);
        SELECT @ParentPath = path FROM chart_of_accounts WHERE id = @ParentId;
        SET @Path = @ParentPath + '/' + @Code;
    END
    ELSE
    BEGIN
        SET @Path = @Code;
    END
    
    -- Determine default transaction type if not provided
    IF @DefaultTransactionType IS NULL
    BEGIN
        IF @AccountType IN ('Assets', 'Expenses')
            SET @DefaultTransactionType = 'debit';
        ELSE
            SET @DefaultTransactionType = 'credit';
    END
    
    -- Insert the account
    INSERT INTO chart_of_accounts 
        (code, name_ar, name_en, parent_id, level, path, account_type,
         account_classification, account_sub_classification, is_active, is_main, is_system,
         default_transaction_type, currency_code, description, created_at)
    VALUES 
        (@Code, @NameAr, @NameEn, @ParentId, @Level, @Path, @AccountType,
         @AccountClassification, @AccountSubClassification, 1, @IsMain, 0,
         @DefaultTransactionType, 'SAR', @Description, GETDATE());
    
    PRINT '✅ تم إدراج حساب ' + @Code + ' - ' + @NameAr + ' (' + @NameEn + ')';
END
GO

-- =============================================
-- INSERT CHILD ACCOUNTS FROM PDF
-- =============================================
-- Replace the examples below with actual data from accounts_12-12-2025__11_16_21.pdf
-- Format: EXEC dbo.InsertChartAccount @Code, @NameAr, @NameEn, @AccountType, @AccountClassification, @AccountSubClassification, @DefaultTransactionType, @IsMain, @Description

-- =============================================
-- LEVEL 2 ACCOUNTS (3-digit codes under root accounts)
-- =============================================

-- Assets (01) -> Level 2 Children
-- Example structure (REPLACE WITH ACTUAL DATA FROM PDF):
/*
EXEC dbo.InsertChartAccount 
    @Code = '011',
    @NameAr = N'الأصول المتداولة',
    @NameEn = 'Current Assets',
    @AccountType = 'Assets',
    @AccountClassification = 'Current Assets',
    @IsMain = 1;

EXEC dbo.InsertChartAccount 
    @Code = '012',
    @NameAr = N'الأصول الثابتة',
    @NameEn = 'Fixed Assets',
    @AccountType = 'Assets',
    @AccountClassification = 'Fixed Assets',
    @IsMain = 1;
*/

-- Liabilities (02) -> Level 2 Children
/*
EXEC dbo.InsertChartAccount 
    @Code = '021',
    @NameAr = N'الخصوم قصيرة الأجل',
    @NameEn = 'Short Term Liabilities',
    @AccountType = 'Liabilities',
    @AccountClassification = 'Short Term Liabilities',
    @IsMain = 1;

EXEC dbo.InsertChartAccount 
    @Code = '022',
    @NameAr = N'الخصوم طويلة الأجل',
    @NameEn = 'Long Term Liabilities',
    @AccountType = 'Liabilities',
    @AccountClassification = 'Long Term Liabilities',
    @IsMain = 1;
*/

-- Equity (03) -> Level 2 Children
-- TODO: Add from PDF

-- Revenue (04) -> Level 2 Children
-- TODO: Add from PDF

-- Expenses (05) -> Level 2 Children
-- TODO: Add from PDF

-- =============================================
-- LEVEL 3 ACCOUNTS (Codes with one dash, e.g., 011-1)
-- =============================================

-- Example (REPLACE WITH ACTUAL DATA FROM PDF):
/*
EXEC dbo.InsertChartAccount 
    @Code = '011-1',
    @NameAr = N'الاراضي',
    @NameEn = 'Lands',
    @AccountType = 'Assets',
    @AccountClassification = 'Current Assets',
    @AccountSubClassification = 'Fixed Assets',
    @IsMain = 0;

EXEC dbo.InsertChartAccount 
    @Code = '011-2',
    @NameAr = N'المباني',
    @NameEn = 'Building',
    @AccountType = 'Assets',
    @AccountClassification = 'Current Assets',
    @AccountSubClassification = 'Fixed Assets',
    @IsMain = 0;
*/

-- =============================================
-- LEVEL 4+ ACCOUNTS (Codes with multiple dashes, e.g., 011-1-1)
-- =============================================
-- Continue the pattern for deeper levels
-- TODO: Add from PDF

-- =============================================
-- CLEANUP: Drop helper functions and procedure after use (optional)
-- =============================================
-- Uncomment the following lines if you want to remove the helper functions after insertion:
/*
DROP PROCEDURE IF EXISTS dbo.InsertChartAccount;
DROP FUNCTION IF EXISTS dbo.BuildAccountPath;
DROP FUNCTION IF EXISTS dbo.CalculateAccountLevel;
DROP FUNCTION IF EXISTS dbo.GetParentAccountId;
GO
*/

-- =============================================
-- VERIFICATION QUERY
-- =============================================
SELECT 
    id,
    code,
    name_ar,
    name_en,
    parent_id,
    level,
    path,
    account_type,
    account_classification,
    account_sub_classification,
    is_active,
    is_main
FROM chart_of_accounts
ORDER BY 
    account_type,
    level,
    code;

PRINT '✅ تم إدراج الحسابات الفرعية بنجاح';
PRINT '📊 إجمالي عدد الحسابات: ' + CAST((SELECT COUNT(*) FROM chart_of_accounts) AS NVARCHAR(10));
GO
