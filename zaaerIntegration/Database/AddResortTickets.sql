IF COL_LENGTH('dbo.apartments', 'resort_area_type') IS NULL
BEGIN
    ALTER TABLE dbo.apartments ADD resort_area_type NVARCHAR(50) NULL;
END;
GO

IF COL_LENGTH('dbo.apartments', 'parent_apartment_id') IS NULL
BEGIN
    ALTER TABLE dbo.apartments ADD parent_apartment_id INT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_apartments_parent_apartment' AND object_id = OBJECT_ID('dbo.apartments'))
    CREATE INDEX IX_apartments_parent_apartment ON dbo.apartments(hotel_id, parent_apartment_id);
GO

IF OBJECT_ID('dbo.maintenances', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.maintenances', 'maintenance_categories') IS NULL
BEGIN
    ALTER TABLE dbo.maintenances ADD maintenance_categories NVARCHAR(200) NULL;
END;
GO

IF OBJECT_ID('dbo.resort_ticket_types', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.resort_ticket_types (
        ticket_type_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        code NVARCHAR(100) NOT NULL,
        name_ar NVARCHAR(200) NOT NULL,
        name_en NVARCHAR(200) NULL,
        description NVARCHAR(MAX) NULL,
        unit_price DECIMAL(12,2) NOT NULL CONSTRAINT DF_resort_ticket_types_unit_price DEFAULT(0),
        vat_rate DECIMAL(5,2) NOT NULL CONSTRAINT DF_resort_ticket_types_vat_rate DEFAULT(15),
        valid_for_hours INT NOT NULL CONSTRAINT DF_resort_ticket_types_valid DEFAULT(24),
        ticket_category NVARCHAR(50) NOT NULL CONSTRAINT DF_resort_ticket_types_category DEFAULT('other'),
        sort_order INT NOT NULL CONSTRAINT DF_resort_ticket_types_sort DEFAULT(0),
        is_generic BIT NOT NULL CONSTRAINT DF_resort_ticket_types_generic DEFAULT(0),
        is_active BIT NOT NULL CONSTRAINT DF_resort_ticket_types_active DEFAULT(1),
        zaaer_id INT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_resort_ticket_types_created DEFAULT(SYSDATETIME()),
        updated_at DATETIME2 NULL
    );
END;
GO

IF OBJECT_ID('dbo.resort_ticket_orders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.resort_ticket_orders (
        ticket_order_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        order_no NVARCHAR(50) NOT NULL,
        reservation_id INT NULL,
        unit_id INT NULL,
        customer_id INT NULL,
        invoice_id INT NULL,
        receipt_id INT NULL,
        order_date DATETIME2 NOT NULL CONSTRAINT DF_resort_ticket_orders_order_date DEFAULT(SYSDATETIME()),
        service_date DATE NOT NULL,
        subtotal DECIMAL(12,2) NOT NULL CONSTRAINT DF_resort_ticket_orders_subtotal DEFAULT(0),
        vat_amount DECIMAL(12,2) NOT NULL CONSTRAINT DF_resort_ticket_orders_vat DEFAULT(0),
        total_amount DECIMAL(12,2) NOT NULL CONSTRAINT DF_resort_ticket_orders_total DEFAULT(0),
        payment_status NVARCHAR(30) NOT NULL CONSTRAINT DF_resort_ticket_orders_payment DEFAULT('unpaid'),
        order_status NVARCHAR(30) NOT NULL CONSTRAINT DF_resort_ticket_orders_status DEFAULT('active'),
        notes NVARCHAR(MAX) NULL,
        created_by INT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_resort_ticket_orders_created DEFAULT(SYSDATETIME()),
        cancelled_by INT NULL,
        cancelled_at DATETIME2 NULL,
        cancel_reason NVARCHAR(MAX) NULL,
        zaaer_id INT NULL
    );
END;
GO

IF OBJECT_ID('dbo.resort_tickets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.resort_tickets (
        ticket_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        ticket_order_id INT NOT NULL,
        ticket_type_id INT NOT NULL,
        ticket_no NVARCHAR(120) NOT NULL,
        qr_code NVARCHAR(256) NOT NULL,
        ticket_status NVARCHAR(30) NOT NULL CONSTRAINT DF_resort_tickets_status DEFAULT('issued'),
        unit_price DECIMAL(12,2) NOT NULL CONSTRAINT DF_resort_tickets_unit_price DEFAULT(0),
        vat_amount DECIMAL(12,2) NOT NULL CONSTRAINT DF_resort_tickets_vat DEFAULT(0),
        total_amount DECIMAL(12,2) NOT NULL CONSTRAINT DF_resort_tickets_total DEFAULT(0),
        valid_from DATETIME2 NOT NULL,
        valid_to DATETIME2 NOT NULL,
        printed_at DATETIME2 NULL,
        used_at DATETIME2 NULL,
        cancelled_at DATETIME2 NULL,
        cancelled_by INT NULL,
        cancel_reason NVARCHAR(MAX) NULL,
        zaaer_id INT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_resort_tickets_created DEFAULT(SYSDATETIME())
    );
END;
GO

IF OBJECT_ID('dbo.resort_ticket_events', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.resort_ticket_events (
        event_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id INT NOT NULL,
        ticket_id INT NULL,
        ticket_order_id INT NULL,
        event_type NVARCHAR(50) NOT NULL,
        event_note NVARCHAR(MAX) NULL,
        created_by INT NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_resort_ticket_events_created DEFAULT(SYSDATETIME())
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_resort_ticket_types_hotel_code' AND object_id = OBJECT_ID('dbo.resort_ticket_types'))
    CREATE INDEX IX_resort_ticket_types_hotel_code ON dbo.resort_ticket_types(hotel_id, code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_resort_ticket_types_hotel_zaaer' AND object_id = OBJECT_ID('dbo.resort_ticket_types'))
    CREATE INDEX IX_resort_ticket_types_hotel_zaaer ON dbo.resort_ticket_types(hotel_id, zaaer_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_resort_ticket_orders_hotel_date' AND object_id = OBJECT_ID('dbo.resort_ticket_orders'))
    CREATE INDEX IX_resort_ticket_orders_hotel_date ON dbo.resort_ticket_orders(hotel_id, service_date, order_date);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_resort_ticket_orders_reservation' AND object_id = OBJECT_ID('dbo.resort_ticket_orders'))
    CREATE INDEX IX_resort_ticket_orders_reservation ON dbo.resort_ticket_orders(hotel_id, reservation_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_resort_tickets_order' AND object_id = OBJECT_ID('dbo.resort_tickets'))
    CREATE INDEX IX_resort_tickets_order ON dbo.resort_tickets(hotel_id, ticket_order_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_resort_tickets_qr' AND object_id = OBJECT_ID('dbo.resort_tickets'))
    CREATE INDEX IX_resort_tickets_qr ON dbo.resort_tickets(hotel_id, qr_code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_resort_ticket_events_order' AND object_id = OBJECT_ID('dbo.resort_ticket_events'))
    CREATE INDEX IX_resort_ticket_events_order ON dbo.resort_ticket_events(hotel_id, ticket_order_id, created_at);
GO
