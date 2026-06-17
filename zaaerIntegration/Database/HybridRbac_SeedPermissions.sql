/*
PMS permission catalog (AR/EN) — grouped by module / submodule like Zaaer.
Run on Master DB after HybridRbac_MasterDB.sql + HybridRbac_SimplifyPmsSchema.sql
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

INSERT INTO @p (permission_code, permission_name_en, permission_name_ar, module_name, submodule_name, action_name, sort_order)
VALUES
    -- Admin / RBAC
    (N'rbac.users.manage', N'Manage users', N'إدارة المستخدمين', N'admin', N'rbac', N'manage', 10),
    (N'rbac.roles.manage', N'Manage roles', N'إدارة الأدوار', N'admin', N'rbac', N'manage', 20),
    (N'rbac.permissions.view', N'View permissions', N'عرض الصلاحيات', N'admin', N'rbac', N'view', 30),
    (N'admin.numbering.manage', N'Manage numbering (developer)', N'إدارة الترقيم (للمبرمج)', N'admin', N'numbering', N'manage', 35),

    -- Room board
    (N'room_board.view', N'View room board', N'عرض لوحة الغرف', N'room_board', N'room_board', N'view', 100),
    (N'room_board.update_status', N'Update unit status', N'تحديث حالة الوحدة', N'room_board', N'room_board', N'update', 110),

    -- Reservations — core
    (N'reservations.list', N'List reservations', N'قائمة الحجوزات', N'reservations', N'core', N'list', 200),
    (N'reservations.view', N'View reservation', N'عرض الحجز', N'reservations', N'core', N'view', 210),
    (N'reservations.create', N'Create reservation', N'إنشاء حجز', N'reservations', N'core', N'create', 220),
    (N'reservations.update', N'Update reservation', N'تعديل الحجز', N'reservations', N'core', N'update', 230),
    (N'reservations.cancel', N'Cancel reservation', N'إلغاء الحجز', N'reservations', N'core', N'cancel', 240),
    (N'reservations.undo_check_in', N'Undo check-in', N'التراجع عن تسجيل الوصول', N'reservations', N'core', N'undo_check_in', 242),
    (N'reservations.undo_cancel', N'Undo cancel reservation', N'التراجع عن إلغاء الحجز', N'reservations', N'core', N'undo_cancel', 245),
    (N'reservations.reopen', N'Reopen reservation', N'إعادة فتح الحجز', N'reservations', N'core', N'reopen', 250),
    (N'reservations.no_show', N'Mark no-show', N'عدم الحضور', N'reservations', N'core', N'no_show', 255),
    (N'reservations.bulk_create', N'Create bulk reservation', N'إنشاء حجز بالجملة', N'reservations', N'core', N'bulk_create', 260),
    (N'reservations.summary', N'Reservation summary', N'ملخص الحجز', N'reservations', N'core', N'summary', 265),
    (N'reservations.activity_log_view', N'View reservation activity log', N'عرض سجل نشاطات الحجز', N'reservations', N'core', N'activity_log_view', 268),
    (N'reservations.contract', N'Accommodation contract', N'عقد التسكين', N'reservations', N'core', N'contract', 270),
    (N'reservations.vcc', N'Virtual credit card (VCC)', N'بطاقة افتراضية VCC', N'reservations', N'core', N'vcc', 275),

    -- Reservations — stay
    (N'reservations.check_in', N'Check in', N'تسجيل الوصول', N'reservations', N'stay', N'check_in', 280),
    (N'reservations.check_out', N'Check out', N'تسجيل المغادرة', N'reservations', N'stay', N'check_out', 285),
    (N'reservations.late_check_out', N'Late check-out', N'تسجيل خروج متأخر', N'reservations', N'stay', N'late_check_out', 295),

    -- Reservations — units
    (N'reservations.unit_add', N'Add unit', N'إضافة وحدة', N'reservations', N'units', N'unit_add', 300),
    (N'reservations.unit_remove', N'Remove unit', N'حذف وحدة', N'reservations', N'units', N'unit_remove', 305),
    (N'reservations.unit_change', N'Change unit', N'تغيير الوحدة', N'reservations', N'units', N'unit_change', 310),
    (N'reservations.unit_check_out', N'Unit check-out', N'تسجيل خروج الوحدة', N'reservations', N'units', N'unit_check_out', 315),

    -- Reservations — adjustments (image 2)
    (N'reservations.discount', N'Apply discount', N'إضافة خصم', N'reservations', N'adjustments', N'discount', 320),
    (N'reservations.penalty', N'Apply penalty', N'إضافة غرامة', N'reservations', N'adjustments', N'penalty', 325),
    (N'reservations.package', N'Add package / extra', N'إضافة باقة', N'reservations', N'adjustments', N'package', 330),

    -- Reservations — pricing (unit pricing popup + apply)
    (N'reservations.pricing_view', N'View unit night prices (read-only)', N'عرض أسعار الليلة للوحدات (قراءة فقط — بدون تعديل)', N'reservations', N'pricing', N'pricing_view', 348),
    (N'reservations.pricing_edit', N'Edit and apply unit night prices', N'تعديل وتطبيق أسعار الليلة للوحدات', N'reservations', N'pricing', N'pricing_edit', 352),
    (N'reservations.pricing_edit_after_checkin', N'Edit price after check-in', N'تعديل السعر بعد تسجيل الوصول', N'reservations', N'pricing', N'pricing_edit_after_checkin', 350),
    (N'reservations.pricing_below_minimum', N'Price below minimum', N'خفض السعر دون الحد الأدنى', N'reservations', N'pricing', N'pricing_below_minimum', 360),

    -- Reservations — dates (listed reservation: arrival/departure dates, times, daily/monthly)
    (N'reservations.edit_stay_dates_after_checkin', N'Edit stay dates after check-in', N'تعديل تواريخ الوصول والمغادرة بعد تسجيل الوصول', N'reservations', N'dates', N'edit_stay_dates_after_checkin', 370),
    (N'reservations.monthly_calendar_thirty_day', N'Monthly stay: 30-day months', N'الحجز الشهري: شهور 30 يوم', N'reservations', N'dates', N'monthly_calendar_thirty_day', 375),
    (N'reservations.monthly_calendar_actual', N'Monthly stay: actual calendar months', N'الحجز الشهري: شهور ميلادية فعلية', N'reservations', N'dates', N'monthly_calendar_actual', 380),
    (N'reservations.auto_extend', N'Allow auto extension', N'السماح بالتمديد التلقائي', N'reservations', N'dates', N'auto_extend', 400),

    -- Reservations — company / tax / financial
    (N'reservations.company_add', N'Add company', N'إضافة شركة', N'reservations', N'company', N'company_add', 410),
    (N'reservations.tax_modify', N'Modify tax', N'تعديل الضريبة', N'reservations', N'tax', N'tax_modify', 420),
    (N'reservations.financial_summary_view', N'View financial summary', N'عرض الملخص المالي', N'reservations', N'financial', N'financial_summary_view', 430),

    -- Guests
    (N'guests.list', N'List guests', N'قائمة النزلاء', N'guests', N'guests', N'list', 500),
    (N'guests.view', N'View guest', N'عرض النزيل', N'guests', N'guests', N'view', 510),
    (N'guests.create', N'Create guest', N'إضافة نزيل', N'guests', N'guests', N'create', 520),
    (N'guests.update', N'Update guest', N'تعديل النزيل', N'guests', N'guests', N'update', 530),

    -- Payment receipts (سندات القبض)
    (N'payments.list', N'List receipts', N'قائمة السندات', N'finance', N'receipt_voucher', N'list', 600),
    (N'payments.view', N'View receipt', N'عرض السند', N'finance', N'receipt_voucher', N'view', 610),
    (N'finance.receipt_voucher.document_date', N'Show receipt voucher date', N'إظهار تاريخ السند', N'finance', N'receipt_voucher', N'document_date', 615),
    (N'payments.create', N'Create receipt', N'إنشاء سند قبض', N'finance', N'receipt_voucher', N'create', 620),
    (N'payments.receipt_voucher.edit', N'Edit receipt voucher', N'تعديل سند قبض', N'finance', N'receipt_voucher', N'edit', 625),
    (N'payments.cancel', N'Cancel receipt', N'إلغاء السند', N'finance', N'receipt_voucher', N'cancel', 630),

    -- Refund / disbursement vouchers (سندات الاسترداد — incl. سندات الصرف in PMS UI)
    (N'finance.refund_voucher.document_date', N'Show refund/disbursement voucher date', N'إظهار تاريخ السند', N'finance', N'refund_voucher', N'document_date', 635),
    (N'payments.refund_voucher.cancel', N'Cancel refund/disbursement voucher', N'إلغاء السند', N'finance', N'refund_voucher', N'cancel', 637),
    (N'payments.refund', N'Refund voucher', N'سند استرداد', N'finance', N'refund_voucher', N'create', 640),
    (N'payments.refund_voucher.edit', N'Edit refund/disbursement voucher', N'تعديل سند صرف/استرداد', N'finance', N'refund_voucher', N'edit', 645),

    -- Invoices / promissory (future screens)
    (N'finance.invoice.document_date', N'Show invoice document date', N'إظهار تاريخ السند', N'finance', N'invoice', N'document_date', 652),
    (N'finance.promissory_note.document_date', N'Show promissory document date', N'إظهار تاريخ السند', N'finance', N'promissory_note', N'document_date', 662),
    (N'finance.invoice.create', N'Create invoice', N'إنشاء فاتورة', N'finance', N'invoice', N'create', 650),
    (N'finance.invoice.view', N'View invoices on reservation', N'عرض فواتير الحجز', N'finance', N'invoice', N'view', 651),
    (N'finance.invoice.send_zatca', N'Send invoice to ZATCA', N'إرسال الفاتورة إلى الزكاة', N'finance', N'invoice', N'send_zatca', 653),
    (N'finance.credit_note.create', N'Create credit note', N'إنشاء إشعار دائن', N'finance', N'credit_note', N'create', 656),
    (N'finance.credit_note.view', N'View credit notes', N'عرض الإشعارات الدائنة', N'finance', N'credit_note', N'view', 657),
    (N'finance.credit_note.send_zatca', N'Send credit note to ZATCA', N'إرسال الإشعار الدائن إلى الزكاة', N'finance', N'credit_note', N'send_zatca', 658),
    (N'finance.debit_note.create', N'Create debit note', N'إنشاء إشعار مدين', N'finance', N'debit_note', N'create', 659),
    (N'finance.debit_note.view', N'View debit notes', N'عرض الإشعارات المدينة', N'finance', N'debit_note', N'view', 661),
    (N'finance.debit_note.send_zatca', N'Send debit note to ZATCA', N'إرسال الإشعار المدين إلى الزكاة', N'finance', N'debit_note', N'send_zatca', 663),
    (N'finance.promissory.create', N'Create promissory note', N'إنشاء سند لأمر', N'finance', N'promissory_note', N'create', 660),
    (N'finance.promissory_note.edit', N'Edit promissory note', N'تعديل سند لأمر', N'finance', N'promissory_note', N'edit', 665),
    (N'finance.promissory_note.cancel', N'Cancel promissory note', N'إلغاء سند لأمر', N'finance', N'promissory_note', N'cancel', 668),
    (N'finance.invoice.cancel', N'Cancel invoice (credit note)', N'إلغاء الفاتورة (إشعار دائن)', N'finance', N'invoice', N'cancel', 655),

    -- Hotel expenses (PMS api/v1/pms/expenses)
    (N'finance.expense.view', N'View hotel expenses', N'عرض مصروفات الفندق', N'finance', N'expense', N'view', 670),
    (N'finance.expense.create', N'Create hotel expense', N'إنشاء مصروف', N'finance', N'expense', N'create', 671),
    (N'finance.expense.update', N'Update hotel expense', N'تعديل مصروف', N'finance', N'expense', N'update', 672),
    (N'finance.expense.approve', N'Approve/reject hotel expense', N'اعتماد/رفض مصروف', N'finance', N'expense', N'approve', 673),
    (N'finance.expense.document_date', N'Show expense date', N'تاريخ المصروف', N'finance', N'expense', N'document_date', 674),

    -- Bank deposits (PMS api/v1/pms/deposits)
    (N'finance.deposit.view', N'View bank deposits', N'عرض الإيداعات', N'finance', N'deposit', N'view', 675),
    (N'finance.deposit.create', N'Create bank deposit', N'إنشاء إيداع', N'finance', N'deposit', N'create', 676),
    (N'finance.deposit.update', N'Update bank deposit', N'تعديل إيداع', N'finance', N'deposit', N'update', 677),
    (N'finance.deposit.document_date', N'Show deposit date', N'تاريخ الإيداع', N'finance', N'deposit', N'document_date', 678),

    -- Property / units setup
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
    (N'property.rates.manage', N'Manage unit rates', N'إدارة أسعار الوحدات', N'property', N'rates', N'manage', 673),

    -- Point of sale (POS)
    (N'pos.view', N'Use point of sale', N'استخدام نقاط البيع', N'pos', N'terminal', N'view', 700),
    (N'pos.orders.create', N'Create POS order', N'إنشاء طلب نقطة بيع', N'pos', N'orders', N'create', 710),
    (N'pos.orders.discount', N'Apply POS order discount', N'تطبيق خصم على طلب نقطة بيع', N'pos', N'orders', N'discount', 712),
    (N'pos.orders.receipt_edit', N'Edit POS order receipt', N'تعديل سند طلب نقطة بيع', N'pos', N'orders', N'receipt_edit', 715),
    (N'pos.orders.cancel', N'Cancel POS order', N'إلغاء طلب نقطة بيع', N'pos', N'orders', N'cancel', 718),
    (N'pos.settings.view', N'View POS catalog settings', N'عرض إعدادات نقاط البيع', N'pos', N'settings', N'view', 720),
    (N'pos.settings.manage', N'Manage POS catalog', N'إدارة كتالوج نقاط البيع', N'pos', N'settings', N'manage', 730),

    -- Platform integrations (NTMP / Shomoos / ZATCA / Balady)
    (N'integrations.view', N'View platform integrations', N'عرض تكامل المنصات', N'integrations', N'platforms', N'view', 800),
    (N'integrations.manage', N'Manage platform integrations', N'إدارة تكامل المنصات', N'integrations', N'platforms', N'manage', 810),
    (N'integrations.balady.view', N'View Balady disclosure report', N'عرض تقرير بلدي', N'integrations', N'balady', N'view', 815);

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

PRINT CONCAT(N'Permissions synced: ', @@ROWCOUNT);

/*
RBAC policy (stable):
  reservations.update                    = general reservation PATCH / save
  reservations.pricing_view                 = unit night pricing popup read-only (no grid edit, no apply)
  reservations.pricing_edit                  = edit night rates in popup and apply to reservation
  reservations.pricing_edit_after_checkin = unit pricing after check-in only (new/unchecked-in use update/create/pricing_edit)
  reservations.edit_stay_dates_after_checkin = listed: dates, times, nights/months, daily/monthly switch
  reservations.monthly_calendar_thirty_day = monthly rental uses 30-day month blocks (default when granted alone)
  reservations.monthly_calendar_actual = monthly rental uses actual Gregorian calendar months
  Both granted = user can toggle on reservation detail; one granted = forced mode; neither = 30-day default
  reservations.auto_extend = hide switch + force is_auto_extend=true; without it user toggles manually
  reservations.pricing_below_minimum     = allow gross rate below room-type minimum
  reservations.check_in/out/reopen/undo_check_in/... = actions; never implied by update on API
  reservations.undo_check_in             = undo check-in (core card); PATCH checked_in → confirmed
  reservations.late_check_out            = allow checkout when planned departure (KSA) is before today; without it checkout is blocked before the wizard

Obsolete pricing codes removed via HybridRbac_RemoveObsoletePricingPermissions.sql

  finance.receipt_voucher.document_date = receipt forms (إيجار/تأمين); grids always show date; save always sends date
  finance.refund_voucher.document_date  = disbursement/refund forms (كارت سندات الاسترداد); grids always show date
  payments.refund_voucher.cancel       = cancel disbursement/refund vouchers (كارت سندات الاسترداد); same UX as payments.cancel on receipts
  payments.refund                      = create disbursement/refund only (not cancel)

  Obsolete: finance.disbursement_voucher.document_date — HybridRbac_MigrateFinanceDocumentDatePermissions.sql
  finance.invoice.document_date              = invoices (future)
  finance.promissory_note.document_date      = promissory notes (future)
  finance.promissory_note.cancel             = cancel promissory note (سند لأمر)
  finance.invoice.cancel                     = cancel invoice / credit note (إشعار دائن)
  finance.expense.view/create/update/approve = PMS hotel expenses (api/v1/pms/expenses)
  finance.expense.document_date            = expense form date field; grids always show date
  finance.deposit.view/create/update       = PMS bank deposits (api/v1/pms/deposits)
  finance.deposit.document_date            = deposit form date field; grids always show date

  Obsolete: finance.document_date — run HybridRbac_MigrateFinanceDocumentDatePermissions.sql
  Obsolete: HybridRbac_AddFinanceExpensePermissions.sql — merged into this file
*/
