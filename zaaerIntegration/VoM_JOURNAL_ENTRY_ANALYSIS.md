# 📋 VoM Journal Entry Integration - Analysis & Plan
## تحليل وتخطيط تكامل VoM Journal Entry

---

## 🎯 **الهدف:**
إضافة تكامل مع VoM API لإنشاء Journal Entry تلقائياً بعد:
1. `POST /api/zaaer/PaymentReceipt` (إنشاء Payment Receipt)
2. `PUT /api/zaaer/PaymentReceipt/zaaer/{zaaerId}` (تحديث Payment Receipt)

---

## 📊 **من الصورة (GUI):**

### **Journal Entry Structure:**
```
Journal Entry:
├── Entry Number (رقم القيد)
├── Date (التاريخ)
├── Details (التفاصيل) - نص عام للقيد
└── Lines (السطور):
    ├── Account (الحساب) - dropdown
    ├── Debit (مدين) - رقم
    ├── Credit (دائن) - رقم
    ├── Details (التفاصيل) - نص للسطر
    ├── Beneficiary (المستفيد) - نص
    ├── Cost Center (مراكز التكلفة) - dropdown
    ├── Tax Status (حالة الضريبة) - dropdown
    └── Taxes (الضرائب) - dropdown
```

---

## 🔍 **ما يجب تحديده قبل كتابة الكود:**

### **1. Mapping PaymentReceipt → Journal Entry:**

#### **A. Entry Header:**
- **Entry Number:** من أين؟ 
  - ✅ `ReceiptNo` من PaymentReceipt؟
  - ⚠️ أم يحتاج رقم قيد منفصل؟
  
- **Date:** 
  - ✅ `ReceiptDate` من PaymentReceipt

- **Details (التفاصيل العامة):**
  - ✅ `Notes` من PaymentReceipt
  - ✅ أو نص ثابت مثل "سند قبض رقم {ReceiptNo}"

#### **B. Journal Entry Lines:**

**السؤال الأساسي: ما هي الحسابات المستخدمة؟**

**سيناريو 1: سند قبض عادي (Customer Payment)**
```
Line 1 (Debit):
  - Account: Bank Account أو Cash Account (حسب PaymentMethod)
  - Debit: AmountPaid
  - Credit: 0
  - Details: "سند قبض رقم {ReceiptNo}"
  - Beneficiary: Customer Name
  - Cost Center: ???

Line 2 (Credit):
  - Account: Customer Account (حساب العميل)
  - Debit: 0
  - Credit: AmountPaid
  - Details: "سند قبض رقم {ReceiptNo}"
  - Beneficiary: Customer Name
  - Cost Center: ???
```

**سيناريو 2: سند قبض تأمين (Security Deposit)**
```
Line 1 (Debit):
  - Account: Bank Account أو Cash Account
  - Debit: AmountPaid
  - Credit: 0
  - Details: "سند قبض تأمين رقم {ReceiptNo}"
  - Beneficiary: Customer Name
  - Cost Center: ???

Line 2 (Credit):
  - Account: Security Deposit Account (حساب التأمينات)
  - Debit: 0
  - Credit: AmountPaid
  - Details: "سند قبض تأمين رقم {ReceiptNo}"
  - Beneficiary: Customer Name
  - Cost Center: ???
```

**سيناريو 3: سند مصروف (Expense Receipt)**
```
Line 1 (Debit):
  - Account: Expense Account (حساب المصروفات)
  - Debit: AmountPaid
  - Credit: 0
  - Details: "سند مصروف رقم {ReceiptNo}"
  - Beneficiary: ???
  - Cost Center: ???

Line 2 (Credit):
  - Account: Bank Account أو Cash Account
  - Debit: 0
  - Credit: AmountPaid
  - Details: "سند مصروف رقم {ReceiptNo}"
  - Beneficiary: ???
  - Cost Center: ???
```

---

### **2. تحديد الحسابات (Accounts):**

**يجب معرفة:**
- ✅ **Bank Account:** من `BankId` في PaymentReceipt
  - ⚠️ كيف نحصل على VoM Account Code من BankId؟
  - ⚠️ هل يوجد mapping بين Banks و VoM Accounts؟

- ✅ **Cash Account:** عند `PaymentMethod = "Cash"` أو `PaymentMethodId = null`
  - ⚠️ ما هو VoM Account Code للحساب النقدي؟

- ✅ **Customer Account:** من `CustomerId`
  - ⚠️ كيف نحصل على VoM Account Code من CustomerId؟
  - ⚠️ هل يوجد mapping بين Customers و VoM Accounts؟

- ✅ **Security Deposit Account:** عند `ReceiptType = "security_deposit"`
  - ⚠️ ما هو VoM Account Code لحساب التأمينات؟

- ✅ **Expense Account:** عند `VoucherCode = "expense"`
  - ⚠️ ما هو VoM Account Code لحساب المصروفات؟

---

### **3. Cost Center (مراكز التكلفة):**

**السؤال: كيف نحدد Cost Center لكل سطر؟**

**الخيارات:**
1. **من HotelId:**
   - ✅ كل فندق له Cost Center خاص
   - ⚠️ هل يوجد mapping بين Hotels و VoM Cost Centers؟

2. **من ReservationId:**
   - ✅ كل حجز قد يكون له Cost Center (مثل: قسم الإقامة)
   - ⚠️ هل يوجد Cost Center في Reservation؟

3. **من UnitId:**
   - ✅ كل وحدة قد يكون لها Cost Center (مثل: قسم معين)
   - ⚠️ هل يوجد Cost Center في Unit/Apartment؟

4. **Default Cost Center:**
   - ✅ "المركز الرئيسي" (Main Cost Center) كقيمة افتراضية
   - ⚠️ ما هو Cost Center الافتراضي في VoM؟

**التوصية:**
- ✅ استخدام HotelId كأساس لتحديد Cost Center
- ✅ إذا لم يوجد mapping، استخدام Cost Center افتراضي
- ⚠️ يحتاج جدول mapping: `HotelId → VoM Cost Center Code/ID`

---

### **4. Tax Status & Taxes:**

**من الصورة:**
- Tax Status: "بدون ضريبة" | "شامل الضرائب" | "غير شامل الضرائب"
- Taxes: dropdown (فارغ في الصورة)

**السؤال:**
- ⚠️ هل PaymentReceipt يحتوي على معلومات ضريبية؟
- ⚠️ هل نحتاج حساب الضريبة من `AmountPaid`؟
- ⚠️ ما هي القيمة الافتراضية؟ ("بدون ضريبة"؟)

**التوصية:**
- ✅ افتراضي: "بدون ضريبة" (No Tax)
- ⚠️ إذا كان PaymentReceipt يحتوي على VAT، نحتاج حساب الضريبة

---

### **5. Beneficiary (المستفيد):**

**السؤال: من هو المستفيد؟**

**الخيارات:**
- ✅ Customer Name (من CustomerId)
- ✅ أو نص ثابت مثل "عميل" أو "مورد"
- ⚠️ هل نحتاج جلب Customer Name من قاعدة البيانات؟

---

### **6. متى يتم استدعاء VoM API؟**

#### **A. بعد Create PaymentReceipt:**
```csharp
// في ZaaerPaymentReceiptService.CreatePaymentReceiptAsync
var createdPaymentReceipt = await _paymentReceiptRepository.AddAsync(paymentReceipt);
await _unitOfWork.SaveChangesAsync();

await _customerLedgerService.SyncReceiptAsync(createdPaymentReceipt);

// ✅ هنا: استدعاء VoM API لإنشاء Journal Entry
await _vomJournalEntryService.CreateJournalEntryAsync(createdPaymentReceipt);

return _mapper.Map<ZaaerPaymentReceiptResponseDto>(createdPaymentReceipt);
```

#### **B. بعد Update PaymentReceipt:**
```csharp
// في ZaaerPaymentReceiptService.UpdatePaymentReceiptAsync
// بعد التحديث
await _unitOfWork.SaveChangesAsync();

// ✅ هنا: استدعاء VoM API لتحديث Journal Entry
// ⚠️ السؤال: هل VoM يدعم Update Journal Entry؟ = no it support Create and Delete only (BEBA)
// ⚠️ أم نحتاج Delete + Create جديد؟= YES 
await _vomJournalEntryService.UpdateJournalEntryAsync(existingPaymentReceipt);
```

---

## 📝 **ما يجب فعله قبل كتابة الكود:**

### **1. فحص VoM API Documentation:**
- [ ] قراءة `/api/journal-entry/create` endpoint
- [ ] معرفة Request Body Structure
- [ ] معرفة Response Structure
- [ ] معرفة Authentication Requirements
- [ ] معرفة Error Handling

### **2. تحديد Account Mapping:**
- [ ] إنشاء جدول/configuration لـ Account Mapping:
  ```
  PaymentMethod → VoM Account Code
  BankId → VoM Account Code
  ReceiptType → VoM Account Code (Security Deposit)
  VoucherCode → VoM Account Code (Expense)
  ```

### **3. تحديد Cost Center Mapping:**
- [ ] إنشاء جدول/configuration لـ Cost Center Mapping:
  ```
  HotelId → VoM Cost Center Code/ID
  Default Cost Center Code/ID
  ```

### **4. تحديد Business Rules:**
- [ ] متى نستخدم Bank Account vs Cash Account؟
- [ ] متى نستخدم Customer Account vs Security Deposit Account vs Expense Account؟
- [ ] كيف نحدد Tax Status؟
- [ ] كيف نحدد Beneficiary؟

### **5. Error Handling:**
- [ ] ماذا نفعل إذا فشل إنشاء Journal Entry في VoM؟
- [ ] هل نرجع خطأ أم نكمل بدون Journal Entry؟
- [ ] هل نحتاج Retry Logic؟
- [ ] هل نحتاج Logging للفشل؟

### **6. Idempotency:**
- [ ] كيف نتأكد من عدم إنشاء Journal Entry مكرر؟
- [ ] هل نحتاج حفظ VoM Journal Entry ID في PaymentReceipt؟
- [ ] كيف نتعامل مع Retry/Webhook retries؟

---

## 🏗️ **البنية المقترحة:**

### **1. VoM Journal Entry Service:**
```csharp
public interface IVoMJournalEntryService
{
    Task<VoMJournalEntryResponseDto> CreateJournalEntryAsync(PaymentReceipt paymentReceipt);
    Task<VoMJournalEntryResponseDto?> UpdateJournalEntryAsync(PaymentReceipt paymentReceipt);
    Task<bool> DeleteJournalEntryAsync(string entryNumber);
}
```

### **2. Account Mapping Service:**
```csharp
public interface IVoMAccountMappingService
{
    Task<string?> GetBankAccountCodeAsync(int? bankId);
    Task<string?> GetCashAccountCodeAsync();
    Task<string?> GetCustomerAccountCodeAsync(int customerId);
    Task<string?> GetSecurityDepositAccountCodeAsync();
    Task<string?> GetExpenseAccountCodeAsync();
}
```

### **3. Cost Center Mapping Service:**
```csharp
public interface IVoMCostCenterMappingService
{
    Task<string?> GetCostCenterCodeAsync(int hotelId);
    Task<string?> GetDefaultCostCenterCodeAsync();
}
```

### **4. DTOs:**
```csharp
// VoMJournalEntryDto.cs
public class VoMJournalEntryDto
{
    public string EntryNumber { get; set; }
    public DateTime Date { get; set; }
    public string? Details { get; set; }
    public List<VoMJournalEntryLineDto> Lines { get; set; }
}

public class VoMJournalEntryLineDto
{
    public string AccountCode { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Details { get; set; }
    public string? Beneficiary { get; set; }
    public string? CostCenterCode { get; set; }
    public string? TaxStatus { get; set; }
    public string? Taxes { get; set; }
}
```

---

## ✅ **الخطوات التالية:**

1. **قراءة VoM API Documentation:**
   - [ ] فحص `/api/journal-entry/create` endpoint
   - [ ] معرفة Request/Response structure

2. **إنشاء Configuration Tables:**
   - [ ] Account Mapping (في appsettings.json أو database)
   - [ ] Cost Center Mapping (في appsettings.json أو database)

3. **كتابة Code:**
   - [ ] VoM Journal Entry DTOs
   - [ ] VoM Journal Entry Service
   - [ ] Account Mapping Service
   - [ ] Cost Center Mapping Service
   - [ ] Integration في PaymentReceiptService

4. **Testing:**
   - [ ] Test Create Journal Entry
   - [ ] Test Update Journal Entry
   - [ ] Test Error Handling
   - [ ] Test Idempotency

---

## ❓ **أسئلة تحتاج إجابة:**

1. **من VoM API Documentation:**
   - ما هي بنية Request Body لـ Create Journal Entry؟
   - ما هي الحقول المطلوبة vs الاختيارية؟
   - هل يدعم Update Journal Entry؟
   - كيف نتعامل مع Errors؟

2. **من Business Logic:**
   - كيف نحدد Account Code من PaymentMethod/BankId؟
   - كيف نحدد Cost Center من HotelId؟
   - ما هي القيم الافتراضية (Tax Status, Beneficiary, etc.)؟

3. **من Integration:**
   - هل نرجع خطأ إذا فشل VoM API؟
   - هل نحتاج Retry Logic؟
   - كيف نتأكد من Idempotency؟

---

**Last Updated:** 2025-01-11  
**Status:** ⚠️ **يحتاج قراءة VoM API Documentation أولاً**
