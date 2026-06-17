/*
  Incremental property/unit-setup permissions for existing Master DBs.
  Prefer re-running HybridRbac_SeedPermissions.sql when possible.
*/
SET NOCOUNT ON;

DECLARE @p TABLE (
    permission_code NVARCHAR(150),
    permission_name_en NVARCHAR(200),
    permission_name_ar NVARCHAR(200),
    module_name NVARCHAR(80),
    submodule_name NVARCHAR(80),
    action_name NVARCHAR(80),
    sort_order INT
);

INSERT INTO @p VALUES
    (N'property.settings.view', N'View property settings', N'عرض إعدادات الوحدات', N'property', N'settings', N'view', 650),
    (N'property.settings.manage', N'Manage property settings', N'إدارة إعدادات الوحدات', N'property', N'settings', N'manage', 651),
    (N'property.units.list', N'List units', N'قائمة الوحدات', N'property', N'units', N'list', 652),
    (N'property.units.view', N'View unit', N'عرض الوحدة', N'property', N'units', N'view', 653),
    (N'property.units.create', N'Create unit', N'إنشاء وحدة', N'property', N'units', N'create', 654),
    (N'property.units.update', N'Update unit', N'تعديل الوحدة', N'property', N'units', N'update', 655),
    (N'property.units.delete', N'Delete unit', N'حذف الوحدة', N'property', N'units', N'delete', 656),
    (N'property.buildings.list', N'List blocks', N'قائمة البلوكات', N'property', N'buildings', N'list', 657),
    (N'property.buildings.view', N'View block', N'عرض البلوك', N'property', N'buildings', N'view', 658),
    (N'property.buildings.create', N'Create block', N'إنشاء بلوك', N'property', N'buildings', N'create', 659),
    (N'property.buildings.update', N'Update block', N'تعديل البلوك', N'property', N'buildings', N'update', 660),
    (N'property.buildings.delete', N'Delete block', N'حذف البلوك', N'property', N'buildings', N'delete', 661),
    (N'property.room_types.list', N'List unit types', N'قائمة أنواع الوحدات', N'property', N'room_types', N'list', 662),
    (N'property.room_types.view', N'View unit type', N'عرض نوع الوحدة', N'property', N'room_types', N'view', 663),
    (N'property.room_types.create', N'Create unit type', N'إنشاء نوع وحدة', N'property', N'room_types', N'create', 664),
    (N'property.room_types.update', N'Update unit type', N'تعديل نوع الوحدة', N'property', N'room_types', N'update', 665),
    (N'property.room_types.delete', N'Delete unit type', N'حذف نوع الوحدة', N'property', N'room_types', N'delete', 666),
    (N'property.facilities.list', N'List facilities', N'قائمة المرافق', N'property', N'facilities', N'list', 667),
    (N'property.facilities.view', N'View facility', N'عرض المرفق', N'property', N'facilities', N'view', 668),
    (N'property.facilities.create', N'Create facility', N'إنشاء مرفق', N'property', N'facilities', N'create', 669),
    (N'property.facilities.update', N'Update facility', N'تعديل المرفق', N'property', N'facilities', N'update', 670),
    (N'property.facilities.delete', N'Delete facility', N'حذف المرفق', N'property', N'facilities', N'delete', 671),
    (N'property.rates.view', N'View unit rates', N'عرض أسعار الوحدات', N'property', N'rates', N'view', 672),
    (N'property.rates.manage', N'Manage unit rates', N'إدارة أسعار الوحدات', N'property', N'rates', N'manage', 673);

MERGE dbo.pms_permissions AS target
USING @p AS source
ON target.permission_code = source.permission_code
WHEN MATCHED THEN
    UPDATE SET
        permission_name = source.permission_name_en,
        permission_name_en = source.permission_name_en,
        permission_name_ar = source.permission_name_ar,
        module_name = source.module_name,
        submodule_name = source.submodule_name,
        action_name = source.action_name,
        sort_order = source.sort_order,
        is_active = 1
WHEN NOT MATCHED THEN
    INSERT (permission_code, permission_name, permission_name_en, permission_name_ar,
            module_name, submodule_name, action_name, sort_order, is_active, created_at)
    VALUES (source.permission_code, source.permission_name_en, source.permission_name_en, source.permission_name_ar,
            source.module_name, source.submodule_name, source.action_name, source.sort_order, 1, SYSUTCDATETIME());

PRINT CONCAT(N'Property permissions synced: ', @@ROWCOUNT);
