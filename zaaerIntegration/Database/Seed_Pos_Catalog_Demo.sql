/*
  Seed POS catalog (outlets, categories, items, tables) — demo data from Zaaer-style POS screens.
  Run on EACH tenant database AFTER CreateOutletsTables.sql

  1) Set @HotelId to your hotel_settings.hotel_id, OR
  2) Set @HotelCode to match hotel_settings.hotel_code (e.g. N'Riyadh1')
*/
SET NOCOUNT ON;

DECLARE @HotelId INT = NULL;           -- e.g. 1
DECLARE @HotelCode NVARCHAR(50) = NULL; -- e.g. N'Dammam7'
DECLARE @CreatedBy INT = NULL;

IF @HotelId IS NULL AND @HotelCode IS NOT NULL
BEGIN
    SELECT @HotelId = hs.hotel_id
    FROM dbo.hotel_settings hs
    WHERE hs.hotel_code = @HotelCode;
END

IF @HotelId IS NULL
BEGIN
    SELECT TOP (1) @HotelId = hs.hotel_id
    FROM dbo.hotel_settings hs
    ORDER BY hs.hotel_id;
END

IF @HotelId IS NULL
BEGIN
    RAISERROR(N'Hotel not found. Set @HotelId or @HotelCode before running this script.', 16, 1);
    RETURN;
END

PRINT CONCAT(N'Using hotel_id = ', @HotelId);

DECLARE @Now DATETIME = GETDATE();

/* ===================== OUTLETS ===================== */
IF NOT EXISTS (SELECT 1 FROM dbo.outlets o WHERE o.hotel_id = @HotelId AND o.outlet_name = N'Mini Fridge')
BEGIN
    INSERT INTO dbo.outlets (hotel_id, outlet_name, outlet_name_ar, location, status, is_active, created_by, created_at)
    VALUES (@HotelId, N'Mini Fridge', N'الثلاجة', N'الاستقبال', N'Open', 1, @CreatedBy, @Now);
END

IF NOT EXISTS (SELECT 1 FROM dbo.outlets o WHERE o.hotel_id = @HotelId AND o.outlet_name = N'Cafeteria')
BEGIN
    INSERT INTO dbo.outlets (hotel_id, outlet_name, outlet_name_ar, location, status, is_active, created_by, created_at)
    VALUES (@HotelId, N'Cafeteria', N'الكافتيريا', N'الاستقبال', N'Open', 1, @CreatedBy, @Now);
END

IF NOT EXISTS (SELECT 1 FROM dbo.outlets o WHERE o.hotel_id = @HotelId AND o.outlet_name = N'Services')
BEGIN
    INSERT INTO dbo.outlets (hotel_id, outlet_name, outlet_name_ar, location, status, is_active, created_by, created_at)
    VALUES (@HotelId, N'Services', N'خدمات', N'المستودع', N'Open', 1, @CreatedBy, @Now);
END

IF NOT EXISTS (SELECT 1 FROM dbo.outlets o WHERE o.hotel_id = @HotelId AND o.outlet_name = N'Other Revenue')
BEGIN
    INSERT INTO dbo.outlets (hotel_id, outlet_name, outlet_name_ar, location, status, is_active, created_by, created_at)
    VALUES (@HotelId, N'Other Revenue', N'إيرادات أخرى', N'الاستقبال', N'Open', 1, @CreatedBy, @Now);
END

DECLARE @OutletFridge INT = (SELECT outlet_id FROM dbo.outlets WHERE hotel_id = @HotelId AND outlet_name = N'Mini Fridge');
DECLARE @OutletCafe INT = (SELECT outlet_id FROM dbo.outlets WHERE hotel_id = @HotelId AND outlet_name = N'Cafeteria');
DECLARE @OutletServices INT = (SELECT outlet_id FROM dbo.outlets WHERE hotel_id = @HotelId AND outlet_name = N'Services');
DECLARE @OutletOther INT = (SELECT outlet_id FROM dbo.outlets WHERE hotel_id = @HotelId AND outlet_name = N'Other Revenue');

/* ===================== CATEGORIES ===================== */
IF NOT EXISTS (SELECT 1 FROM dbo.outlet_categories c WHERE c.hotel_id = @HotelId AND c.category_name = N'Tea / Coffee')
BEGIN
    INSERT INTO dbo.outlet_categories (hotel_id, category_name, category_name_ar, sort_order, is_active, created_by, created_at)
    VALUES (@HotelId, N'Tea / Coffee', N'شاي / قهوة', 10, 1, @CreatedBy, @Now);
END

IF NOT EXISTS (SELECT 1 FROM dbo.outlet_categories c WHERE c.hotel_id = @HotelId AND c.category_name = N'Hot Drinks')
BEGIN
    INSERT INTO dbo.outlet_categories (hotel_id, category_name, category_name_ar, sort_order, is_active, created_by, created_at)
    VALUES (@HotelId, N'Hot Drinks', N'مشروبات ساخنة', 20, 1, @CreatedBy, @Now);
END

IF NOT EXISTS (SELECT 1 FROM dbo.outlet_categories c WHERE c.hotel_id = @HotelId AND c.category_name = N'Cold Drinks')
BEGIN
    INSERT INTO dbo.outlet_categories (hotel_id, category_name, category_name_ar, sort_order, is_active, created_by, created_at)
    VALUES (@HotelId, N'Cold Drinks', N'مشروبات باردة', 30, 1, @CreatedBy, @Now);
END

IF NOT EXISTS (SELECT 1 FROM dbo.outlet_categories c WHERE c.hotel_id = @HotelId AND c.category_name = N'Chocolate')
BEGIN
    INSERT INTO dbo.outlet_categories (hotel_id, category_name, category_name_ar, sort_order, is_active, created_by, created_at)
    VALUES (@HotelId, N'Chocolate', N'شوكولاتة', 40, 1, @CreatedBy, @Now);
END

IF NOT EXISTS (SELECT 1 FROM dbo.outlet_categories c WHERE c.hotel_id = @HotelId AND c.category_name = N'Other Revenue')
BEGIN
    INSERT INTO dbo.outlet_categories (hotel_id, category_name, category_name_ar, sort_order, is_active, created_by, created_at)
    VALUES (@HotelId, N'Other Revenue', N'إيرادات أخرى', 50, 1, @CreatedBy, @Now);
END

DECLARE @CatTea INT = (SELECT category_id FROM dbo.outlet_categories WHERE hotel_id = @HotelId AND category_name = N'Tea / Coffee');
DECLARE @CatHot INT = (SELECT category_id FROM dbo.outlet_categories WHERE hotel_id = @HotelId AND category_name = N'Hot Drinks');
DECLARE @CatCold INT = (SELECT category_id FROM dbo.outlet_categories WHERE hotel_id = @HotelId AND category_name = N'Cold Drinks');
DECLARE @CatChoco INT = (SELECT category_id FROM dbo.outlet_categories WHERE hotel_id = @HotelId AND category_name = N'Chocolate');
DECLARE @CatOther INT = (SELECT category_id FROM dbo.outlet_categories WHERE hotel_id = @HotelId AND category_name = N'Other Revenue');

/* ===================== ITEMS (helper) ===================== */
DECLARE @Items TABLE (
    item_code NVARCHAR(50) NOT NULL PRIMARY KEY,
    outlet_id INT NULL,
    category_id INT NULL,
    item_name NVARCHAR(200) NOT NULL,
    item_name_ar NVARCHAR(200) NULL,
    price DECIMAL(12,2) NOT NULL,
    quantity INT NULL,
    includes_tax BIT NOT NULL DEFAULT 0
);

INSERT INTO @Items (item_code, outlet_id, category_id, item_name, item_name_ar, price, quantity, includes_tax)
VALUES
    /* Tea / Coffee — الكافتيريا / الثلاجة */
    /* Optional: add image_url when inserting, e.g. N'/images/pos/tea001.png' or full HTTPS URL */
    (N'TEA001', @OutletCafe, @CatTea, N'Coffee pot', N'دلة قهوة', 15.00, 100, 0),
    (N'TEA002', @OutletCafe, @CatTea, N'Karak tea', N'شاي كرك', 15.00, 100, 0),
    (N'TEA003', @OutletCafe, @CatTea, N'Tea pot', N'براد شاي', 20.00, 100, 0),
    (N'TEA004', @OutletCafe, @CatTea, N'Arabic coffee', N'قهوة عربية', 12.00, 100, 0),

    /* Hot drinks */
    (N'HOT001', @OutletCafe, @CatHot, N'Cappuccino', N'كابتشينو', 10.00, 100, 0),
    (N'HOT002', @OutletCafe, @CatHot, N'Hot chocolate', N'هوت شوكليت', 12.00, 100, 0),
    (N'HOT003', @OutletCafe, @CatHot, N'Black tea', N'شاي أحمر', 5.00, 100, 0),
    (N'HOT004', @OutletCafe, @CatHot, N'Nescafe', N'نسكافيه', 8.00, 100, 0),
    (N'HOT005', @OutletCafe, @CatHot, N'Latte', N'لاتيه', 14.00, 100, 0),

    /* Cold drinks */
    (N'COLD001', @OutletCafe, @CatCold, N'Pepsi', N'بيبسي', 5.00, 200, 0),
    (N'COLD002', @OutletCafe, @CatCold, N'Coca Cola', N'كوكا كولا', 5.00, 200, 0),
    (N'COLD003', @OutletCafe, @CatCold, N'Mirinda', N'ميرندا', 5.00, 200, 0),
    (N'COLD004', @OutletCafe, @CatCold, N'Al Rabie juice', N'عصير الربيع', 6.00, 150, 0),
    (N'COLD005', @OutletCafe, @CatCold, N'Mineral water', N'مياه معدنية', 2.00, 300, 0),

    /* Chocolate / snacks */
    (N'CHO001', @OutletCafe, @CatChoco, N'Kit Kat', N'كيت كات', 3.00, 80, 0),
    (N'CHO002', @OutletCafe, @CatChoco, N'Galaxy chocolate', N'شوكولاتة جالكسي', 4.00, 80, 0),
    (N'CHO003', @OutletCafe, @CatChoco, N'Snickers', N'سنيكرز', 4.00, 80, 0),
    (N'CHO004', @OutletCafe, @CatChoco, N'Twix', N'تويكس', 4.00, 80, 0),
    (N'CHO005', @OutletCafe, @CatChoco, N'Mars', N'مارس', 4.00, 80, 0),

    /* Other revenue */
    (N'OTH001', @OutletOther, @CatOther, N'Rice with fish', N'أرز مع سمك', 30.00, NULL, 0),
    (N'OTH002', @OutletOther, @CatOther, N'Laundry service', N'غسيل', 25.00, NULL, 0),
    (N'OTH003', @OutletOther, @CatOther, N'Room service fee', N'خدمة غرف', 15.00, NULL, 0),
    (N'OTH004', @OutletServices, @CatOther, N'Misc service', N'خدمة متنوعة', 10.00, NULL, 0),

    /* Mini fridge (cold + chocolate) */
    (N'FRG001', @OutletFridge, @CatCold, N'Mineral water', N'مياه معدنية', 2.00, 50, 0),
    (N'FRG002', @OutletFridge, @CatCold, N'Pepsi', N'بيبسي', 5.00, 40, 0),
    (N'FRG003', @OutletFridge, @CatChoco, N'Kit Kat', N'كيت كات', 3.00, 30, 0);

INSERT INTO dbo.outlet_items (
    hotel_id, outlet_id, category_id, item_code, item_name, item_name_ar,
    price, quantity, includes_tax, is_active, created_by, created_at
)
SELECT
    @HotelId,
    s.outlet_id,
    s.category_id,
    s.item_code,
    s.item_name,
    s.item_name_ar,
    s.price,
    s.quantity,
    s.includes_tax,
    1,
    @CreatedBy,
    @Now
FROM @Items s
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.outlet_items i
    WHERE i.hotel_id = @HotelId AND i.item_code = s.item_code
);

/* ===================== TABLES (الكافتيريا) ===================== */
DECLARE @Tables TABLE (table_name NVARCHAR(200), table_name_ar NVARCHAR(200), capacity INT, sort_no INT);
INSERT INTO @Tables VALUES
    (N'Table 1', N'طاولة 1', 4, 1),
    (N'Table 2', N'طاولة 2', 4, 2),
    (N'Table 3', N'طاولة 3', 6, 3),
    (N'Table 4', N'طاولة 4', 2, 4),
    (N'Table 5', N'طاولة 5', 8, 5);

INSERT INTO dbo.outlet_tables (
    hotel_id, outlet_id, table_name, table_name_ar, capacity, status, is_active, created_by, created_at
)
SELECT
    @HotelId,
    @OutletCafe,
    t.table_name,
    t.table_name_ar,
    t.capacity,
    N'Available',
    1,
    @CreatedBy,
    @Now
FROM @Tables t
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.outlet_tables ot
    WHERE ot.hotel_id = @HotelId
      AND ot.outlet_id = @OutletCafe
      AND ot.table_name = t.table_name
);

/* ===================== SUMMARY ===================== */
SELECT N'outlets' AS entity, COUNT(*) AS cnt FROM dbo.outlets WHERE hotel_id = @HotelId
UNION ALL
SELECT N'categories', COUNT(*) FROM dbo.outlet_categories WHERE hotel_id = @HotelId
UNION ALL
SELECT N'items', COUNT(*) FROM dbo.outlet_items WHERE hotel_id = @HotelId
UNION ALL
SELECT N'tables', COUNT(*) FROM dbo.outlet_tables WHERE hotel_id = @HotelId;

PRINT N'POS catalog seed completed.';

/*
  Example — set product images (put files under wwwroot/images/pos/ on the server):

  UPDATE dbo.outlet_items SET image_url = N'/images/pos/pepsi.png'
  WHERE hotel_id = @HotelId AND item_code = N'COLD001';

  UPDATE dbo.outlet_items SET image_url = N'https://cdn.example.com/pos/cappuccino.jpg'
  WHERE hotel_id = @HotelId AND item_code = N'HOT001';
*/
GO
