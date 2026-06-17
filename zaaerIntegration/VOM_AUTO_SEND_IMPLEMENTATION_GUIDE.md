# 🚀 VoM Auto Send Job - Complete Implementation Guide

## 📋 Overview
Automated job to send **Invoices**, **Credit Notes**, and **Payment Receipts** to VoM system daily without manual intervention.

---

## ✅ Files Created

### 1️⃣ **Backend Controller**
- **File**: `zaaerIntegration/Controllers/Jobs/VoMAutoSendJobController.cs`
- **Purpose**: HTTP endpoint for MonsterASP scheduled task
- **Endpoint**: `POST /api/jobs/VoMAutoSendJob/vom-auto-send`

### 2️⃣ **Configuration**
- **File**: `zaaerIntegration/appsettings.json`
- **Section**: `Jobs:VoMAutoSend`
- **API Key**: `VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f`

### 3️⃣ **Database Model**
- **File**: `zaaerIntegration/Models/CreditNoteJournalEntry.cs`
- **Purpose**: Track credit note journal entries (was missing!)

### 4️⃣ **SQL Migration**
- **File**: `zaaerIntegration/Database/CreateCreditNoteJournalEntriesTable.sql`
- **Purpose**: Create `credit_note_journal_entries` table

### 5️⃣ **Documentation**
- `MONSTERASP_SCHEDULED_TASK_SETUP.md` - MonsterASP configuration guide
- `VOM_AUTO_SEND_TEST_SCRIPT.md` - Testing scripts

---

## 🎯 How It Works

### Architecture Flow:

```
┌─────────────────────────────────────────────────────────────┐
│   MonsterASP Scheduled Task (Daily at Midnight)            │
│   Calls: POST /api/jobs/VoMAutoSendJob/vom-auto-send       │
│   Header: X-API-Key: YOUR-SECURE-API-KEY                    │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              VoMAutoSendJobController                        │
│  1. Validates API Key                                        │
│  2. Gets all tenants from master DB                          │
│  3. For each tenant:                                         │
│     - Connects to tenant database                            │
│     - Finds unsent records (Pending/Failed)                  │
│     - Processes Invoices, Credit Notes, Payment Receipts     │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              Journal Entry Services                          │
│  - InvoiceJournalEntryService                               │
│  - CreditNoteJournalEntryService                            │
│  - PaymentReceiptJournalEntryService                        │
│                                                              │
│  Each service:                                               │
│  1. Checks if already sent (idempotency)                    │
│  2. Builds VoM journal entry payload                        │
│  3. Calls VoMJournalEntryService.CreateJournalEntryAsync()  │
│  4. Updates status in tracking table (Sent/Failed)          │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              VoMJournalEntryService                          │
│  1. Validates journal entry (balanced, accounts exist)       │
│  2. Authenticates with VoM API                               │
│  3. Sends POST to /api/accounting/journal-entries            │
│  4. Returns response                                         │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                VoM External API                              │
│  - Creates journal entry in VoM system                       │
│  - Returns VoM journal entry ID                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 📊 Database Tracking

### Tables:
1. **`invoice_journal_entries`** - Tracks invoice journal entries
2. **`payment_receipt_journal_entries`** - Tracks payment receipt journal entries
3. **`credit_note_journal_entries`** - Tracks credit note journal entries ✨ (NEW!)

### Status Values:
- **`Pending`** - Created but not sent yet
- **`Sent`** - Successfully sent to VoM
- **`Failed`** - Failed to send (will retry up to maxRetries)
- **`Cancelled`** - Manually cancelled

### Key Fields:
- `credit_note_zaaer_id` / `invoice_zaaer_id` / `receipt_zaaer_id` - Unique identifier from Zaaer
- `vom_journal_entry_id` - ID returned by VoM API
- `status` - Current status (Pending/Sent/Failed)
- `retry_count` - Number of retry attempts
- `vom_response` - Full VoM API response
- `error_message` - Error details if failed

---

## 🔧 Setup Instructions

### Step 1: Run SQL Migration
```sql
-- Connect to EACH tenant database and run:
USE [Dammam1DB];
GO
-- Execute: CreateCreditNoteJournalEntriesTable.sql
```

### Step 2: Verify API Key in appsettings.json
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

### Step 3: Configure MonsterASP Scheduled Task

**Task Details:**
- **Name**: VoM Auto Send Job - Daily
- **Schedule**: Daily at midnight
- **Domain**: `http://aleairy.tryasp.net`
- **URL**: `/api/jobs/VoMAutoSendJob/vom-auto-send`
- **Method**: POST
- **Custom Header**: 
  - Name: `X-API-Key`
  - Value: `VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f`

### Step 4: Test Manually
```powershell
$headers = @{
    "X-API-Key" = "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f"
}

Invoke-RestMethod -Uri "https://localhost:7095/api/jobs/VoMAutoSendJob/vom-auto-send" `
    -Method POST `
    -Headers $headers
```

---

## 📈 Monitoring & Logs

### Check Application Logs:
```powershell
Get-Content "C:\zaaerIntegration\zaaerIntegration\logs\log-*.txt" -Tail 200 | Select-String "VoM Auto Send"
```

### Expected Log Output:
```
[VoM Auto Send Job] 🚀 Job Started at 2025-12-20 00:00:00
[VoM Auto Send Job] ✅ API Key validated successfully
[VoM Auto Send Job] Found 5 active tenants
[VoM Auto Send Job] ➡️ Processing Tenant: Dammam1 - الدمام 1
[VoM Auto Send Job] 📄 Processing Invoices for Dammam1...
[VoM Auto Send Job] Found 45 invoices to send for Dammam1
[VoM Auto Send Job] ✅ Invoice INV00046 sent successfully
[VoM Auto Send Job] 🔄 Processing Credit Notes for Dammam1...
[VoM Auto Send Job] Found 12 credit notes to send for Dammam1
[VoM Auto Send Job] ✅ Credit Note CN00001 sent successfully
[VoM Auto Send Job] 💰 Processing Payment Receipts for Dammam1...
[VoM Auto Send Job] Found 78 payment receipts to send for Dammam1
[VoM Auto Send Job] ✅ Payment Receipt PR00234 sent successfully
[VoM Auto Send Job] ✅ Job Completed Successfully
[VoM Auto Send Job] Duration: 00:05:23
[VoM Auto Send Job] Total Invoices: 42/45 sent
[VoM Auto Send Job] Total Credit Notes: 12/12 sent
[VoM Auto Send Job] Total Payment Receipts: 75/78 sent
```

---

## 🎯 Features

✅ **Automatic Sending**: Runs daily without manual intervention  
✅ **Multi-Tenant Support**: Processes all tenants automatically  
✅ **All Document Types**: Invoices, Credit Notes, Payment Receipts  
✅ **Smart Retry Logic**: Only retries failed records (configurable max)  
✅ **Idempotency**: Won't send the same record twice  
✅ **Secure**: API Key authentication  
✅ **Performance**: Batch processing with configurable batch size  
✅ **Detailed Logging**: Complete audit trail  
✅ **Summary Report**: JSON response with statistics  
✅ **Error Handling**: Graceful failure handling per tenant  

---

## ⚙️ Configuration Options

### Query Parameters:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `maxRetries` | 3 | Maximum retry attempts for failed records |
| `batchSize` | 100 | Number of records to process per tenant |

### Example:
```
POST /api/jobs/VoMAutoSendJob/vom-auto-send?maxRetries=5&batchSize=50
```

---

## 🔍 Troubleshooting

### Issue: "Invalid API Key"
**Solution**: Verify API key in:
1. `appsettings.json` → `Jobs:VoMAutoSend:ApiKey`
2. MonsterASP task header → `X-API-Key`

### Issue: Some Records Always Fail
**Solution**: Check logs for specific error:
- VoM API authentication issues
- Missing accounts in `chart_of_accounts`
- Invalid data in source records

### Issue: Job Times Out
**Solution**: Reduce batch size:
```
?batchSize=50
```

### Issue: Duplicate Journal Entries
**Solution**: Check unique constraint in database:
```sql
SELECT * FROM invoice_journal_entries 
WHERE invoice_zaaer_id = 12345 AND status = 'Sent';
```

---

## 📊 Success Metrics

**HTTP 200 OK Response:**
```json
{
  "success": true,
  "duration": "00:05:23",
  "summary": {
    "totalRecords": 135,
    "totalSent": 129,
    "totalFailed": 6,
    "successRate": 95.56
  }
}
```

---

## 🛡️ Security

1. ✅ API Key authentication prevents unauthorized access
2. ✅ HTTPS recommended for production
3. ✅ API key stored securely in appsettings.json
4. ✅ Rate limiting via batch size
5. ✅ Audit trail in logs and database

---

## 📞 Support

For issues or questions:
1. Check application logs: `/logs/log-*.txt`
2. Check VoM API logs: `GET /api/vom/VoMLogs/recent`
3. Review database tracking tables
4. Test endpoint manually with Postman/PowerShell

---

**Status**: ✅ Ready for Production  
**Version**: 1.0  
**Last Updated**: December 20, 2025

