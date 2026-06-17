-- =============================================
-- VoM Integration - Create Tables in MasterDB
-- تكامل VoM - إنشاء جداول في قاعدة البيانات المركزية
-- =============================================

USE db32357_MasterDB;
GO

-- =============================================
-- 1. إنشاء جدول vom_accounts
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vom_accounts')
BEGIN
    CREATE TABLE [dbo].[vom_accounts] (
        [id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [name_ar] NVARCHAR(200) NULL,
        [name_en] NVARCHAR(200) NULL,
        [code] NVARCHAR(50) NULL,
        [description] NVARCHAR(500) NULL,
        [used_in_payment] BIT NULL,
        [default_transaction_type] NVARCHAR(20) NULL,
        [subcategory_id] INT NULL,
        [status] INT NULL,
        [created_by] INT NULL,
        [updated_by] INT NULL,
        [is_main] INT NULL,
        [currency_code] NVARCHAR(10) NULL,
        [current_total_debit] DECIMAL(18,2) NULL,
        [current_total_credit] DECIMAL(18,2) NULL,
        [current_balance] DECIMAL(18,2) NULL,
        [created_at] DATETIME2 NULL,
        [updated_at] DATETIME2 NULL,
        [deleted_at] DATETIME2 NULL,
        [vom_id] INT NULL,
        [synced_at] DATETIME2 NULL
    );

    PRINT '✅ تم إنشاء جدول vom_accounts بنجاح';
END
ELSE
BEGIN
    PRINT '⚠️ جدول vom_accounts موجود بالفعل';
END
GO

-- =============================================
-- 2. إنشاء Index على code للبحث السريع
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_vom_accounts_code')
BEGIN
    CREATE INDEX IX_vom_accounts_code ON [dbo].[vom_accounts]([code]);
    PRINT '✅ تم إنشاء Index على code';
END
GO

-- =============================================
-- 3. إنشاء Index على vom_id للبحث السريع
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_vom_accounts_vom_id')
BEGIN
    CREATE INDEX IX_vom_accounts_vom_id ON [dbo].[vom_accounts]([vom_id]);
    PRINT '✅ تم إنشاء Index على vom_id';
END
GO

-- =============================================
-- 4. إنشاء جدول vom_accounting_sub_categories
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'vom_accounting_sub_categories')
BEGIN
    CREATE TABLE [dbo].[vom_accounting_sub_categories] (
        [id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [name_ar] NVARCHAR(200) NULL,
        [name_en] NVARCHAR(200) NULL,
        [code] NVARCHAR(50) NULL,
        [category_id] INT NULL,
        [created_at] DATETIME2 NULL,
        [updated_at] DATETIME2 NULL,
        [vom_id] INT NULL,
        [synced_at] DATETIME2 NULL
    );

    PRINT '✅ تم إنشاء جدول vom_accounting_sub_categories بنجاح';
END
ELSE
BEGIN
    PRINT '⚠️ جدول vom_accounting_sub_categories موجود بالفعل';
END
GO

-- =============================================
-- 5. إنشاء Index على code للبحث السريع
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_vom_accounting_sub_categories_code')
BEGIN
    CREATE INDEX IX_vom_accounting_sub_categories_code ON [dbo].[vom_accounting_sub_categories]([code]);
    PRINT '✅ تم إنشاء Index على code';
END
GO

-- =============================================
-- 6. إنشاء Index على vom_id للبحث السريع
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_vom_accounting_sub_categories_vom_id')
BEGIN
    CREATE INDEX IX_vom_accounting_sub_categories_vom_id ON [dbo].[vom_accounting_sub_categories]([vom_id]);
    PRINT '✅ تم إنشاء Index على vom_id';
END
GO

-- =============================================
-- 7. إنشاء Foreign Key Relationship (اختياري)
-- =============================================

-- IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_vom_accounts_subcategory')
-- BEGIN
--     ALTER TABLE [dbo].[vom_accounts]
--     ADD CONSTRAINT FK_vom_accounts_subcategory
--     FOREIGN KEY ([subcategory_id]) REFERENCES [dbo].[vom_accounting_sub_categories]([id]);
--     PRINT '✅ تم إنشاء Foreign Key';
-- END
-- GO

PRINT '✅ تم إنشاء جميع جداول VoM بنجاح في MasterDB';
GO
