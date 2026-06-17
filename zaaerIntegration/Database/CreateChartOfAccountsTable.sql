-- =============================================
-- VoM Integration - Create Chart of Accounts Table in MasterDB
-- تكامل VoM - إنشاء جدول دليل الحسابات في قاعدة البيانات المركزية
-- =============================================

USE db32357_MasterDB;
GO

-- =============================================
-- 1. إنشاء جدول chart_of_accounts
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'chart_of_accounts')
BEGIN
    CREATE TABLE [dbo].[chart_of_accounts] (
        [id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        
        -- Basic Information
        [code] NVARCHAR(50) NOT NULL UNIQUE,              -- كود الحساب (مثل: 01, 02, 03)
        [name_ar] NVARCHAR(200) NOT NULL,                 -- الاسم بالعربية
        [name_en] NVARCHAR(200) NOT NULL,                 -- الاسم بالإنجليزية
        
        -- Hierarchy
        [parent_id] INT NULL,                             -- الحساب الأب (للحسابات الفرعية)
        [level] INT NOT NULL DEFAULT 1,                   -- المستوى (1 = Root, 2 = Sub, etc.)
        [path] NVARCHAR(500) NULL,                        -- المسار الكامل (مثل: 01/011/011-1)
        
        -- Account Type (Root Level Categories)
        [account_type] NVARCHAR(50) NOT NULL,             -- نوع الحساب: Assets, Liabilities, Equity, Revenue, Expenses
        
        -- Account Classification
        [account_classification] NVARCHAR(100) NULL,      -- تصنيف الحساب (مثل: Current Assets, Fixed Assets)
        [account_sub_classification] NVARCHAR(100) NULL,  -- تصنيف فرعي
        
        -- VoM Integration
        [vom_account_code] NVARCHAR(50) NULL,             -- كود الحساب في VoM (للربط)
        [vom_account_id] INT NULL,                        -- ID الحساب في VoM
        
        -- Account Properties
        [is_active] BIT NOT NULL DEFAULT 1,               -- نشط/غير نشط
        [is_main] BIT NOT NULL DEFAULT 0,                 -- حساب رئيسي
        [is_system] BIT NOT NULL DEFAULT 0,               -- حساب نظام (لا يمكن حذفه)
        
        -- Default Settings
        [default_transaction_type] NVARCHAR(20) NULL,     -- debit أو credit (الافتراضي)
        [currency_code] NVARCHAR(10) NULL DEFAULT 'SAR',  -- العملة
        
        -- Metadata
        [description] NVARCHAR(500) NULL,                 -- وصف الحساب
        [notes] NVARCHAR(1000) NULL,                      -- ملاحظات
        
        -- Audit Fields
        [created_by] INT NULL,
        [created_at] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [updated_by] INT NULL,
        [updated_at] DATETIME2 NULL,
        [deleted_at] DATETIME2 NULL
    );

    PRINT '✅ تم إنشاء جدول chart_of_accounts بنجاح';
END
ELSE
BEGIN
    PRINT '⚠️ جدول chart_of_accounts موجود بالفعل';
END
GO

-- =============================================
-- 2. إنشاء Foreign Key على parent_id
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_chart_of_accounts_parent')
BEGIN
    ALTER TABLE [dbo].[chart_of_accounts]
    ADD CONSTRAINT FK_chart_of_accounts_parent
    FOREIGN KEY ([parent_id]) REFERENCES [dbo].[chart_of_accounts]([id]);
    
    PRINT '✅ تم إنشاء Foreign Key على parent_id';
END
ELSE
BEGIN
    PRINT '⚠️ Foreign Key موجود بالفعل';
END
GO

-- =============================================
-- 3. إنشاء Indexes
-- =============================================

-- Index على code
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_chart_of_accounts_code')
BEGIN
    CREATE UNIQUE INDEX IX_chart_of_accounts_code ON [dbo].[chart_of_accounts]([code]);
    PRINT '✅ تم إنشاء Index على code';
END
GO

-- Index على parent_id
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_chart_of_accounts_parent_id')
BEGIN
    CREATE INDEX IX_chart_of_accounts_parent_id ON [dbo].[chart_of_accounts]([parent_id]);
    PRINT '✅ تم إنشاء Index على parent_id';
END
GO

-- Index على account_type
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_chart_of_accounts_account_type')
BEGIN
    CREATE INDEX IX_chart_of_accounts_account_type ON [dbo].[chart_of_accounts]([account_type]);
    PRINT '✅ تم إنشاء Index على account_type';
END
GO

-- Index على level
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_chart_of_accounts_level')
BEGIN
    CREATE INDEX IX_chart_of_accounts_level ON [dbo].[chart_of_accounts]([level]);
    PRINT '✅ تم إنشاء Index على level';
END
GO

-- Index على vom_account_code
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_chart_of_accounts_vom_account_code')
BEGIN
    CREATE INDEX IX_chart_of_accounts_vom_account_code ON [dbo].[chart_of_accounts]([vom_account_code]);
    PRINT '✅ تم إنشاء Index على vom_account_code';
END
GO

-- =============================================
-- 4. إدراج المستويات الجذرية (Root Level Accounts)
-- =============================================

-- 1. الاصول (Assets)
IF NOT EXISTS (SELECT * FROM [dbo].[chart_of_accounts] WHERE code = '01')
BEGIN
    INSERT INTO [dbo].[chart_of_accounts] 
        ([code], [name_ar], [name_en], [parent_id], [level], [path], [account_type], [is_active], [is_main], [is_system], [default_transaction_type], [currency_code])
    VALUES 
        ('01', N'الاصول', 'Assets', NULL, 1, '01', 'Assets', 1, 1, 1, 'debit', 'SAR');
    PRINT '✅ تم إدراج حساب الاصول (Assets)';
END
GO

-- 2. الخصوم (Liabilities)
IF NOT EXISTS (SELECT * FROM [dbo].[chart_of_accounts] WHERE code = '02')
BEGIN
    INSERT INTO [dbo].[chart_of_accounts] 
        ([code], [name_ar], [name_en], [parent_id], [level], [path], [account_type], [is_active], [is_main], [is_system], [default_transaction_type], [currency_code])
    VALUES 
        ('02', N'الخصوم', 'Liabilities', NULL, 1, '02', 'Liabilities', 1, 1, 1, 'credit', 'SAR');
    PRINT '✅ تم إدراج حساب الخصوم (Liabilities)';
END
GO

-- 3. حقوق الملكية (Owner''s Equity)
IF NOT EXISTS (SELECT * FROM [dbo].[chart_of_accounts] WHERE code = '03')
BEGIN
    INSERT INTO [dbo].[chart_of_accounts] 
        ([code], [name_ar], [name_en], [parent_id], [level], [path], [account_type], [is_active], [is_main], [is_system], [default_transaction_type], [currency_code])
    VALUES 
        ('03', N'حقوق الملكية', 'Owner''s Equity', NULL, 1, '03', 'Equity', 1, 1, 1, 'credit', 'SAR');
    PRINT '✅ تم إدراج حساب حقوق الملكية (Owner''s Equity)';
END
GO

-- 4. الايرادات (Revenue)
IF NOT EXISTS (SELECT * FROM [dbo].[chart_of_accounts] WHERE code = '04')
BEGIN
    INSERT INTO [dbo].[chart_of_accounts] 
        ([code], [name_ar], [name_en], [parent_id], [level], [path], [account_type], [is_active], [is_main], [is_system], [default_transaction_type], [currency_code])
    VALUES 
        ('04', N'الايرادات', 'Revenue', NULL, 1, '04', 'Revenue', 1, 1, 1, 'credit', 'SAR');
    PRINT '✅ تم إدراج حساب الايرادات (Revenue)';
END
GO

-- 5. المصروفات (Expenses)
IF NOT EXISTS (SELECT * FROM [dbo].[chart_of_accounts] WHERE code = '05')
BEGIN
    INSERT INTO [dbo].[chart_of_accounts] 
        ([code], [name_ar], [name_en], [parent_id], [level], [path], [account_type], [is_active], [is_main], [is_system], [default_transaction_type], [currency_code])
    VALUES 
        ('05', N'المصروفات', 'Expenses', NULL, 1, '05', 'Expenses', 1, 1, 1, 'debit', 'SAR');
    PRINT '✅ تم إدراج حساب المصروفات (Expenses)';
END
GO

-- =============================================
-- 5. التحقق من البيانات
-- =============================================

SELECT 
    id,
    code,
    name_ar,
    name_en,
    account_type,
    level,
    is_active,
    is_system
FROM [dbo].[chart_of_accounts]
WHERE level = 1
ORDER BY code;

PRINT '✅ تم إنشاء جدول chart_of_accounts وإدراج المستويات الجذرية بنجاح';
GO
