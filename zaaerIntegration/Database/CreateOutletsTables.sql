-- =============================================
-- Migration Script: Create Outlets, OutletCategories, OutletItems, and OutletTables
-- =============================================
-- This script creates tables for managing outlets (المنافذ), categories, items, and tables
-- for the POS/Order management system.
--
-- IMPORTANT: Run this script on ALL tenant databases
-- =============================================

-- =============================================
-- Step 1: Create outlets table
-- =============================================
PRINT 'Step 1: Creating outlets table...';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[outlets]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[outlets](
        [outlet_id] INT IDENTITY(1,1) NOT NULL,
        [zaaer_id] INT NULL,
        [hotel_id] INT NOT NULL,
        [outlet_name] NVARCHAR(200) NOT NULL,
        [outlet_name_ar] NVARCHAR(200) NULL,
        [location] NVARCHAR(500) NULL,
        [image_url] NVARCHAR(500) NULL,
        [status] NVARCHAR(50) NOT NULL DEFAULT 'Open',
        [is_active] BIT NOT NULL DEFAULT 1,
        [created_by] INT NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [updated_at] DATETIME NULL,
        CONSTRAINT [PK_Outlets] PRIMARY KEY CLUSTERED ([outlet_id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
    
    PRINT 'Table [dbo].[outlets] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[outlets] already exists';
END
GO

-- =============================================
-- Step 2: Create outlet_categories table
-- =============================================
PRINT 'Step 2: Creating outlet_categories table...';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[outlet_categories]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[outlet_categories](
        [category_id] INT IDENTITY(1,1) NOT NULL,
        [zaaer_id] INT NULL,
        [hotel_id] INT NOT NULL,
        [category_name] NVARCHAR(200) NOT NULL,
        [category_name_ar] NVARCHAR(200) NULL,
        [description] NVARCHAR(1000) NULL,
        [sort_order] INT NOT NULL DEFAULT 0,
        [is_active] BIT NOT NULL DEFAULT 1,
        [created_by] INT NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [updated_at] DATETIME NULL,
        CONSTRAINT [PK_OutletCategories] PRIMARY KEY CLUSTERED ([category_id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
    
    PRINT 'Table [dbo].[outlet_categories] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[outlet_categories] already exists';
END
GO

-- =============================================
-- Step 3: Create outlet_items table
-- =============================================
PRINT 'Step 3: Creating outlet_items table...';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[outlet_items]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[outlet_items](
        [item_id] INT IDENTITY(1,1) NOT NULL,
        [zaaer_id] INT NULL,
        [hotel_id] INT NOT NULL,
        [outlet_id] INT NULL,
        [category_id] INT NULL,
        [item_code] NVARCHAR(50) NULL,
        [item_name] NVARCHAR(200) NOT NULL,
        [item_name_ar] NVARCHAR(200) NULL,
        [description] NVARCHAR(1000) NULL,
        [price] DECIMAL(12,2) NOT NULL DEFAULT 0,
        [quantity] INT NULL,
        [image_url] NVARCHAR(500) NULL,
        [includes_tax] BIT NOT NULL DEFAULT 0,
        [is_active] BIT NOT NULL DEFAULT 1,
        [created_by] INT NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [updated_at] DATETIME NULL,
        CONSTRAINT [PK_OutletItems] PRIMARY KEY CLUSTERED ([item_id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
    
    PRINT 'Table [dbo].[outlet_items] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[outlet_items] already exists';
END
GO

-- =============================================
-- Step 4: Create outlet_tables table
-- =============================================
PRINT 'Step 4: Creating outlet_tables table...';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[outlet_tables]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[outlet_tables](
        [table_id] INT IDENTITY(1,1) NOT NULL,
        [zaaer_id] INT NULL,
        [hotel_id] INT NOT NULL,
        [outlet_id] INT NULL,
        [table_name] NVARCHAR(200) NOT NULL,
        [table_name_ar] NVARCHAR(200) NULL,
        [description] NVARCHAR(1000) NULL,
        [capacity] INT NULL,
        [status] NVARCHAR(50) NOT NULL DEFAULT 'Available',
        [is_active] BIT NOT NULL DEFAULT 1,
        [created_by] INT NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [updated_at] DATETIME NULL,
        CONSTRAINT [PK_OutletTables] PRIMARY KEY CLUSTERED ([table_id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
    
    PRINT 'Table [dbo].[outlet_tables] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[outlet_tables] already exists';
END
GO

-- =============================================
-- Step 5: Create Indexes for performance
-- =============================================
PRINT 'Step 5: Creating indexes...';

-- Indexes on outlets table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Outlets_HotelId' AND object_id = OBJECT_ID('dbo.outlets'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Outlets_HotelId]
    ON [dbo].[outlets] ([hotel_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Outlets_HotelId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Outlets_HotelId] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Outlets_ZaaerId' AND object_id = OBJECT_ID('dbo.outlets'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Outlets_ZaaerId]
    ON [dbo].[outlets] ([zaaer_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Outlets_ZaaerId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Outlets_ZaaerId] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Outlets_Status' AND object_id = OBJECT_ID('dbo.outlets'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Outlets_Status]
    ON [dbo].[outlets] ([status] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_Outlets_Status] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_Outlets_Status] already exists';
END
GO

-- Indexes on outlet_categories table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OutletCategories_HotelId' AND object_id = OBJECT_ID('dbo.outlet_categories'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutletCategories_HotelId]
    ON [dbo].[outlet_categories] ([hotel_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OutletCategories_HotelId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OutletCategories_HotelId] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OutletCategories_ZaaerId' AND object_id = OBJECT_ID('dbo.outlet_categories'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutletCategories_ZaaerId]
    ON [dbo].[outlet_categories] ([zaaer_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OutletCategories_ZaaerId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OutletCategories_ZaaerId] already exists';
END
GO

-- Indexes on outlet_items table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OutletItems_HotelId' AND object_id = OBJECT_ID('dbo.outlet_items'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutletItems_HotelId]
    ON [dbo].[outlet_items] ([hotel_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OutletItems_HotelId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OutletItems_HotelId] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OutletItems_ZaaerId' AND object_id = OBJECT_ID('dbo.outlet_items'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutletItems_ZaaerId]
    ON [dbo].[outlet_items] ([zaaer_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OutletItems_ZaaerId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OutletItems_ZaaerId] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OutletItems_OutletId' AND object_id = OBJECT_ID('dbo.outlet_items'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutletItems_OutletId]
    ON [dbo].[outlet_items] ([outlet_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OutletItems_OutletId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OutletItems_OutletId] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OutletItems_CategoryId' AND object_id = OBJECT_ID('dbo.outlet_items'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutletItems_CategoryId]
    ON [dbo].[outlet_items] ([category_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OutletItems_CategoryId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OutletItems_CategoryId] already exists';
END
GO

-- Indexes on outlet_tables table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OutletTables_HotelId' AND object_id = OBJECT_ID('dbo.outlet_tables'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutletTables_HotelId]
    ON [dbo].[outlet_tables] ([hotel_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OutletTables_HotelId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OutletTables_HotelId] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OutletTables_ZaaerId' AND object_id = OBJECT_ID('dbo.outlet_tables'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutletTables_ZaaerId]
    ON [dbo].[outlet_tables] ([zaaer_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OutletTables_ZaaerId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OutletTables_ZaaerId] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OutletTables_OutletId' AND object_id = OBJECT_ID('dbo.outlet_tables'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OutletTables_OutletId]
    ON [dbo].[outlet_tables] ([outlet_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_OutletTables_OutletId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_OutletTables_OutletId] already exists';
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
PRINT '- Created outlets table';
PRINT '- Created outlet_categories table';
PRINT '- Created outlet_items table';
PRINT '- Created outlet_tables table';
PRINT '- Created all indexes for performance';
PRINT '';
PRINT 'IMPORTANT NOTES:';
PRINT '1. Foreign Key constraints are NOT created to avoid issues when receiving data from Zaaer API';
PRINT '2. All tables include zaaer_id field (after PK) for Zaaer system integration';
PRINT '3. outlet_id and table_id in orders table can reference these tables';
PRINT '4. item_id in order_items table can reference outlet_items table';
PRINT '5. category_id in outlet_items table can reference outlet_categories table';
PRINT '';

