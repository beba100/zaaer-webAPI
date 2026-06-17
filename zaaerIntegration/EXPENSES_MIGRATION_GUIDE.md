# دليل نقل ملفات Expenses و VoM إلى مشروع جديد

## 📋 نظرة عامة

هذا الدليل يحتوي على قائمة شاملة بجميع الملفات والمكونات المتعلقة بـ **Expenses** (المصروفات) و **VoM** (Voice of Management) في المشروع الحالي، لمساعدة في نقلها إلى مشروع جديد بدون كسر أي شيء.

### ✨ الميزات الرئيسية

- ✅ **CRUD Operations** للمصروفات
- ✅ **Multi-Tenancy Support** مع X-Hotel-Code header
- ✅ **Approval Workflow** مع قواعد الموافقة
- ✅ **Analytics Dashboard** مع KPIs و Trend Analysis
- ✅ **VoM Integration** لإرسال المصروفات إلى VoM
- ✅ **Partner Queue System** للمعالجة غير المتزامنة
- ✅ **Role-Based Access Control** لصفحات مختلفة حسب الدور
- ✅ **Expense Rooms Management** لإدارة غرف المصروفات
- ✅ **Image Upload** للمرفقات

---

## 🗂️ Backend - Controllers

### 1. ExpenseController.cs
**الموقع:** `zaaerIntegration/Controllers/ExpenseController.cs`
**الوصف:** Controller الرئيسي لإدارة المصروفات
**APIs:**
- CRUD operations للمصروفات
- Analytics endpoints
- Supervisor expenses
- Approval workflows

### 2. ExpenseApprovalRulesController.cs
**الموقع:** `zaaerIntegration/Controllers/ExpenseApprovalRulesController.cs`
**الوصف:** Controller لإدارة قواعد الموافقة على المصروفات

### 3. VoMExpensesController.cs
**الموقع:** `zaaerIntegration/Controllers/VoMExpensesController.cs`
**الوصف:** Controller لإدارة إرسال المصروفات إلى VoM

### 4. VoMAutoSendJobController.cs
**الموقع:** `zaaerIntegration/Controllers/Jobs/VoMAutoSendJobController.cs`
**الوصف:** Controller للـ Background Job لإرسال المصروفات تلقائياً إلى VoM

### 5. ZaaerExpenseController.cs (اختياري - للتكامل مع Zaaer)
**الموقع:** `zaaerIntegration/Controllers/Zaaer/ZaaerExpenseController.cs`
**الوصف:** Controller للتكامل مع نظام Zaaer

---

## 🗂️ Backend - Partner Queue Handlers

### ZaaerGenericHandlers.cs
**الموقع:** `zaaerIntegration/Services/PartnerQueue/Handlers/ZaaerGenericHandlers.cs`

**Expense Handlers:**
1. **ZaaerExpenseCreateHandler** - Key: `"Zaaer.Expense.Create"`
   - معالجة إنشاء مصروفات من Zaaer

2. **ZaaerExpenseUpdateByIdHandler** - Key: `"Zaaer.Expense.UpdateById"`
   - معالجة تحديث مصروفات من Zaaer

3. **ExpenseCreateHandler** - Key: `"Expense.Create"`
   - معالجة إنشاء مصروفات جديدة

4. **ExpenseUpdateByIdHandler** - Key: `"Expense.UpdateById"`
   - معالجة تحديث مصروفات بالـ ID

5. **ExpenseDeleteHandler** - Key: `"Expense.Delete"`
   - معالجة حذف مصروفات

6. **ExpenseRoomAddHandler** - Key: `"Expense.Room.Add"`
   - معالجة إضافة غرف للمصروفات

7. **ExpenseRoomUpdateHandler** - Key: `"Expense.Room.Update"`
   - معالجة تحديث غرف المصروفات

8. **ExpenseRoomDeleteHandler** - Key: `"Expense.Room.Delete"`
   - معالجة حذف غرف المصروفات

**ملاحظة:** جميع Handlers موجودة في نفس الملف `ZaaerGenericHandlers.cs`

---

## 🗂️ Backend - Services

### Services/Expense/

#### 1. IExpenseService.cs
**الموقع:** `zaaerIntegration/Services/Expense/IExpenseService.cs`
**الوصف:** Interface للـ Expense Service

#### 2. ExpenseService.cs
**الموقع:** `zaaerIntegration/Services/Expense/ExpenseService.cs`
**الوصف:** Service الرئيسي لإدارة المصروفات (CRUD operations)

#### 3. ExpenseDapperService.cs
**الموقع:** `zaaerIntegration/Services/Expense/ExpenseDapperService.cs`
**الوصف:** Service محسّن باستخدام Dapper للاستعلامات المعقدة

#### 4. IExpenseApprovalRuleService.cs
**الموقع:** `zaaerIntegration/Services/Expense/IExpenseApprovalRuleService.cs`
**الوصف:** Interface لخدمة قواعد الموافقة

#### 5. ExpenseApprovalRuleService.cs
**الموقع:** `zaaerIntegration/Services/Expense/ExpenseApprovalRuleService.cs`
**الوصف:** Service لإدارة قواعد الموافقة على المصروفات

### Services/VoM/

#### 1. IVoMAuthService.cs
**الموقع:** `zaaerIntegration/Services/VoM/IVoMAuthService.cs`
**الوصف:** Interface لخدمة المصادقة مع VoM

#### 2. VoMAuthService.cs
**الموقع:** `zaaerIntegration/Services/VoM/VoMAuthService.cs`
**الوصف:** Service للمصادقة مع VoM API

#### 3. IVoMJournalEntryService.cs
**الموقع:** `zaaerIntegration/Services/VoM/IVoMJournalEntryService.cs`
**الوصف:** Interface لخدمة Journal Entries في VoM

#### 4. VoMJournalEntryService.cs
**الموقع:** `zaaerIntegration/Services/VoM/VoMJournalEntryService.cs`
**الوصف:** Service لإرسال Journal Entries إلى VoM

#### 5. VoMExpensesDapperService.cs
**الموقع:** `zaaerIntegration/Services/VoM/VoMExpensesDapperService.cs`
**الوصف:** Service محسّن باستخدام Dapper للاستعلامات المتعلقة بـ VoM

### Services الأخرى

#### 1. ExpenseJournalEntryService.cs
**الموقع:** `zaaerIntegration/Services/ExpenseJournalEntryService.cs`
**الوصف:** Service لتحويل المصروفات إلى Journal Entries

#### 2. ZaaerExpenseService.cs (اختياري)
**الموقع:** `zaaerIntegration/Services/Zaaer/ZaaerExpenseService.cs`
**الوصف:** Service للتكامل مع Zaaer

---

## 🗂️ Backend - Models

### Models الأساسية

#### 1. Expense.cs
**الموقع:** `zaaerIntegration/Models/Expense.cs`
**الوصف:** نموذج المصروف الرئيسي
**الجداول:** `expenses`

#### 2. ExpenseRoom.cs
**الموقع:** `zaaerIntegration/Models/ExpenseRoom.cs`
**الوصف:** نموذج غرف المصروف
**الجداول:** `expense_rooms`

#### 3. ExpenseCategory.cs
**الموقع:** `zaaerIntegration/Models/ExpenseCategory.cs`
**الوصف:** نموذج فئات المصروفات
**الجداول:** `expense_categories`

#### 4. ExpenseImage.cs
**الموقع:** `zaaerIntegration/Models/ExpenseImage.cs`
**الوصف:** نموذج صور المصروفات
**الجداول:** `expense_images`

#### 5. ExpenseApprovalHistory.cs
**الموقع:** `zaaerIntegration/Models/ExpenseApprovalHistory.cs`
**الوصف:** نموذج سجل الموافقات على المصروفات
**الجداول:** `expense_approval_history`

#### 6. ExpenseApprovalRule.cs
**الموقع:** `zaaerIntegration/Models/ExpenseApprovalRule.cs`
**الوصف:** نموذج قواعد الموافقة على المصروفات
**الجداول:** `ExpenseApprovalRules` (في Master DB)

#### 7. MasterExpenseCategory.cs
**الموقع:** `zaaerIntegration/Models/MasterExpenseCategory.cs`
**الوصف:** نموذج فئات المصروفات الرئيسية (في Master DB)

### Models/VoM/

#### 1. ExpenseJournalEntry.cs
**الموقع:** `zaaerIntegration/Models/VoM/ExpenseJournalEntry.cs`
**الوصف:** نموذج سجل إرسال المصروفات إلى VoM
**الجداول:** `ExpenseJournalEntries`

#### 2. ChartOfAccounts.cs
**الموقع:** `zaaerIntegration/Models/VoM/ChartOfAccounts.cs`
**الوصف:** نموذج دليل الحسابات لـ VoM

#### 3. CostCenter.cs
**الموقع:** `zaaerIntegration/Models/VoM/CostCenter.cs`
**الوصف:** نموذج مراكز التكلفة لـ VoM

---

## 🗂️ Backend - DTOs

### DTOs/Expense/

1. **CreateExpenseDto.cs** - إنشاء مصروف جديد
2. **UpdateExpenseDto.cs** - تحديث مصروف
3. **ExpenseResponseDto.cs** - استجابة المصروف
4. **CreateExpenseRoomDto.cs** - إنشاء غرفة مصروف
5. **UpdateExpenseRoomDto.cs** - تحديث غرفة مصروف
6. **ExpenseRoomResponseDto.cs** - استجابة غرفة المصروف
7. **ExpenseApprovalHistoryDto.cs** - سجل الموافقات
8. **ExpenseAnalyticsKpiDto.cs** - KPIs للتحليلات
9. **ExpenseAnalyticsTrendDto.cs** - اتجاهات التحليلات
10. **ExpenseAnalyticsRoleStatisticsDto.cs** - إحصائيات الأدوار
11. **ExpenseAnalyticsStatusDistributionDto.cs** - توزيع الحالات
12. **ExpenseAnalyticsTopHotelDto.cs** - أفضل الفنادق
13. **ExpenseAnalyticsTopCategoryDto.cs** - أفضل الفئات
14. **ExpenseAnalyticsHotelTableDto.cs** - جدول الفنادق
15. **SupervisorHotelExpenseSummaryDto.cs** - ملخص مصروفات المشرفين

### DTOs/VoM/

1. **VoMAuthDto.cs** - مصادقة VoM
2. **VoMJournalEntryRequestDto.cs** - طلب Journal Entry
3. **VoMJournalEntryResponseDto.cs** - استجابة Journal Entry

### DTOs/Zaaer/ (اختياري)

1. **ZaaerCreateExpenseDto.cs**
2. **ZaaerUpdateExpenseDto.cs**
3. **ZaaerExpenseResponseDto.cs**

---

## 🗂️ Backend - Configuration

### 1. VoMAccountConfiguration.cs
**الموقع:** `zaaerIntegration/Configuration/VoMAccountConfiguration.cs`
**الوصف:** ثوابت تكوين حساب VoM (Tax Status)

### 2. appsettings.json
**الأقسام المتعلقة:**
```json
{
  "VoMAutoSend": {
    "ApiKey": "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f"
  },
  "VoM": {
    "BaseUrl": "https://kimoo.getvom.com",
    ...
  }
}
```

### 3. Program.cs
**التسجيلات المطلوبة:**
```csharp
// Expense Services
builder.Services.AddScoped<IZaaerExpenseService, ZaaerExpenseService>();
builder.Services.AddScoped<ExpenseDapperService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IExpenseApprovalRuleService, ExpenseApprovalRuleService>();

// VoM Services
builder.Services.AddScoped<zaaerIntegration.Services.VoM.IVoMAuthService, zaaerIntegration.Services.VoM.VoMAuthService>();
builder.Services.AddScoped<zaaerIntegration.Services.VoM.IVoMJournalEntryService, zaaerIntegration.Services.VoM.VoMJournalEntryService>();
builder.Services.AddScoped<zaaerIntegration.Services.IExpenseJournalEntryService, zaaerIntegration.Services.ExpenseJournalEntryService>();
builder.Services.AddScoped<zaaerIntegration.Services.VoM.VoMExpensesDapperService>();

// Expense Queue Handlers
builder.Services.AddScoped<IQueuedOperationHandler, ExpenseCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ExpenseUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ExpenseDeleteHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ExpenseRoomAddHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ExpenseRoomUpdateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ExpenseRoomDeleteHandler>();
```

---

## 🗂️ Backend - Data Context

### ApplicationDbContext.cs
**الموقع:** `zaaerIntegration/Data/ApplicationDbContext.cs`

**DbSets المطلوبة:**
```csharp
public DbSet<Expense> Expenses { get; set; }
public DbSet<ExpenseRoom> ExpenseRooms { get; set; }
public DbSet<FinanceLedgerAPI.Models.ExpenseCategory> ExpenseCategories { get; set; }
public DbSet<ExpenseImage> ExpenseImages { get; set; }
public DbSet<ExpenseApprovalHistory> ExpenseApprovalHistories { get; set; }
public DbSet<ExpenseJournalEntry> ExpenseJournalEntries { get; set; }
```

**Methods المطلوبة:**
- `ConfigureExpenseRelationships(ModelBuilder modelBuilder)` - تكوين العلاقات

### MasterDbContext.cs
**الموقع:** `zaaerIntegration/Data/MasterDbContext.cs`

**DbSets المطلوبة:**
```csharp
public DbSet<ExpenseApprovalRule> ExpenseApprovalRules { get; set; }
public DbSet<MasterExpenseCategory> MasterExpenseCategories { get; set; }
```

---

## 🗂️ Frontend - HTML Pages

### صفحات المصروفات الرئيسية

1. **expenses.html**
   - **الموقع:** `zaaerIntegration/wwwroot/expenses.html`
   - **الوصف:** صفحة المصروفات للموظفين العاديين

2. **admin-expenses.html**
   - **الموقع:** `zaaerIntegration/wwwroot/admin-expenses.html`
   - **الوصف:** صفحة المصروفات للإداريين

3. **manager-expenses.html**
   - **الموقع:** `zaaerIntegration/wwwroot/manager-expenses.html`
   - **الوصف:** صفحة المصروفات للمديرين

4. **accountant-expenses.html**
   - **الموقع:** `zaaerIntegration/wwwroot/accountant-expenses.html`
   - **الوصف:** صفحة المصروفات للمحاسبين

5. **supervisor-expenses.html**
   - **الموقع:** `zaaerIntegration/wwwroot/supervisor-expenses.html`
   - **الوصف:** صفحة المصروفات للمشرفين

6. **verifier-expenses.html**
   - **الموقع:** `zaaerIntegration/wwwroot/verifier-expenses.html`
   - **الوصف:** صفحة المصروفات للمراجعين

7. **officer-expenses.html**
   - **الموقع:** `zaaerIntegration/wwwroot/officer-expenses.html`
   - **الوصف:** صفحة المصروفات للموظفين الإداريين

8. **owner-expenses.html**
   - **الموقع:** `zaaerIntegration/wwwroot/owner-expenses.html`
   - **الوصف:** صفحة المصروفات للمالكين

9. **admin-expenses.html** (في المجلد الرئيسي)
   - **الموقع:** `admin-expenses.html`
   - **ملاحظة:** نسخة إضافية من الصفحة

### صفحات VoM

10. **vom-expenses.html**
    - **الموقع:** `zaaerIntegration/wwwroot/vom-expenses.html`
    - **الوصف:** صفحة إدارة إرسال المصروفات إلى VoM

### صفحات أخرى متعلقة

11. **approve-expense.html**
    - **الموقع:** `zaaerIntegration/wwwroot/approve-expense.html`
    - **الوصف:** صفحة الموافقة على المصروفات

---

## 🗂️ Frontend - JavaScript Files

### ملفات JavaScript العامة

1. **analytics_functions.js**
   - **الموقع:** `zaaerIntegration/wwwroot/analytics_functions.js`
   - **الوصف:** دوال التحليلات للمصروفات

2. **analytics_temp.js**
   - **الموقع:** `zaaerIntegration/wwwroot/analytics_temp.js`
   - **الوصف:** ملف مؤقت للتحليلات

3. **js/scripts.js**
   - **الموقع:** `zaaerIntegration/wwwroot/js/scripts.js`
   - **الوصف:** ملفات JavaScript العامة (قد تحتوي على دوال متعلقة)

---

## 🗂️ Database Tables

### جداول Tenant Database

1. **expenses** - المصروفات الرئيسية
2. **expense_rooms** - غرف المصروفات
3. **expense_categories** - فئات المصروفات
4. **expense_images** - صور المصروفات
5. **expense_approval_history** - سجل الموافقات
6. **ExpenseJournalEntries** - سجل إرسال المصروفات إلى VoM

### جداول Master Database

1. **ExpenseApprovalRules** - قواعد الموافقة على المصروفات
2. **MasterExpenseCategories** - فئات المصروفات الرئيسية

---

## 🔗 Dependencies المطلوبة

### NuGet Packages
- Entity Framework Core
- Dapper (لـ Dapper Services)
- AutoMapper (إن وجد)
- FluentValidation (إن وجد)

### External APIs
- **VoM API:** `https://kimoo.getvom.com`
- **Zaaer API** (إن كان التكامل مطلوب)

---

## 📝 خطوات النقل

### 1. نقل الملفات Backend

#### Controllers
```bash
Controllers/ExpenseController.cs
Controllers/ExpenseApprovalRulesController.cs
Controllers/VoMExpensesController.cs
Controllers/Jobs/VoMAutoSendJobController.cs
Controllers/Zaaer/ZaaerExpenseController.cs (اختياري)
```

#### Services
```bash
Services/Expense/
  - IExpenseService.cs
  - ExpenseService.cs
  - ExpenseDapperService.cs
  - IExpenseApprovalRuleService.cs
  - ExpenseApprovalRuleService.cs

Services/VoM/
  - IVoMAuthService.cs
  - VoMAuthService.cs
  - IVoMJournalEntryService.cs
  - VoMJournalEntryService.cs
  - VoMExpensesDapperService.cs

Services/ExpenseJournalEntryService.cs
Services/Zaaer/ZaaerExpenseService.cs (اختياري)
```

#### Models
```bash
Models/
  - Expense.cs
  - ExpenseRoom.cs
  - ExpenseCategory.cs
  - ExpenseImage.cs
  - ExpenseApprovalHistory.cs
  - ExpenseApprovalRule.cs
  - MasterExpenseCategory.cs

Models/VoM/
  - ExpenseJournalEntry.cs
  - ChartOfAccounts.cs
  - CostCenter.cs
```

#### DTOs
```bash
DTOs/Expense/
  - CreateExpenseDto.cs
  - UpdateExpenseDto.cs
  - ExpenseResponseDto.cs
  - CreateExpenseRoomDto.cs
  - UpdateExpenseRoomDto.cs
  - ExpenseRoomResponseDto.cs
  - ExpenseApprovalHistoryDto.cs
  - ExpenseAnalyticsKpiDto.cs
  - ExpenseAnalyticsTrendDto.cs
  - ExpenseAnalyticsRoleStatisticsDto.cs
  - ExpenseAnalyticsStatusDistributionDto.cs
  - ExpenseAnalyticsTopHotelDto.cs
  - ExpenseAnalyticsTopCategoryDto.cs
  - ExpenseAnalyticsHotelTableDto.cs
  - SupervisorHotelExpenseSummaryDto.cs

DTOs/VoM/
  - VoMAuthDto.cs
  - VoMJournalEntryRequestDto.cs
  - VoMJournalEntryResponseDto.cs

DTOs/Zaaer/ (اختياري)
  - ZaaerCreateExpenseDto.cs
  - ZaaerUpdateExpenseDto.cs
  - ZaaerExpenseResponseDto.cs
```

#### Partner Queue Handlers
```bash
Services/PartnerQueue/Handlers/ZaaerGenericHandlers.cs
# يحتوي على:
# - ZaaerExpenseCreateHandler
# - ZaaerExpenseUpdateByIdHandler
# - ExpenseCreateHandler
# - ExpenseUpdateByIdHandler
# - ExpenseDeleteHandler
# - ExpenseRoomAddHandler
# - ExpenseRoomUpdateHandler
# - ExpenseRoomDeleteHandler
```

#### Configuration
```bash
Configuration/VoMAccountConfiguration.cs
```

### 2. تحديث Data Context

```csharp
// في ApplicationDbContext.cs
// إضافة DbSets المطلوبة
// إضافة ConfigureExpenseRelationships method

// في MasterDbContext.cs
// إضافة DbSets المطلوبة
```

### 3. تحديث Program.cs

```csharp
// تسجيل جميع Services المطلوبة
// تسجيل Queue Handlers إن وجدت
```

### 4. تحديث appsettings.json

```json
{
  "VoMAutoSend": {
    "ApiKey": "..."
  },
  "VoM": {
    "BaseUrl": "...",
    ...
  }
}
```

### 5. نقل ملفات Frontend

#### HTML Pages
```bash
wwwroot/
  - expenses.html (للموظفين العاديين)
  - admin-expenses.html (للإداريين)
  - manager-expenses.html (للمديرين)
  - accountant-expenses.html (للمحاسبين)
  - supervisor-expenses.html (للمشرفين)
  - verifier-expenses.html (للمراجعين)
  - officer-expenses.html (للموظفين الإداريين)
  - owner-expenses.html (للمالكين)
  - vom-expenses.html (صفحة VoM)
  - approve-expense.html (صفحة الموافقة)

# ملف إضافي
admin-expenses.html (في المجلد الرئيسي - إن وجد)
```

#### JavaScript Files
```bash
wwwroot/
  - analytics_functions.js (دوال التحليلات)
  - analytics_temp.js (ملف مؤقت للتحليلات)
  - js/scripts.js (مراجعة المحتوى - قد يحتوي على دوال متعلقة)
```

**ملاحظة:** جميع صفحات HTML تحتوي على JavaScript مدمج داخلها

### 6. Database Migration

```sql
-- نسخ جداول Tenant Database
-- نسخ جداول Master Database
-- نسخ البيانات إن لزم الأمر
```

---

## ⚠️ ملاحظات مهمة

### 1. التبعيات

- **ITenantService:** Expense Services تعتمد على ITenantService للحصول على HotelId
- **IUnitOfWork:** بعض Services تستخدم Unit of Work pattern
- **ITenantDatabaseService:** للحصول على Connection Strings للـ Tenants

### 2. Authentication & Authorization

- جميع Controllers تتطلب Authentication
- بعض Endpoints تتطلب أدوار محددة (Admin, Manager, etc.)

### 3. Multi-Tenancy

- النظام يعتمد على Multi-Tenancy
- يستخدم `X-Hotel-Code` header لتحديد الفندق
- كل Tenant له Database منفصل

### 4. Queue System

- النظام يستخدم Partner Queue System
- Expense Operations يتم إرسالها عبر Queue
- يجب تسجيل Queue Handlers في Program.cs

### 5. VoM Integration

- يتطلب تكوين VoM API credentials
- يستخدم Background Job لإرسال المصروفات تلقائياً
- يحفظ سجل الإرسال في ExpenseJournalEntries table

---

## 🔍 فحص بعد النقل

### Checklist

- [ ] جميع Controllers تعمل بشكل صحيح
- [ ] جميع Services مسجلة في DI Container
- [ ] DbSets موجودة في Context
- [ ] Relationships مُكوّنة بشكل صحيح
- [ ] Frontend Pages تعمل وتتصل بالـ APIs
- [ ] VoM Integration يعمل
- [ ] Queue Handlers مسجلة
- [ ] Authentication & Authorization تعمل
- [ ] Multi-Tenancy يعمل
- [ ] Database Migrations تمت بنجاح

---

## 📞 الدعم

في حالة وجود أي مشاكل أو أسئلة أثناء النقل، يرجى مراجعة:
1. Logs في Application
2. Database Schema
3. API Documentation
4. Frontend Console Errors

---

## 📅 تاريخ الإنشاء

تم إنشاء هذا الدليل في: **2024**

**آخر تحديث:** تاريخ آخر تعديل

---

## 📄 الرخصة

هذا الدليل مخصص للاستخدام الداخلي فقط.

