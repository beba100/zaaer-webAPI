# 🎯 VoM Journal Entry Routing Architecture
## نظام توجيه قيود VoM حسب الفندق ومراكز التكلفة

---

## 📋 **نظرة عامة (Overview)**

نظام متقدم لتوجيه قيود VoM تلقائياً بناءً على:
- **الفندق (Hotel)**: يتم تحديده من `X-Hotel-Code` أو `HotelId`
- **مراكز التكلفة (Cost Centers)**: مثل "صندوق الدمام 1" (ID: 67) لكل فندق
- **نوع العملية**: Create, Update, Delete

---

## 🏗️ **البنية المعمارية المقترحة (Proposed Architecture)**

### **1. جدول Mapping في MasterDB**

```sql
CREATE TABLE hotel_vom_cost_center_mapping (
    id INT IDENTITY(1,1) PRIMARY KEY,
    hotel_id INT NOT NULL,                    -- معرف الفندق
    tenant_code NVARCHAR(50) NULL,            -- كود الفندق (Dammam1, Dammam2)
    
    -- VoM Cost Center Account (الصندوق/مركز التكلفة)
    vom_cost_center_account_id INT NOT NULL,  -- VoM Account ID (مثل: 67 لصندوق الدمام 1)
    vom_cost_center_code NVARCHAR(50) NULL,   -- VoM Account Code (مثل: 012-1-1-1)
    
    -- Account Type Mappings (حسابات أخرى)
    vom_cash_account_id INT NULL,             -- حساب النقد (Cash)
    vom_bank_account_id INT NULL,             -- حساب البنك (Bank)
    vom_customer_account_id INT NULL,         -- حساب العميل (Customer)
    vom_sales_tax_account_id INT NULL,        -- حساب ضريبة المبيعات (Sales Tax)
    vom_lodging_tax_account_id INT NULL,      -- حساب ضريبة الإقامة (Lodging Tax)
    vom_revenue_account_id INT NULL,          -- حساب الإيرادات (Revenue)
    vom_expense_account_id INT NULL,          -- حساب المصروفات (Expense)
    vom_security_deposit_account_id INT NULL, -- حساب التأمينات (Security Deposit)
    
    -- Configuration
    is_active BIT NOT NULL DEFAULT 1,
    is_default BIT NOT NULL DEFAULT 0,        -- Default mapping for hotel
    
    -- Metadata
    description NVARCHAR(500) NULL,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    updated_at DATETIME2 NULL,
    
    -- Foreign Key to Tenants (optional)
    FOREIGN KEY (hotel_id) REFERENCES Tenants(Id)
);
```

### **2. Service Layer Architecture**

```
┌─────────────────────────────────────────────────────────────┐
│              VoMJournalEntryRoutingService                   │
├─────────────────────────────────────────────────────────────┤
│  • ResolveCostCenter(hotelId) → VoM Account ID              │
│  • MapPaymentReceiptToJournalEntry(receipt) → Journal Entry │
│  • CreateJournalEntry(receipt) → VoM API                    │
│  • UpdateJournalEntry(receipt) → VoM API (Delete + Create)  │
│  • DeleteJournalEntry(receipt) → VoM API                    │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│         HotelVoMCostCenterMappingService                     │
├─────────────────────────────────────────────────────────────┤
│  • GetMappingByHotelId(hotelId) → Mapping                   │
│  • GetCostCenterAccount(hotelId) → VoM Account ID           │
│  • GetAccountByType(hotelId, type) → VoM Account ID         │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              MasterDbContext (hotel_vom_cost_center_mapping) │
└─────────────────────────────────────────────────────────────┘
```

### **3. Integration Points**

#### **A. PaymentReceipt Create Flow:**
```
POST /api/zaaer/PaymentReceipt
  ↓
ZaaerPaymentReceiptService.CreatePaymentReceiptAsync()
  ↓
PaymentReceipt Created & Saved
  ↓
VoMJournalEntryRoutingService.CreateJournalEntryAsync(receipt)
  ↓
  ├─→ Resolve Cost Center (HotelId → VoM Account ID)
  ├─→ Map PaymentReceipt → Journal Entry Lines
  ├─→ Balance Journal Entry
  └─→ POST /api/accounting/journal-entries (VoM API)
```

#### **B. PaymentReceipt Update Flow:**
```
PUT /api/zaaer/PaymentReceipt/{id}
  ↓
ZaaerPaymentReceiptService.UpdatePaymentReceiptAsync()
  ↓
PaymentReceipt Updated & Saved
  ↓
VoMJournalEntryRoutingService.UpdateJournalEntryAsync(receipt)
  ↓
  ├─→ Find existing VoM Journal Entry (by ReceiptNo)
  ├─→ DELETE old Journal Entry (VoM API)
  └─→ CREATE new Journal Entry (VoM API)
```

#### **C. PaymentReceipt Delete Flow:**
```
DELETE /api/zaaer/PaymentReceipt/{id}
  ↓
ZaaerPaymentReceiptService.DeletePaymentReceiptAsync()
  ↓
VoMJournalEntryRoutingService.DeleteJournalEntryAsync(receipt)
  ↓
  └─→ DELETE Journal Entry (VoM API)
```

---

## 🔄 **Routing Logic (منطق التوجيه)**

### **Scenario 1: Customer Payment Receipt**

**Input:**
- HotelId: 1 (Dammam1)
- PaymentMethod: "Cash" or BankId: 5
- AmountPaid: 100
- CustomerId: 123

**Routing:**
1. **Resolve Cost Center:**
   - HotelId 1 → Lookup `hotel_vom_cost_center_mapping`
   - Get `vom_cost_center_account_id` = 67 (صندوق الدمام 1)

2. **Resolve Accounts:**
   - Cash Account: `vom_cash_account_id` or default
   - Customer Account: `vom_customer_account_id` or lookup from CustomerId

3. **Create Journal Entry:**
   ```
   Line 1 (Debit):
     - Account ID: 67 (صندوق الدمام 1) - Cost Center
     - Debit: 100
     - Credit: 0
   
   Line 2 (Credit):
     - Account ID: [Customer Account ID]
     - Debit: 0
     - Credit: 100
   ```

### **Scenario 2: Payment Receipt with Taxes**

**Input:**
- HotelId: 1
- AmountPaid: 100
- SalesTax: 13.04
- LodgingTax: 2.12

**Routing:**
1. **Resolve Accounts:**
   - Cost Center: 67 (صندوق الدمام 1)
   - Sales Tax: `vom_sales_tax_account_id` = 17
   - Lodging Tax: `vom_lodging_tax_account_id` = 65
   - Revenue: `vom_revenue_account_id` = 28

2. **Create Journal Entry:**
   ```
   Line 1 (Debit):
     - Account ID: 67 (صندوق الدمام 1)
     - Debit: 100
     - Credit: 0
   
   Line 2 (Credit):
     - Account ID: 17 (ضريبة المبيعات)
     - Debit: 0
     - Credit: 13.04
   
   Line 3 (Credit):
     - Account ID: 65 (ضريبة الإقامة)
     - Debit: 0
     - Credit: 2.12
   
   Line 4 (Credit):
     - Account ID: 28 (الإيرادات)
     - Debit: 0
     - Credit: 84.84
   ```

---

## 📊 **Database Schema**

### **Table: hotel_vom_cost_center_mapping**

| Column | Type | Description |
|--------|------|-------------|
| `id` | INT | Primary Key |
| `hotel_id` | INT | Foreign Key to Tenants |
| `tenant_code` | NVARCHAR(50) | Hotel Code (Dammam1, Dammam2) |
| `vom_cost_center_account_id` | INT | **VoM Account ID for Cost Center** (مثل: 67) |
| `vom_cost_center_code` | NVARCHAR(50) | VoM Account Code (مثل: 012-1-1-1) |
| `vom_cash_account_id` | INT | Cash Account ID |
| `vom_bank_account_id` | INT | Bank Account ID |
| `vom_customer_account_id` | INT | Default Customer Account ID |
| `vom_sales_tax_account_id` | INT | Sales Tax Account ID (17) |
| `vom_lodging_tax_account_id` | INT | Lodging Tax Account ID (65) |
| `vom_revenue_account_id` | INT | Revenue Account ID (28) |
| `vom_expense_account_id` | INT | Expense Account ID |
| `vom_security_deposit_account_id` | INT | Security Deposit Account ID |
| `is_active` | BIT | Active/Inactive |
| `is_default` | BIT | Default mapping for hotel |
| `description` | NVARCHAR(500) | Description |
| `created_at` | DATETIME2 | Created timestamp |
| `updated_at` | DATETIME2 | Updated timestamp |

---

## 🎯 **Key Features**

### **1. Automatic Routing**
- ✅ Automatically routes based on `HotelId` from PaymentReceipt
- ✅ Resolves cost center from mapping table
- ✅ Supports multiple hotels with different cost centers

### **2. Flexible Account Mapping**
- ✅ Per-hotel account configuration
- ✅ Default accounts (Cash, Bank, Customer, etc.)
- ✅ Override per hotel if needed

### **3. Operation Support**
- ✅ **POST**: Create Journal Entry after PaymentReceipt creation
- ✅ **PUT**: Delete old + Create new Journal Entry (VoM limitation)
- ✅ **DELETE**: Delete Journal Entry from VoM

### **4. Error Handling**
- ✅ Graceful failure (log error, don't block PaymentReceipt)
- ✅ Retry mechanism (optional)
- ✅ Audit trail (log all VoM operations)

---

## 🔧 **Implementation Plan**

### **Phase 1: Database & Models**
1. Create `hotel_vom_cost_center_mapping` table in MasterDB
2. Create C# Model: `HotelVoMCostCenterMapping`
3. Add to `MasterDbContext`

### **Phase 2: Services**
1. Create `IHotelVoMCostCenterMappingService`
2. Create `HotelVoMCostCenterMappingService`
3. Create `IVoMJournalEntryRoutingService`
4. Create `VoMJournalEntryRoutingService`

### **Phase 3: Integration**
1. Hook into `ZaaerPaymentReceiptService.CreatePaymentReceiptAsync()`
2. Hook into `ZaaerPaymentReceiptService.UpdatePaymentReceiptAsync()`
3. Hook into Delete operations

### **Phase 4: Configuration**
1. Create UI/API to manage hotel → VoM mappings
2. Seed initial data (Hotel 1 → صندوق الدمام 1, etc.)

---

## 💡 **Advanced Features (Future)**

1. **Dynamic Cost Center Selection:**
   - Based on PaymentMethod (Cash → صندوق الدمام 1, Bank → بنك البلاد)
   - Based on BankId (Different banks → Different cost centers)

2. **Multi-Cost Center Support:**
   - Split amounts across multiple cost centers
   - Percentage-based distribution

3. **Audit & Reconciliation:**
   - Track all VoM Journal Entries created
   - Reconciliation reports
   - Re-sync capability

---

## ❓ **Questions to Clarify**

1. **Cost Center Selection:**
   - Should it be based on `PaymentMethod` (Cash vs Bank)?
   - Or always use the default cost center for the hotel?

2. **Multiple Cost Centers:**
   - Can one hotel have multiple cost centers?
   - How to select which one to use?

3. **Account Mapping:**
   - Should we map `BankId` → VoM Bank Account?
   - Or use a default bank account per hotel?

4. **Error Handling:**
   - If VoM API fails, should we:
     - Block PaymentReceipt creation? (Strict)
     - Allow PaymentReceipt but log error? (Flexible) ✅ Recommended

---

## ✅ **Recommended Approach**

**Flexible & Non-Blocking:**
- ✅ Allow PaymentReceipt to be created even if VoM fails
- ✅ Log all errors for debugging
- ✅ Provide retry mechanism
- ✅ Store VoM Journal Entry ID in PaymentReceipt (for tracking)

**Default Behavior:**
- ✅ Use hotel's default cost center (صندوق الدمام 1 for Hotel 1)
- ✅ Support override per PaymentMethod if needed
- ✅ Fallback to default accounts if mapping not found

---

## 📝 **Next Steps**

1. **Confirm Architecture** ✅
2. **Create Database Table** 
3. **Implement Services**
4. **Integrate with PaymentReceipt**
5. **Test & Deploy**

---

**Ready to implement?** 🚀
