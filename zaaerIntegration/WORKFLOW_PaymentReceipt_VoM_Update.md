# Payment Receipt VoM Update Workflow - تفصيل كامل للعملية

## 📋 Overview - نظرة عامة

هذا المستند يشرح بالتفصيل workflow تحديث Payment Receipt وإرساله إلى VoM، مع التركيز على:
1. **DELETE then CREATE** pattern (حسب توصية VoM)
2. **Error Handling** عند فشل DELETE
3. **Fallback Logic** لاستخراج `vom_journal_entry_id` من response

---

## 🔄 Complete Workflow - الفلو الكامل

### **Step 1: Entry Point - نقطة الدخول**

```
POST /api/PaymentReceiptJournalEntry/SendToVoMByNumber/{receiptNo}
PUT  /api/zaaer/PaymentReceipt/{zaaerId}
```

### **Step 2: Load Receipt - تحميل السند**

```csharp
var receipt = await _context.PaymentReceipts
    .FirstOrDefaultAsync(r => r.ReceiptNo == receiptNo);
```

**Fields Checked:**
- `receipt.StatusVoM` (pending, sent, failed, skipped)
- `receipt.VomJournalEntryId` (ID of existing VoM entry)
- `receipt.ZaaerId` (Zaaer system ID)

---

### **Step 3: Check for Existing VoM Entry - التحقق من وجود قيد قديم**

#### **3.1: If `status_vom = "sent"` AND `vom_journal_entry_id IS NOT NULL`**

```csharp
if (receipt.StatusVoM == "sent" && receipt.VomJournalEntryId.HasValue)
{
    // ✅ Found existing entry - must DELETE before CREATE
}
```

**What happens:**
1. **Call DELETE API:**
   ```
   DELETE /api/accounting/journal-entries/{vom_journal_entry_id}
   ```

2. **Two Scenarios:**

   **✅ Scenario A: DELETE Success**
   ```csharp
   if (deleteSuccess)
   {
       // Clear old ID
       receipt.VomJournalEntryId = null;
       receipt.StatusVoM = "pending";
       receipt.VomError = null;
       receipt.VomSentAt = null;
       // Proceed to CREATE new entry
   }
   ```

   **⚠️ Scenario B: DELETE Failed**
   ```csharp
   else
   {
       // ⚠️ IMPORTANT: Don't clear vom_journal_entry_id
       // Keep it for reference and manual cleanup
       receipt.StatusVoM = "pending";
       receipt.VomError = "Failed to delete old VoM entry (ID: {id}) before update. New entry will be created.";
       receipt.VomSentAt = null;
       // ⚠️ Proceed to CREATE new entry anyway
       // Old entry will remain in VoM (can be manually cleaned up)
   }
   ```

**Why keep `vom_journal_entry_id` if DELETE failed?**
- Allows manual cleanup later
- Provides audit trail
- Prevents data loss

#### **3.2: If `status_vom = "sent"` BUT `vom_journal_entry_id IS NULL`**

```csharp
else if (receipt.StatusVoM == "sent")
{
    // Already sent, no update needed
    return true; // Prevent duplicate
}
```

---

### **Step 4: Validation & Preparation - التحقق والإعداد**

#### **4.1: Skip if `voucher_code = "expense"`**

```csharp
if (voucherCode == "expense")
{
    receipt.StatusVoM = "skipped";
    receipt.VomError = "Voucher code is 'expense' - not sent to VoM";
    return false;
}
```

#### **4.2: Validate Receipt Data**

```csharp
if (!ValidateReceiptForJournalEntry(receipt))
{
    return false;
}
```

#### **4.3: Get Cost Center & Payment Account**

```csharp
var costCenter = await GetCostCenterAsync(receipt.HotelId);
var paymentAccountId = await GetPaymentAccountAsync(receipt);
```

---

### **Step 5: Build Journal Entry Request - بناء طلب القيد**

```csharp
var journalEntryRequest = BuildJournalEntryRequest(receipt, costCenter, paymentAccountId);
```

**Request Structure:**
```json
{
  "journal_date": "31-12-2025",
  "code": "REC0134",
  "memo": "قيد سند قبض رقم REC0134 - الدمام 9",
  "accounts": [
    {
      "id": 51,
      "debit": 0,
      "credit": 111.00,
      "cost_center_id": 7,
      "tax_status": 2,
      "description": "سند قبض"
    },
    {
      "id": 179,
      "debit": 111.00,
      "credit": 0,
      "tax_status": 2,
      "description": "صندوق"
    }
  ]
}
```

---

### **Step 6: Send to VoM API - الإرسال إلى VoM**

```csharp
var response = await _voMJournalEntryService.CreateJournalEntryAsync(
    journalEntryRequest, 
    "ar");
```

**API Call:**
```
POST https://kimoo.getvom.com/api/accounting/journal-entries
```

---

### **Step 7: Extract VoM Journal Entry ID - استخراج ID القيد**

#### **7.1: Primary Method - Helper Property**

```csharp
var vomJournalEntryId = response.Data?.Id; 
// Uses: JournalEntry?.Id
```

#### **7.2: Fallback 1 - Direct Access**

```csharp
if (!vomJournalEntryId.HasValue && response.Data?.JournalEntry != null)
{
    vomJournalEntryId = response.Data.JournalEntry.Id;
}
```

#### **7.3: Fallback 2 - Extract from Raw JSON**

```csharp
if (!vomJournalEntryId.HasValue && !string.IsNullOrWhiteSpace(response.RawResponse))
{
    using (JsonDocument doc = JsonDocument.Parse(response.RawResponse))
    {
        var root = doc.RootElement;
        if (root.TryGetProperty("data", out var dataProp))
        {
            if (dataProp.TryGetProperty("journalEntry", out var journalEntryProp))
            {
                if (journalEntryProp.TryGetProperty("id", out var idProp))
                {
                    vomJournalEntryId = idProp.GetInt32();
                }
            }
        }
    }
}
```

**Response Structure:**
```json
{
  "status": 200,
  "data": {
    "status": true,
    "journalEntry": {
      "id": 2716,  // ← This is what we need
      "code": 2701,
      "memo": "...",
      ...
    }
  },
  "success": true
}
```

---

### **Step 8: Save to Database - الحفظ في قاعدة البيانات**

#### **8.1: Update `payment_receipts` Table**

```csharp
receipt.StatusVoM = "sent";
receipt.VomJournalEntryId = vomJournalEntryId; // ✅ Save ID here
receipt.VomPayload = JsonSerializer.Serialize(request);
receipt.VomSentAt = KsaTime.Now;
receipt.VomError = null;

_context.PaymentReceipts.Update(receipt);
await _context.SaveChangesAsync();
```

#### **8.2: Save to `payment_receipt_journal_entries` (Audit Trail)**

```csharp
var entry = new PaymentReceiptJournalEntry
{
    ReceiptId = receipt.ReceiptId,
    ReceiptZaaerId = receipt.ZaaerId,
    VomJournalEntryId = vomJournalEntryId, // For audit only
    JournalEntryCode = request.Code,
    Status = "Sent",
    VomResponse = response.RawResponse, // Full JSON response
    ...
};

_context.Set<PaymentReceiptJournalEntry>().Add(entry);
await _context.SaveChangesAsync();
```

---

## ⚠️ Error Handling Scenarios - سيناريوهات معالجة الأخطاء

### **Scenario 1: DELETE Failed (Network Error)**

```
DELETE /api/accounting/journal-entries/2713
→ Network timeout / Connection error
```

**Handling:**
- ✅ Log warning
- ✅ Keep `vom_journal_entry_id` (don't clear it)
- ✅ Set `vom_error` with message
- ✅ Proceed to CREATE new entry
- ⚠️ Old entry remains in VoM (manual cleanup needed)

**Result:**
- New entry created in VoM
- Old entry still exists in VoM
- `vom_journal_entry_id` points to old entry (for reference)

---

### **Scenario 2: DELETE Failed (Entry Already Deleted)**

```
DELETE /api/accounting/journal-entries/2713
→ 404 Not Found (entry already deleted)
```

**Handling:**
- ✅ Log warning
- ✅ Clear `vom_journal_entry_id` (entry doesn't exist)
- ✅ Proceed to CREATE new entry

**Result:**
- New entry created in VoM
- No duplicate (old entry was already gone)

---

### **Scenario 3: CREATE Failed After DELETE**

```
DELETE /api/accounting/journal-entries/2713 → ✅ Success
CREATE /api/accounting/journal-entries → ❌ Failed
```

**Handling:**
- ✅ Old entry deleted from VoM
- ❌ New entry not created
- ✅ `status_vom = "failed"`
- ✅ `vom_error` contains error message
- ✅ `vom_journal_entry_id = null` (no entry exists)

**Result:**
- No entry in VoM (old deleted, new failed)
- Receipt can be retried later

---

### **Scenario 4: ID Extraction Failed**

```
Response received but id is null
```

**Handling:**
- ✅ Try helper property: `response.Data?.Id`
- ✅ Try direct access: `response.Data.JournalEntry.Id`
- ✅ Try raw JSON parsing
- ⚠️ If all fail, log warning but proceed

**Result:**
- Entry created in VoM
- `vom_journal_entry_id = null` (ID not extracted)
- Can be manually updated later

---

## 📊 Database Schema - هيكل قاعدة البيانات

### **`payment_receipts` Table**

| Column | Type | Description |
|--------|------|-------------|
| `receipt_id` | INT | Primary Key |
| `receipt_no` | NVARCHAR(50) | Receipt Number |
| `zaaer_id` | INT | Zaaer System ID |
| `status_vom` | NVARCHAR(20) | pending, sent, failed, skipped |
| `vom_journal_entry_id` | INT | **VoM Journal Entry ID (PRIMARY STORAGE)** |
| `vom_payload` | NVARCHAR(MAX) | Request JSON |
| `vom_sent_at` | DATETIME | Send timestamp |
| `vom_error` | NVARCHAR(MAX) | Error message |

### **`payment_receipt_journal_entries` Table (Audit Trail)**

| Column | Type | Description |
|--------|------|-------------|
| `id` | INT | Primary Key |
| `receipt_id` | INT | FK to payment_receipts |
| `receipt_zaaer_id` | INT | Zaaer ID (for lookup) |
| `vom_journal_entry_id` | INT | VoM ID (for audit only) |
| `status` | NVARCHAR(20) | Sent, Failed, Deleted |
| `vom_response` | NVARCHAR(MAX) | Full JSON response |
| `error_message` | NVARCHAR(1000) | Error details |

---

## 🔍 Key Points - نقاط مهمة

### **1. Primary Storage Location**
- ✅ `vom_journal_entry_id` is stored in `payment_receipts` table
- ✅ This is the **primary source** for lookups
- ✅ `payment_receipt_journal_entries` is for **audit trail only**

### **2. DELETE Failure Handling**
- ⚠️ If DELETE fails, **don't clear** `vom_journal_entry_id`
- ⚠️ Keep it for reference and manual cleanup
- ⚠️ Proceed with CREATE anyway (old entry may remain in VoM)

### **3. ID Extraction**
- ✅ Multiple fallback methods ensure ID is extracted
- ✅ Raw JSON parsing as last resort
- ⚠️ If all fail, entry is still created (ID can be updated manually)

### **4. Duplicate Prevention**
- ✅ Check `status_vom = "sent"` before sending
- ✅ DELETE old entry before CREATE new one
- ⚠️ If DELETE fails, duplicate may occur (manual cleanup needed)

---

## 🎯 Best Practices - أفضل الممارسات

1. **Always check `vom_journal_entry_id` before DELETE**
2. **Log all DELETE attempts (success and failure)**
3. **Keep `vom_journal_entry_id` if DELETE fails (for audit)**
4. **Use multiple fallback methods for ID extraction**
5. **Store full response in `vom_response` for debugging**
6. **Monitor for duplicates in VoM (manual cleanup if needed)**

---

## 📝 Notes - ملاحظات

- VoM API doesn't support UPDATE - only DELETE and CREATE
- Duplicate entries in VoM can be manually cleaned up
- `vom_journal_entry_id` in `payment_receipts` is the **single source of truth**
- `payment_receipt_journal_entries` provides detailed audit trail

---

## 🔗 Related Files

- `PaymentReceiptJournalEntryService.cs` - Main service
- `VoMJournalEntryService.cs` - VoM API client
- `VoMJournalEntryDto.cs` - DTOs with IntOrStringConverter
- `PaymentReceipt.cs` - Model with `VomJournalEntryId` property

