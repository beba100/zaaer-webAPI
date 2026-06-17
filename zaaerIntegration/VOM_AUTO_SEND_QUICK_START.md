# ✅ VoM Auto Send Job - Quick Start Checklist

## 🎯 Follow these steps to activate automated VoM sending

---

## ☑️ Step 1: Database Migration (5 minutes)

**Run SQL migration on EACH tenant database:**

```sql
-- 1. Connect to first tenant database
USE [Dammam1DB];
GO

-- 2. Execute the migration script
-- File: zaaerIntegration/Database/CreateCreditNoteJournalEntriesTable.sql

-- 3. Verify table created
SELECT * FROM credit_note_journal_entries;
-- Should return empty result (0 rows)

-- 4. Repeat for all other tenant databases
-- USE [Makkah1DB]; GO
-- USE [Riyadh1DB]; GO
-- etc.
```

**✅ Check:** Table `credit_note_journal_entries` exists in all tenant databases

---

## ☑️ Step 2: Test Endpoint Locally (10 minutes)

**Open PowerShell and run:**

```powershell
# Set variables
$apiKey = "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f"
$url = "https://localhost:7095/api/jobs/VoMAutoSendJob/vom-auto-send"

# Make sure your app is running!
# Then test:
$headers = @{ "X-API-Key" = $apiKey }
Invoke-RestMethod -Uri $url -Method POST -Headers $headers
```

**✅ Check:** Response shows `"success": true` and statistics

**Expected Output:**
```json
{
  "success": true,
  "duration": "00:05:23",
  "invoices": { "sent": 42, "total": 45 },
  "creditNotes": { "sent": 12, "total": 12 },
  "paymentReceipts": { "sent": 75, "total": 78 }
}
```

---

## ☑️ Step 3: Verify Database Updates (2 minutes)

**Check records were marked as "Sent":**

```sql
-- Connect to a tenant database
USE [Dammam1DB];
GO

-- Check invoices sent
SELECT COUNT(*) as InvoicesSent
FROM invoice_journal_entries
WHERE status = 'Sent';

-- Check payment receipts sent
SELECT COUNT(*) as ReceiptsSent
FROM payment_receipt_journal_entries
WHERE status = 'Sent';

-- Check credit notes sent
SELECT COUNT(*) as CreditNotesSent
FROM credit_note_journal_entries
WHERE status = 'Sent';
```

**✅ Check:** All counts should match the API response

---

## ☑️ Step 4: Deploy to Production (5 minutes)

### A. Publish Application

1. Build in Release mode
2. Publish to `aleairy.tryasp.net`
3. Update `appsettings.json` on server (if needed)

### B. Verify Production Endpoint

```powershell
$apiKey = "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f"
$url = "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send"

$headers = @{ "X-API-Key" = $apiKey }
Invoke-RestMethod -Uri $url -Method POST -Headers $headers
```

**✅ Check:** Production endpoint returns success

---

## ☑️ Step 5: Configure MonsterASP Task (5 minutes)

### Login to MonsterASP
1. Go to: https://admin.monsterasp.net/
2. Navigate to: **Website** → **Scheduled Tasks**
3. Click: **"Add New Task"**

### Task Configuration:

| Field | Value |
|-------|-------|
| **Task Name** | VoM Auto Send Job - Daily |
| **Description** | Automatically sends pending journal entries to VoM |
| **Plan (Scheduler)** | Daily at midnight |
| **Domain** | `http://aleairy.tryasp.net` |
| **URL Address** | `/api/jobs/VoMAutoSendJob/vom-auto-send` |

### Add Custom Header:
- **Header Name:** `X-API-Key`
- **Header Value:** `VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f`

### Click: **"Save"** or **"Add"**

**✅ Check:** Task appears in MonsterASP scheduled tasks list

---

## ☑️ Step 6: Test MonsterASP Task (Optional - 2 minutes)

**Most hosting platforms have a "Run Now" button:**

1. In MonsterASP task list, find your task
2. Click **"Run Now"** or **"Test"** button
3. Wait for execution (may take 5-10 minutes)
4. Check logs

**✅ Check:** Task executes successfully

---

## ☑️ Step 7: Monitor First Automatic Run (Next Day)

**The next morning (after midnight), check:**

### A. Check Application Logs:
```powershell
# On server, view logs:
Get-Content "C:\zaaerIntegration\logs\log-*.txt" -Tail 100 | Select-String "VoM Auto Send"
```

**Look for:**
- `[VoM Auto Send Job] 🚀 Job Started`
- `[VoM Auto Send Job] ✅ Job Completed Successfully`

### B. Check Database:
```sql
-- Records sent in last 24 hours
SELECT COUNT(*) as TodaysSent
FROM invoice_journal_entries
WHERE status = 'Sent' AND created_at >= DATEADD(day, -1, GETDATE());
```

**✅ Check:** New records have been sent automatically

---

## 📊 Quick Health Check

**Run this daily/weekly to verify everything is working:**

```sql
-- Summary of all journal entry statuses
SELECT 
    'Invoices' as Type,
    status,
    COUNT(*) as Count
FROM invoice_journal_entries
GROUP BY status

UNION ALL

SELECT 
    'Credit Notes' as Type,
    status,
    COUNT(*) as Count
FROM credit_note_journal_entries
GROUP BY status

UNION ALL

SELECT 
    'Payment Receipts' as Type,
    status,
    COUNT(*) as Count
FROM payment_receipt_journal_entries
GROUP BY status
ORDER BY Type, status;
```

**Expected:**
- Most records should be "Sent"
- Some "Pending" is normal (new records)
- Few "Failed" is okay (will retry)
- Many "Failed" needs investigation

---

## 🚨 Troubleshooting

### Problem: Task doesn't run
**Solution:**
1. Check MonsterASP task is **Enabled**
2. Verify schedule is set correctly
3. Check task execution history in MonsterASP

### Problem: Returns "Invalid API Key"
**Solution:**
1. Compare API key in `appsettings.json`
2. Compare API key in MonsterASP task header
3. Ensure no extra spaces in key

### Problem: Some records fail
**Solution:**
1. Check logs for error details
2. Verify VoM API credentials
3. Check `chart_of_accounts` table has all required accounts
4. Review `error_message` field in tracking tables

---

## 📝 Configuration File Reference

### appsettings.json
```json
{
  "Jobs": {
    "VoMAutoSend": {
      "ApiKey": "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f",
      "Enabled": true,
      "MaxRetries": 3,
      "BatchSize": 100
    }
  }
}
```

### MonsterASP Task Settings:
- **URL**: `/api/jobs/VoMAutoSendJob/vom-auto-send`
- **Header**: `X-API-Key: VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f`
- **Schedule**: Daily at midnight

---

## ✅ Final Checklist

- [ ] SQL migration run on all tenant databases
- [ ] Endpoint tested locally (returns success)
- [ ] Database records marked as "Sent"
- [ ] Application published to production
- [ ] Production endpoint tested (returns success)
- [ ] MonsterASP task configured
- [ ] MonsterASP task tested (optional)
- [ ] Monitoring set up for automatic runs
- [ ] Health check query saved

---

## 🎉 You're Done!

Your VoM Auto Send Job is now:
- ✅ Running automatically every day at midnight
- ✅ Processing all tenants
- ✅ Sending Invoices, Credit Notes, and Payment Receipts
- ✅ Retrying failed records
- ✅ Logging everything for monitoring
- ✅ Secured with API key

**Total Setup Time:** ~30 minutes

**Estimated Time Saved:** ~2 hours per day (manual sending)

---

**Need Help?**  
See full documentation: `VOM_AUTO_SEND_IMPLEMENTATION_GUIDE.md`

**Test Scripts:**  
See test scripts: `VOM_AUTO_SEND_TEST_SCRIPT.md`

**MonsterASP Setup:**  
See detailed setup: `MONSTERASP_SCHEDULED_TASK_SETUP.md`

