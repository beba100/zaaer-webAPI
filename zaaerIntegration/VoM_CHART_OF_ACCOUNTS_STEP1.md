# ✅ VoM Chart of Accounts - Step 1 Complete
## دليل الحسابات - الخطوة الأولى مكتملة

---

## 🎯 **ما تم إنجازه:**

### **1. إنشاء جدول `chart_of_accounts` في Master DB:**

**الملف:** `Database/CreateChartOfAccountsTable.sql`

**الأعمدة الرئيسية:**
- `id` - Primary Key
- `code` - كود الحساب (مثل: 01, 02, 011, 011-1)
- `name_ar` - الاسم بالعربية
- `name_en` - الاسم بالإنجليزية
- `parent_id` - الحساب الأب (للحسابات الفرعية)
- `level` - المستوى (1 = Root, 2 = Sub, etc.)
- `path` - المسار الكامل (مثل: 01/011/011-1)
- `account_type` - نوع الحساب: Assets, Liabilities, Equity, Revenue, Expenses
- `vom_account_code` - كود الحساب في VoM (للربط)
- `vom_account_id` - ID الحساب في VoM
- `is_active`, `is_main`, `is_system` - Flags
- `default_transaction_type` - debit أو credit
- `currency_code` - العملة (افتراضي: SAR)

**Indexes:**
- ✅ `IX_chart_of_accounts_code` (Unique)
- ✅ `IX_chart_of_accounts_parent_id`
- ✅ `IX_chart_of_accounts_account_type`
- ✅ `IX_chart_of_accounts_level`
- ✅ `IX_chart_of_accounts_vom_account_code`

**Foreign Key:**
- ✅ `FK_chart_of_accounts_parent` (Self-referencing)

---

### **2. إدراج المستويات الجذرية (Root Level Accounts):**

تم إدراج 5 حسابات رئيسية:

| Code | Name (AR) | Name (EN) | Account Type | Default Transaction |
|------|-----------|-----------|--------------|---------------------|
| `01` | الاصول | Assets | Assets | debit |
| `02` | الخصوم | Liabilities | Liabilities | credit |
| `03` | حقوق الملكية | Owner's Equity | Equity | credit |
| `04` | الايرادات | Revenue | Revenue | credit |
| `05` | المصروفات | Expenses | Expenses | debit |

**جميع الحسابات الجذرية:**
- ✅ `is_system = 1` (لا يمكن حذفها)
- ✅ `is_main = 1` (حسابات رئيسية)
- ✅ `is_active = 1` (نشطة)
- ✅ `level = 1` (مستوى جذري)

---

### **3. إنشاء Model في C#:**

**الملف:** `Models/VoM/ChartOfAccount.cs`

**المميزات:**
- ✅ جميع الأعمدة مع Data Annotations
- ✅ Navigation Properties (Parent, Children)
- ✅ Foreign Key Configuration

---

### **4. إضافة DbSet في MasterDbContext:**

**الملف:** `Data/MasterDbContext.cs`

**ما تم إضافته:**
- ✅ `DbSet<ChartOfAccount> ChartOfAccounts { get; set; }`
- ✅ Configuration في `OnModelCreating`:
  - Table mapping
  - Indexes configuration
  - Foreign Key configuration
  - Self-referencing relationship

---

## 📋 **الخطوات التالية (Step 2):**

### **1. تنفيذ SQL Script:**
```sql
-- تشغيل الملف:
-- Database/CreateChartOfAccountsTable.sql
-- في Master DB: db32357_MasterDB
```

### **2. التحقق من البيانات:**
```sql
-- التحقق من الحسابات الجذرية:
SELECT * FROM chart_of_accounts WHERE level = 1 ORDER BY code;
```

### **3. الخطوة التالية:**
- [ ] إنشاء Service للتعامل مع Chart of Accounts
- [ ] إنشاء Controller للـ API endpoints
- [ ] ربط Chart of Accounts مع VoM Accounts
- [ ] إنشاء Mapping Service للربط بين PaymentReceipt و Chart of Accounts

---

## 🗄️ **بنية الجدول:**

```
chart_of_accounts
├── Level 1 (Root):
│   ├── 01 - الاصول (Assets)
│   ├── 02 - الخصوم (Liabilities)
│   ├── 03 - حقوق الملكية (Owner's Equity)
│   ├── 04 - الايرادات (Revenue)
│   └── 05 - المصروفات (Expenses)
│
└── Level 2+ (Sub Accounts):
    └── سيتم إضافتها لاحقاً
```

---

## 🔗 **الربط مع VoM:**

- `vom_account_code` - للربط مع VoM Account Code
- `vom_account_id` - للربط مع VoM Account ID
- يمكن ربط Chart of Accounts مع VoM Accounts من جدول `vom_accounts`

---

## ✅ **الحالة:**

- ✅ **Step 1 Complete:** جدول Chart of Accounts تم إنشاؤه
- ⏳ **Step 2 Pending:** تنفيذ SQL Script والتحقق
- ⏳ **Step 3 Pending:** إنشاء Services و Controllers

---

**Last Updated:** 2025-01-11  
**Status:** ✅ **Step 1 Complete - Ready for Step 2**
