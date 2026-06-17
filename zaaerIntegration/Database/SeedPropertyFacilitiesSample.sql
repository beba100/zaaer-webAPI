/*
Sample facilities for tenant DB (run on hotel tenant database).
Set @HotelScopeId to hotel_settings.zaaer_id for the property (e.g. 21 for Jizan3).
zaaer_id starts at 0 per hotel scope.
*/

SET NOCOUNT ON;

DECLARE @HotelScopeId INT = 21; -- CHANGE: hotel_settings.zaaer_id (not internal hotel_id)

IF COL_LENGTH(N'dbo.facilities', N'facility_name_en') IS NULL
BEGIN
    ALTER TABLE dbo.facilities ADD facility_name_en NVARCHAR(200) NULL;
    PRINT N'Added facilities.facility_name_en';
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'facilities' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    RAISERROR(N'Table dbo.facilities does not exist. Run AddPmsPropertySettings.sql first.', 16, 1);
    RETURN;
END

;WITH seed AS (
    SELECT *
    FROM (VALUES
        (0,  N'صالة استقبال',      N'Reception lobby',       N'منطقة استقبال الضيوف والانتظار'),
        (1,  N'مصعد',              N'Elevator',              N'مصعد للنزلاء والموظفين'),
        (2,  N'موقف سيارات',       N'Parking',               N'مواقف مخصصة للنزلاء'),
        (3,  N'كافيتريا',           N'Café',                  N'كافيتريا أو ركن مشروبات'),
        (4,  N'مصلى',              N'Prayer room',           N'مصلى أو غرفة صلاة'),
        (5,  N'غرفة غسيل',         N'Laundry room',          N'خدمة غسيل وكي'),
        (6,  N'صالة رياضية',       N'Fitness center',        N'صالة ألعاب رياضية'),
        (7,  N'مسبح',              N'Swimming pool',         N'مسبح داخلي أو خارجي'),
        (8,  N'حديقة',             N'Garden',                N'حديقة أو ساحة خارجية'),
        (9,  N'استقبال 24 ساعة',   N'24-hour reception',     N'خدمة استقبال على مدار الساعة'),
        (10, N'إنترنت لوبي',       N'Lobby Wi‑Fi',           N'شبكة لاسلكية في مناطق مشتركة'),
        (11, N'غرف اجتماعات',      N'Meeting rooms',         N'قاعات اجتماعات صغيرة'),
        (12, N'خدمة أمن',          N'Security',              N'حراسة ومراقبة')
    ) AS v(zaaer_id, name_ar, name_en, descr)
)
INSERT INTO dbo.facilities (
    hotel_id,
    zaaer_id,
    facility_name,
    facility_name_en,
    description,
    building_id,
    floor_id,
    is_active,
    created_at,
    updated_at
)
SELECT
    @HotelScopeId,
    s.zaaer_id,
    s.name_ar,
    s.name_en,
    s.descr,
    NULL,
    NULL,
    1,
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM seed s
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.facilities f
    WHERE f.hotel_id = @HotelScopeId
      AND f.zaaer_id = s.zaaer_id
);

PRINT CONCAT(N'Facilities inserted for hotel scope ', @HotelScopeId, N': ', @@ROWCOUNT);
