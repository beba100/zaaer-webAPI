# 🔧 Fix: VoM Auto Send Job - Finding Unsent Records

## 📋 **Problem Identified**

The tracking tables (`invoice_journal_entries`, `credit_note_journal_entries`, `payment_receipt_journal_entries`) **only create records AFTER attempting to send to VoM**.

This means:
- ✅ 12 invoices exist in `invoices` table
- ❌ Only 4 records in `invoice_journal_entries` (meaning 8 invoices were never sent)
- ✅ 3 credit notes exist in `credit_notes` table
- ❌ 0 records in `credit_note_journal_entries` (all 3 credit notes were never sent)
- ✅ 13 payment receipts exist in `payment_receipts` table
- ❌ 0 records in `payment_receipt_journal_entries` (all 13 receipts were never sent)

---

## ✅ **Solution Implemented**

### **Modified Query Logic:**

The job now uses **LEFT JOIN** to find records that:
1. **Have NO tracking entry** (never attempted) - `j.id IS NULL`
2. **OR have Pending status** - `j.status = 'Pending'`
3. **OR have Failed status with retry count < maxRetries** - `j.status = 'Failed' AND j.retry_count < maxRetries`

### **Code Changes:**

#### **1. `ProcessTenantAsync` Method:**
- Now creates a **single `IServiceScope`** per tenant
- Resolves `ApplicationDbContext` and all journal entry services from the same scope
- Passes the scoped services to each processing method
- Ensures all services share the same database context

#### **2. `ProcessInvoicesAsync` Method:**
```csharp
var invoicesToSend = await db.Invoices
    .Where(i => !db.Set<InvoiceJournalEntry>().Any(j => j.InvoiceZaaerId == i.ZaaerId && j.Status == "Sent")
        || db.Set<InvoiceJournalEntry>().Any(j => 
            j.InvoiceZaaerId == i.ZaaerId && 
            (j.Status == "Pending" || (j.Status == "Failed" && j.RetryCount < maxRetries))))
    .Take(batchSize)
    .ToListAsync();
```

**Translation to SQL (for clarity):**
```sql
SELECT TOP (@batchSize) *
FROM invoices i
LEFT JOIN invoice_journal_entries j ON j.invoice_zaaer_id = i.zaaer_id
WHERE 
    -- Never sent (no tracking record)
    j.id IS NULL 
    -- OR Pending
    OR j.status = 'Pending'
    -- OR Failed with retries remaining
    OR (j.status = 'Failed' AND j.retry_count < @maxRetries)
```

#### **3. `ProcessCreditNotesAsync` Method:**
Same logic applied to `credit_notes` table with `credit_note_journal_entries`.

#### **4. `ProcessPaymentReceiptsAsync` Method:**
Same logic applied to `payment_receipts` table with `payment_receipt_journal_entries`.

---

## 🧪 **How to Test**

### **Step 1: Run Test Query**

1. Open SQL Server Management Studio (SSMS)
2. Open `TEST_UNSENT_QUERY.sql` from the project root
3. Change the database name on line 8: `USE [YOUR_HOTEL_DATABASE_NAME_HERE];`
4. Run the script
5. **Expected Results:**
   - Shows all invoices/credit notes/receipts that need to be sent
   - Shows summary count of unsent records
   - Verifies that `zaaer_id` fields are populated (critical for matching)

### **Step 2: Test the Job API (Postman)**

#### **URL:**
```
http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f&batchSize=1&maxRetries=3
```

**Note:** Using `batchSize=1` to send only 1 record per tenant for quick testing.

#### **Expected Response:**
```json
{
    "success": true,
    "message": "VoM Auto Send Job completed successfully",
    "duration": "00:00:25",
    "tenants": {
        "processed": 48,
        "errors": 0
    },
    "invoices": {
        "total": 8,        // <-- Should now show unsent invoices!
        "sent": 8,
        "failed": 0
    },
    "creditNotes": {
        "total": 3,        // <-- Should now show unsent credit notes!
        "sent": 3,
        "failed": 0
    },
    "paymentReceipts": {
        "total": 13,       // <-- Should now show unsent receipts!
        "sent": 13,
        "failed": 0
    }
}
```

### **Step 3: Verify in Database**

After running the job, check the tracking tables again:

```sql
USE [YOUR_HOTEL_DATABASE_NAME];

-- Check invoice sends
SELECT COUNT(*) AS TotalSent 
FROM invoice_journal_entries 
WHERE status = 'Sent';

-- Check credit note sends
SELECT COUNT(*) AS TotalSent 
FROM credit_note_journal_entries 
WHERE status = 'Sent';

-- Check payment receipt sends
SELECT COUNT(*) AS TotalSent 
FROM payment_receipt_journal_entries 
WHERE status = 'Sent';
```

**Expected:** All counts should increase after the job runs!

### **Step 4: Check Application Logs**

```
C:\zaaerIntegration\logs\log-[TODAY].txt
```

Look for:
```
[VoM Auto Send Job] 🚀 Job Started
[VoM Auto Send Job] ➡️ Processing Tenant: DAMMAM1 - فندق الدمام
[VoM Auto Send Job] 📄 Processing Invoices for DAMMAM1...
[VoM Auto Send Job] Found 8 invoices to send for DAMMAM1
[VoM Auto Send Job] ✅ Invoice INV00001 sent successfully
[VoM Auto Send Job] ✅ Invoice INV00002 sent successfully
...
[VoM Auto Send Job] ✅ Job Completed Successfully
```

---

## ⚠️ **Important: Verify `zaaer_id` is Populated**

The job relies on matching records using `zaaer_id` (or `ZaaerId` in Entity Framework).

**Critical Check:**
```sql
-- Check if zaaer_id exists and is populated
SELECT 
    'Invoices' AS TableName,
    COUNT(*) AS Total,
    COUNT(zaaer_id) AS WithZaaerId,
    COUNT(*) - COUNT(zaaer_id) AS MissingZaaerId
FROM invoices

UNION ALL

SELECT 
    'Credit Notes',
    COUNT(*),
    COUNT(zaaer_id),
    COUNT(*) - COUNT(zaaer_id)
FROM credit_notes

UNION ALL

SELECT 
    'Payment Receipts',
    COUNT(*),
    COUNT(zaaer_id),
    COUNT(*) - COUNT(zaaer_id)
FROM payment_receipts;
```

**If `MissingZaaerId` > 0**, those records **will NOT be sent** because the job cannot match them to tracking records!

### **Fix for Missing `zaaer_id`:**
If records are missing `zaaer_id`, you need to update them:

```sql
-- Example: Update invoices without zaaer_id
UPDATE invoices
SET zaaer_id = invoice_id  -- Or use another unique identifier
WHERE zaaer_id IS NULL;

-- Example: Update credit notes without zaaer_id
UPDATE credit_notes
SET zaaer_id = credit_note_id
WHERE zaaer_id IS NULL;

-- Example: Update payment receipts without zaaer_id
UPDATE payment_receipts
SET zaaer_id = receipt_id
WHERE zaaer_id IS NULL;
```

---

## 🎯 **MonsterASP Configuration**

Once testing is complete, configure MonsterASP with the production settings:

**URL:**
```
http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f&batchSize=50&maxRetries=3
```

**Schedule:** Daily at 00:00 (Midnight KSA)

**Method:** POST

---

## 📊 **Expected Behavior After Fix**

### **First Run (After Fix):**
- Job will find and send **ALL unsent records** across all 48 hotels
- Tracking tables will be populated with new records
- Logs will show successful sends

### **Subsequent Runs:**
- Job will only send new records created since last run
- Job will retry any failed records (if retry_count < maxRetries)
- If all records are sent, response will show 0 (which is expected and correct)

---

## ✅ **Success Criteria**

1. ✅ Test query (`TEST_UNSENT_QUERY.sql`) shows unsent records
2. ✅ Job API returns `total > 0` for invoices/credit notes/receipts on first run
3. ✅ Tracking tables show new records after job completes
4. ✅ Application logs confirm successful sends
5. ✅ `zaaer_id` fields are populated in all source tables
6. ✅ MonsterASP scheduled task is configured and running

---

## 🐛 **Troubleshooting**

### **Problem: Job still returns 0 records**

**Possible Causes:**

1. **Missing `zaaer_id`:**
   - Run verification query (Step 4 in "Verify `zaaer_id`" section above)
   - Update records if needed

2. **All records already sent:**
   - Check tracking tables: `SELECT * FROM invoice_journal_entries WHERE status = 'Sent'`
   - If you want to re-send for testing, delete tracking records

3. **Services not resolving correctly:**
   - Check application logs for errors
   - Verify `Program.cs` has correct service registrations

4. **Database connection issue:**
   - Check connection strings in `appsettings.json`
   - Verify tenant database names match master database

### **Problem: Job returns 500 error**

**Check:**
1. Application logs for detailed error message
2. Verify all services are registered in `Program.cs`
3. Ensure `ApplicationDbContext` is registered as scoped
4. Check database connection strings

### **Problem: Records show as "Failed"**

**Check:**
1. VoM API is accessible
2. VoM API credentials are correct
3. VoM API response for specific error message (stored in `error_message` column)
4. Network connectivity to VoM API

---

## 📝 **Files Modified**

1. ✅ `zaaerIntegration/Controllers/Jobs/VoMAutoSendJobController.cs`
   - Fixed service scope resolution
   - Modified `ProcessTenantAsync` to create single scope per tenant
   - Updated `ProcessInvoicesAsync`, `ProcessCreditNotesAsync`, `ProcessPaymentReceiptsAsync` to accept scoped services

2. ✅ `TEST_UNSENT_QUERY.sql` (New file)
   - SQL test script to verify unsent records logic

3. ✅ `FIX_UNSENT_RECORDS.md` (This file)
   - Documentation of the fix and testing procedures

---

## 🎉 **Ready to Test!**

Run the job with `batchSize=1` first to test with minimal impact, then scale up to `batchSize=50` for production! 🚀

