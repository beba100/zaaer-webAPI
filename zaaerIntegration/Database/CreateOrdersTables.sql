-- =============================================
-- Migration Script: Create Orders and OrderItems Tables
-- =============================================
-- This script creates the orders and order_items tables and adds order_id
-- foreign key columns to payment_receipts, invoices, and credit_notes tables.
-- It also adds revenue_category columns to invoices and payment_receipts
-- for VoM integration (إيرادات أخرى).
--
-- IMPORTANT: Run this script on ALL tenant databases
-- =============================================

-- =============================================
-- Step 1: Create orders table
-- =============================================
PRINT 'Step 1: Creating orders table...';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[orders]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[orders](
        [order_id] INT IDENTITY(1,1) NOT NULL,
        [zaaer_id] INT NULL,
        [order_no] NVARCHAR(50) NOT NULL,
        [hotel_id] INT NOT NULL,
        [outlet_id] INT NULL,
        [table_id] INT NULL,
        [customer_id] INT NULL,
        [reservation_id] INT NULL,
        [order_date] DATETIME NOT NULL DEFAULT GETDATE(),
        [order_time] NVARCHAR(20) NULL,
        [order_status] NVARCHAR(50) NOT NULL DEFAULT 'Created',
        [payment_status] NVARCHAR(50) NOT NULL DEFAULT 'Unpaid',
        [order_type] NVARCHAR(50) NOT NULL DEFAULT 'InPlace',
        [subtotal] DECIMAL(12,2) NULL,
        [tax_amount] DECIMAL(12,2) NULL,
        [discount_amount] DECIMAL(12,2) NULL,
        [total_amount] DECIMAL(12,2) NULL,
        [paid_amount] DECIMAL(12,2) NOT NULL DEFAULT 0,
        [balance] DECIMAL(12,2) NULL,
        [target] NVARCHAR(500) NULL,
        [notes] NVARCHAR(1000) NULL,
        [cancellation_date] DATETIME NULL,
        [cancellation_reason] NVARCHAR(500) NULL,
        [is_refunded] BIT NOT NULL DEFAULT 0,
        [created_by] INT NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [updated_at] DATETIME NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([order_id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
    
    PRINT 'Table [dbo].[orders] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[orders] already exists';
END
GO

-- =============================================
-- Step 2: Create order_items table
-- =============================================
PRINT 'Step 2: Creating order_items table...';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[order_items]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[order_items](
        [order_item_id] INT IDENTITY(1,1) NOT NULL,
        [zaaer_id] INT NULL,
        [order_id] INT NOT NULL,
        [item_id] INT NULL,
        [item_name] NVARCHAR(200) NOT NULL,
        [quantity] INT NOT NULL,
        [unit_price] DECIMAL(12,2) NOT NULL,
        [discount] DECIMAL(12,2) NOT NULL DEFAULT 0,
        [total_price] DECIMAL(12,2) NOT NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_OrderItems] PRIMARY KEY CLUSTERED ([order_item_id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
    
    PRINT 'Table [dbo].[order_items] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[order_items] already exists';
END
GO

-- =============================================
-- Step 3: Add order_id to payment_receipts table
-- =============================================
PRINT 'Step 3: Adding order_id to payment_receipts table...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.payment_receipts') AND name = 'order_id')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [order_id] INT NULL;
    PRINT 'Column [order_id] added to [dbo].[payment_receipts]';
END
ELSE
BEGIN
    PRINT 'Column [order_id] already exists in [dbo].[payment_receipts]';
END
GO

-- =============================================
-- Step 4: Add order_id to invoices table
-- =============================================
PRINT 'Step 4: Adding order_id to invoices table...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.invoices') AND name = 'order_id')
BEGIN
    ALTER TABLE [dbo].[invoices]
    ADD [order_id] INT NULL;
    PRINT 'Column [order_id] added to [dbo].[invoices]';
END
ELSE
BEGIN
    PRINT 'Column [order_id] already exists in [dbo].[invoices]';
END
GO

-- =============================================
-- Step 5: Add order_id to credit_notes table
-- =============================================
PRINT 'Step 5: Adding order_id to credit_notes table...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.credit_notes') AND name = 'order_id')
BEGIN
    ALTER TABLE [dbo].[credit_notes]
    ADD [order_id] INT NULL;
    PRINT 'Column [order_id] added to [dbo].[credit_notes]';
END
ELSE
BEGIN
    PRINT 'Column [order_id] already exists in [dbo].[credit_notes]';
END
GO

-- =============================================
-- Step 6: Add revenue_category to invoices table
-- =============================================
PRINT 'Step 6: Adding revenue_category to invoices table...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.invoices') AND name = 'revenue_category')
BEGIN
    ALTER TABLE [dbo].[invoices]
    ADD [revenue_category] NVARCHAR(50) NULL;
    PRINT 'Column [revenue_category] added to [dbo].[invoices]';
END
ELSE
BEGIN
    PRINT 'Column [revenue_category] already exists in [dbo].[invoices]';
END
GO

-- =============================================
-- Step 7: Add revenue_category to payment_receipts table
-- =============================================
PRINT 'Step 7: Adding revenue_category to payment_receipts table...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.payment_receipts') AND name = 'revenue_category')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [revenue_category] NVARCHAR(50) NULL;
    PRINT 'Column [revenue_category] added to [dbo].[payment_receipts]';
END
ELSE
BEGIN
    PRINT 'Column [revenue_category] already exists in [dbo].[payment_receipts]';
END
GO

-- =============================================
-- Step 8: Create Indexes for performance
-- NOTE: Foreign Key constraints are NOT created to avoid issues
-- when receiving data from Zaaer API (data may arrive out of order)
-- =============================================
PRINT 'Step 8: Creating indexes...';

-- Index on orders.order_no
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_OrderNo' AND object_id = OBJECT_ID('dbo.orders'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Orders_OrderNo]
    ON [dbo].[orders] ([order_no] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Orders_OrderNo] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Orders_OrderNo] already exists';
END
GO

-- Index on orders.hotel_id
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_HotelId' AND object_id = OBJECT_ID('dbo.orders'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Orders_HotelId]
    ON [dbo].[orders] ([hotel_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Orders_HotelId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Orders_HotelId] already exists';
END
GO

-- Index on orders.customer_id
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_CustomerId' AND object_id = OBJECT_ID('dbo.orders'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Orders_CustomerId]
    ON [dbo].[orders] ([customer_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Orders_CustomerId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Orders_CustomerId] already exists';
END
GO

-- Index on orders.order_status
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_OrderStatus' AND object_id = OBJECT_ID('dbo.orders'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Orders_OrderStatus]
    ON [dbo].[orders] ([order_status] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Orders_OrderStatus] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Orders_OrderStatus] already exists';
END
GO


-- Index on orders.zaaer_id
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_ZaaerId' AND object_id = OBJECT_ID('dbo.orders'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Orders_ZaaerId]
    ON [dbo].[orders] ([zaaer_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Orders_ZaaerId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Orders_ZaaerId] already exists';
END
GO

-- Index on order_items.order_id
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrderItems_OrderId' AND object_id = OBJECT_ID('dbo.order_items'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrderItems_OrderId]
    ON [dbo].[order_items] ([order_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OrderItems_OrderId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OrderItems_OrderId] already exists';
END
GO

-- Index on order_items.zaaer_id
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrderItems_ZaaerId' AND object_id = OBJECT_ID('dbo.order_items'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrderItems_ZaaerId]
    ON [dbo].[order_items] ([zaaer_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OrderItems_ZaaerId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OrderItems_ZaaerId] already exists';
END
GO

-- Index on payment_receipts.order_id
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PaymentReceipts_OrderId' AND object_id = OBJECT_ID('dbo.payment_receipts'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceipts_OrderId]
    ON [dbo].[payment_receipts] ([order_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_PaymentReceipts_OrderId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_PaymentReceipts_OrderId] already exists';
END
GO

-- Index on invoices.order_id
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Invoices_OrderId' AND object_id = OBJECT_ID('dbo.invoices'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoices_OrderId]
    ON [dbo].[invoices] ([order_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Invoices_OrderId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Invoices_OrderId] already exists';
END
GO

-- Index on credit_notes.order_id
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CreditNotes_OrderId' AND object_id = OBJECT_ID('dbo.credit_notes'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CreditNotes_OrderId]
    ON [dbo].[credit_notes] ([order_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_CreditNotes_OrderId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_CreditNotes_OrderId] already exists';
END
GO

-- Index on invoices.revenue_category
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Invoices_RevenueCategory' AND object_id = OBJECT_ID('dbo.invoices'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoices_RevenueCategory]
    ON [dbo].[invoices] ([revenue_category] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Invoices_RevenueCategory] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Invoices_RevenueCategory] already exists';
END
GO

-- Index on payment_receipts.revenue_category
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PaymentReceipts_RevenueCategory' AND object_id = OBJECT_ID('dbo.payment_receipts'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceipts_RevenueCategory]
    ON [dbo].[payment_receipts] ([revenue_category] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_PaymentReceipts_RevenueCategory] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_PaymentReceipts_RevenueCategory] already exists';
END
GO

-- =============================================
-- Step 8: Make customer_id nullable in orders, invoices, and payment_receipts tables (if tables already exist)
-- =============================================
PRINT 'Step 8: Making customer_id nullable in orders, invoices, and payment_receipts tables (if needed)...';

-- Make customer_id nullable in orders table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[orders]') AND type in (N'U'))
BEGIN
    IF EXISTS (
        SELECT * 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID('dbo.orders') 
        AND name = 'customer_id' 
        AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE [dbo].[orders]
        ALTER COLUMN [customer_id] INT NULL;
        PRINT 'Column [customer_id] in [dbo].[orders] is now nullable';
    END
    ELSE
    BEGIN
        PRINT 'Column [customer_id] in [dbo].[orders] is already nullable';
    END
END
ELSE
BEGIN
    PRINT 'Table [dbo].[orders] does not exist yet - will be created with nullable customer_id';
END
GO

-- Make customer_id nullable in invoices table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[invoices]') AND type in (N'U'))
BEGIN
    IF EXISTS (
        SELECT * 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID('dbo.invoices') 
        AND name = 'customer_id' 
        AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE [dbo].[invoices]
        ALTER COLUMN [customer_id] INT NULL;
        PRINT 'Column [customer_id] in [dbo].[invoices] is now nullable';
    END
    ELSE
    BEGIN
        PRINT 'Column [customer_id] in [dbo].[invoices] is already nullable';
    END
END
ELSE
BEGIN
    PRINT 'Table [dbo].[invoices] does not exist yet';
END
GO

-- Make customer_id nullable in payment_receipts table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[payment_receipts]') AND type in (N'U'))
BEGIN
    IF EXISTS (
        SELECT * 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID('dbo.payment_receipts') 
        AND name = 'customer_id' 
        AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE [dbo].[payment_receipts]
        ALTER COLUMN [customer_id] INT NULL;
        PRINT 'Column [customer_id] in [dbo].[payment_receipts] is now nullable';
    END
    ELSE
    BEGIN
        PRINT 'Column [customer_id] in [dbo].[payment_receipts] is already nullable';
    END
END
ELSE
BEGIN
    PRINT 'Table [dbo].[payment_receipts] does not exist yet';
END
GO

-- =============================================
-- Migration Complete
-- =============================================
PRINT '';
PRINT '=============================================';
PRINT 'Migration completed successfully!';
PRINT '=============================================';
PRINT '';
PRINT 'Summary:';
PRINT '- Created orders table';
PRINT '- Created order_items table';
PRINT '- Added order_id to payment_receipts';
PRINT '- Added order_id to invoices';
PRINT '- Added order_id to credit_notes';
PRINT '- Added revenue_category to invoices';
PRINT '- Added revenue_category to payment_receipts';
PRINT '- Created all indexes for performance';
PRINT '';
PRINT 'IMPORTANT NOTES:';
PRINT '1. Foreign Key constraints are NOT created to avoid issues when receiving data from Zaaer API';
PRINT '2. revenue_category is added ONLY to invoices and payment_receipts tables (NOT in orders)';
PRINT '3. When invoice/receipt is created from order, set revenue_category = "OtherRevenue" automatically in C#';
PRINT '4. To identify orders-related transactions: check if order_id IS NOT NULL in invoices/payment_receipts';
PRINT '5. revenue_category in invoices/payment_receipts will be used for VoM "إيرادات أخرى" account';
PRINT '6. Zaaer system does NOT send revenue_category - it will be determined locally in our C# code';
PRINT '';

