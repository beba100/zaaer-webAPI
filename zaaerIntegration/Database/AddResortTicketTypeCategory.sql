-- Idempotent: ticket category columns + sample seed for resort ticket types.
-- Run on tenant DBs that already have resort_ticket_types.

IF OBJECT_ID('dbo.resort_ticket_types', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.resort_ticket_types', 'ticket_category') IS NULL
        ALTER TABLE dbo.resort_ticket_types ADD ticket_category NVARCHAR(50) NOT NULL
            CONSTRAINT DF_resort_ticket_types_category DEFAULT('other');

    IF COL_LENGTH('dbo.resort_ticket_types', 'sort_order') IS NULL
        ALTER TABLE dbo.resort_ticket_types ADD sort_order INT NOT NULL
            CONSTRAINT DF_resort_ticket_types_sort DEFAULT(0);

    IF COL_LENGTH('dbo.resort_ticket_types', 'is_generic') IS NULL
        ALTER TABLE dbo.resort_ticket_types ADD is_generic BIT NOT NULL
            CONSTRAINT DF_resort_ticket_types_generic DEFAULT(0);
END;
GO

-- Backfill category from code prefix when still 'other'
IF OBJECT_ID('dbo.resort_ticket_types', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.resort_ticket_types', 'ticket_category') IS NOT NULL
BEGIN
    UPDATE dbo.resort_ticket_types
    SET ticket_category = CASE
        WHEN code LIKE 'entry%' OR code LIKE '%entry%' THEN 'entry'
        WHEN code LIKE 'game%' OR code LIKE 'games%' THEN 'games'
        WHEN code LIKE 'pool%' THEN 'pool'
        ELSE ticket_category
    END
    WHERE ticket_category = 'other'
      AND (code LIKE 'entry%' OR code LIKE '%entry%' OR code LIKE 'game%' OR code LIKE 'games%' OR code LIKE 'pool%');
END;
GO

-- Sample seed (per hotel, only when no types exist for that hotel)
IF OBJECT_ID('dbo.resort_ticket_types', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.resort_ticket_types (hotel_id, code, name_ar, name_en, unit_price, vat_rate, valid_for_hours, ticket_category, sort_order, is_generic, is_active, created_at)
    SELECT h.hotel_id, v.code, v.name_ar, v.name_en, v.unit_price, 15, v.valid_for_hours, v.ticket_category, v.sort_order, v.is_generic, 1, SYSDATETIME()
    FROM dbo.hotel_settings h
    CROSS APPLY (VALUES
        ('entry_adult', N'تذكرة دخول', 'Entry ticket', 50.00, 24, 'entry', 10, 0),
        ('games_pass', N'تذكرة ألعاب عامة', 'Games pass', 80.00, 24, 'games', 10, 1),
        ('game_bumper', N'سيارات تصادم', 'Bumper cars', 35.00, 2, 'games', 20, 0),
        ('game_trampoline', N'ترامبولين', 'Trampoline', 30.00, 1, 'games', 30, 0),
        ('pool_day', N'تذكرة مسبح يوم', 'Pool day pass', 40.00, 8, 'pool', 10, 0)
    ) v(code, name_ar, name_en, unit_price, valid_for_hours, ticket_category, sort_order, is_generic)
    WHERE LOWER(ISNULL(h.property_type, 'hotel')) = 'resort'
      AND NOT EXISTS (SELECT 1 FROM dbo.resort_ticket_types t WHERE t.hotel_id = h.hotel_id);
END;
GO
