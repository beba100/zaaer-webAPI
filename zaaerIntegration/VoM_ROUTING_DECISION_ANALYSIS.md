# 🤔 VoM Routing Decision Analysis
## تحليل قرارات توجيه قيود VoM

---

## 📊 **البنية الحالية (Current Architecture)**

### **1. Flow الحالي:**

```
HTTP Request
  ↓
X-Hotel-Code: "Dammam1" (Header)
  ↓
TenantService.GetTenant()
  ↓
MasterDB → Tenants Table
  ├─→ Id: 2
  ├─→ Code: "Dammam1" ✅
  └─→ DatabaseName: "db32416_Dammam2"
  ↓
Tenant DB (db32416_Dammam2) → hotel_settings Table
  ├─→ hotel_id: 1 (في قاعدة بيانات الفندق)
  └─→ hotel_code: "Dammam1" ✅ (يساوي Tenants.Code)
  ↓
PaymentReceipt
  └─→ hotel_id: 1 (من hotel_settings)
```

### **2. العلاقات:**

| Location | Field | Value | Purpose |
|----------|-------|-------|---------|
| **MasterDB.Tenants** | `Id` | 2 | معرف الفندق في MasterDB |
| **MasterDB.Tenants** | `Code` | "Dammam1" | **كود الفندق (المفتاح المشترك)** ✅ |
| **Tenant DB.hotel_settings** | `hotel_id` | 1 | معرف الفندق في قاعدة بيانات الفندق |
| **Tenant DB.hotel_settings** | `hotel_code` | "Dammam1" | **كود الفندق (يساوي Tenants.Code)** ✅ |
| **Tenant DB.payment_receipts** | `hotel_id` | 1 | معرف الفندق (من hotel_settings) |

---

## 🎯 **الخيارات المتاحة (Available Options)**

### **Option 1: Mapping بـ `Code` (Tenants.Code = hotel_settings.hotel_code)** ✅ **مُوصى به**

**المزايا:**
- ✅ `Code` هو المفتاح المشترك بين MasterDB و Tenant DB
- ✅ `Code` ثابت ولا يتغير (مثل "Dammam1")
- ✅ `Code` موجود في MasterDB (سهل الوصول)
- ✅ `Code` موجود في كل Tenant DB (hotel_code)
- ✅ لا يعتمد على `hotel_id` الذي يختلف بين قواعد البيانات

**البنية:**
```sql
CREATE TABLE hotel_vom_cost_center_mapping (
    id INT IDENTITY(1,1) PRIMARY KEY,
    tenant_code NVARCHAR(50) NOT NULL UNIQUE,  -- ✅ المفتاح الأساسي (Dammam1, Dammam2)
    
    -- VoM Cost Center Account
    vom_cost_center_account_id INT NOT NULL,   -- VoM Account ID (67 لصندوق الدمام 1)
    vom_cost_center_code NVARCHAR(50) NULL,    -- VoM Account Code (012-1-1-1)
    
    -- Account Type Mappings
    vom_cash_account_id INT NULL,
    vom_bank_account_id INT NULL,
    vom_customer_account_id INT NULL,
    vom_sales_tax_account_id INT NULL,         -- 17
    vom_lodging_tax_account_id INT NULL,       -- 65
    vom_revenue_account_id INT NULL,           -- 28
    vom_expense_account_id INT NULL,
    vom_security_deposit_account_id INT NULL,
    
    -- Configuration
    is_active BIT NOT NULL DEFAULT 1,
    description NVARCHAR(500) NULL,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    updated_at DATETIME2 NULL,
    
    -- Foreign Key (optional, for validation)
    FOREIGN KEY (tenant_code) REFERENCES Tenants(Code)
);
```

**Flow:**
```
PaymentReceipt Created
  ↓
Get hotel_id from PaymentReceipt
  ↓
Query Tenant DB → hotel_settings WHERE hotel_id = ?
  ↓
Get hotel_code = "Dammam1"
  ↓
Query MasterDB → hotel_vom_cost_center_mapping WHERE tenant_code = "Dammam1"
  ↓
Get vom_cost_center_account_id = 67
  ↓
Create VoM Journal Entry
```

---

### **Option 2: Mapping بـ `hotel_id` (من Tenant DB)**

**المشاكل:**
- ❌ `hotel_id` يختلف بين قواعد البيانات (Hotel 1 في DB1 = hotel_id 1، Hotel 1 في DB2 = hotel_id 1 أيضاً)
- ❌ `hotel_id` غير موجود في MasterDB
- ❌ يحتاج إلى Query في Tenant DB أولاً للحصول على `hotel_code`
- ❌ معقد وغير موثوق

**الخلاصة:** ❌ **غير مُوصى به**

---

### **Option 3: Mapping بـ `Tenants.Id` (MasterDB)**

**المشاكل:**
- ❌ `Tenants.Id` غير موجود في PaymentReceipt
- ❌ يحتاج إلى Query إضافي للحصول على `Tenants.Id` من `hotel_code`
- ❌ معقد وغير مباشر

**الخلاصة:** ❌ **غير مُوصى به**

---

## ✅ **القرار المُوصى به (Recommended Decision)**

### **استخدام `tenant_code` (Code) كـ Primary Key:**

```sql
-- جدول Mapping في MasterDB
CREATE TABLE hotel_vom_cost_center_mapping (
    id INT IDENTITY(1,1) PRIMARY KEY,
    tenant_code NVARCHAR(50) NOT NULL UNIQUE,  -- ✅ Dammam1, Dammam2, etc.
    
    -- VoM Cost Center
    vom_cost_center_account_id INT NOT NULL,   -- 67 (صندوق الدمام 1)
    vom_cost_center_code NVARCHAR(50) NULL,    -- 012-1-1-1
    
    -- Account Mappings
    vom_sales_tax_account_id INT NULL,         -- 17
    vom_lodging_tax_account_id INT NULL,       -- 65
    vom_revenue_account_id INT NULL,           -- 28
    -- ... etc
    
    is_active BIT NOT NULL DEFAULT 1,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    updated_at DATETIME2 NULL
);
```

---

## 🔄 **Routing Logic (منطق التوجيه)**

### **Scenario: PaymentReceipt Created**

```csharp
// 1. PaymentReceipt موجود في Tenant DB
PaymentReceipt receipt = ...; // hotel_id = 1

// 2. الحصول على hotel_code من Tenant DB
var hotelSettings = await tenantDbContext.HotelSettings
    .FirstOrDefaultAsync(h => h.HotelId == receipt.HotelId);
// hotelSettings.hotel_code = "Dammam1"

// 3. البحث في MasterDB عن Mapping
var mapping = await masterDbContext.HotelVoMCostCenterMappings
    .FirstOrDefaultAsync(m => m.TenantCode == hotelSettings.HotelCode);
// mapping.vom_cost_center_account_id = 67

// 4. إنشاء Journal Entry في VoM
var journalEntry = new VoMJournalEntryRequestDto {
    Accounts = new List<VoMJournalEntryAccountDto> {
        new() { Id = 67, Debit = 100, Credit = 0 },  // صندوق الدمام 1
        new() { Id = 17, Debit = 0, Credit = 13.04 }, // ضريبة المبيعات
        // ... etc
    }
};
```

---

## ❓ **أسئلة للتفكير (Questions to Consider)**

### **1. متى يتم إنشاء Journal Entry؟**

**Option A: تلقائياً بعد إنشاء PaymentReceipt** ✅ **مُوصى به**
- ✅ فوري
- ✅ لا يحتاج تدخل يدوي
- ⚠️ إذا فشل VoM API، ماذا نفعل؟

**Option B: يدوياً عبر API منفصل**
- ✅ تحكم كامل
- ❌ يحتاج تدخل يدوي
- ❌ قد ينسى المستخدم

**Option C: عبر Queue/Background Job**
- ✅ لا يبطئ PaymentReceipt
- ✅ يمكن Retry
- ⚠️ معقد أكثر

---

### **2. ماذا لو فشل VoM API؟**

**Option A: Block PaymentReceipt Creation** ❌
- ❌ يمنع العملية الأساسية
- ❌ تجربة مستخدم سيئة

**Option B: Allow PaymentReceipt, Log Error** ✅ **مُوصى به**
- ✅ لا يمنع العملية
- ✅ يمكن Retry لاحقاً
- ✅ Log للتحليل

**Option C: Store in Queue for Retry**
- ✅ Automatic Retry
- ⚠️ معقد أكثر

---

### **3. كيف نحدد الحسابات (Accounts)؟**

**السؤال:** من أين نحصل على:
- Sales Tax Account ID (17)
- Lodging Tax Account ID (65)
- Revenue Account ID (28)
- Customer Account ID

**الخيارات:**

**A. من Mapping Table (Per Hotel)** ✅ **مُوصى به**
```sql
hotel_vom_cost_center_mapping:
  tenant_code = "Dammam1"
  vom_sales_tax_account_id = 17
  vom_lodging_tax_account_id = 65
  vom_revenue_account_id = 28
```

**B. من PaymentReceipt (إذا كان موجود)**
- ⚠️ قد لا يكون موجود في PaymentReceipt

**C. Default Values (Hardcoded)**
- ⚠️ غير مرن
- ❌ لا يدعم حسابات مختلفة لكل فندق

---

### **4. كيف نتعامل مع Update/Delete؟**

**VoM API Limitation:** لا يدعم Update، فقط Create + Delete

**الحل:**
```
Update PaymentReceipt:
  1. Delete old Journal Entry (VoM API)
  2. Create new Journal Entry (VoM API)
```

**السؤال:** كيف نجد Journal Entry القديم؟
- ✅ Store `vom_journal_entry_id` في PaymentReceipt
- ✅ أو البحث بـ `ReceiptNo` في VoM

---

## 🎯 **التوصيات النهائية (Final Recommendations)**

### **1. Database Schema:**
```sql
✅ Use tenant_code (NVARCHAR(50)) as Primary Key
✅ Store in MasterDB
✅ Foreign Key to Tenants(Code)
```

### **2. Routing Logic:**
```
✅ PaymentReceipt.hotel_id → hotel_settings.hotel_code
✅ hotel_code → hotel_vom_cost_center_mapping.tenant_code
✅ Get vom_cost_center_account_id
```

### **3. Integration:**
```
✅ Auto-create Journal Entry after PaymentReceipt creation
✅ Non-blocking (log error if VoM fails)
✅ Store vom_journal_entry_id in PaymentReceipt (new column)
```

### **4. Error Handling:**
```
✅ Allow PaymentReceipt creation even if VoM fails
✅ Log all errors
✅ Provide retry mechanism (optional)
```

---

## 📝 **Next Steps**

1. ✅ **Confirm:** استخدام `tenant_code` كـ Primary Key
2. ✅ **Confirm:** Auto-create Journal Entry (non-blocking)
3. ✅ **Confirm:** Store `vom_journal_entry_id` في PaymentReceipt
4. ⏳ **Implement:** Database Table
5. ⏳ **Implement:** Services
6. ⏳ **Implement:** Integration

---

**ما رأيك في هذه التوصيات؟** 🤔
