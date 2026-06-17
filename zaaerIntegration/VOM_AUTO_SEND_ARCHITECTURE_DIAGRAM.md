# 🎨 VoM Auto Send Job - Visual Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      🌐 MonsterASP.net (Hosting Platform)                    │
│                                                                               │
│  ⏰ Scheduled Task: "VoM Auto Send Job - Daily"                             │
│     Schedule: Daily at 00:00 (midnight)                                      │
│     URL: http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send   │
│     Header: X-API-Key: VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f        │
│                                                                               │
└──────────────────────────────────┬──────────────────────────────────────────┘
                                   │
                                   │ HTTP POST
                                   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    🖥️  ASP.NET Core Web Application                          │
│                     (aleairy.tryasp.net)                                     │
│                                                                               │
│  📂 Controllers/Jobs/VoMAutoSendJobController.cs                            │
│                                                                               │
│  1️⃣  Validate API Key                                                        │
│      ↓ Match with appsettings.json                                          │
│      ✅ Valid: Continue                                                      │
│      ❌ Invalid: Return 401 Unauthorized                                     │
│                                                                               │
│  2️⃣  Get All Tenants from Master Database                                   │
│      ↓ MasterDbContext.Tenants                                               │
│      Example: [Dammam1, Makkah1, Riyadh1, Jeddah1, Taif1]                  │
│                                                                               │
│  3️⃣  For Each Tenant (Loop):                                                 │
│      ┌──────────────────────────────────────────────────────────────┐       │
│      │  🏨 Tenant: Dammam1                                           │       │
│      │                                                                │       │
│      │  A. Connect to Tenant Database (ApplicationDbContext)         │       │
│      │     Connection String: From Master DB or Tenant.Connection    │       │
│      │                                                                │       │
│      │  B. Find Unsent/Failed Records:                               │       │
│      │     ┌────────────────────────────────────────────────────┐   │       │
│      │     │ 📄 INVOICES (invoice_journal_entries)              │   │       │
│      │     │   WHERE:                                            │   │       │
│      │     │     - No entry (new invoice)                        │   │       │
│      │     │     - OR status = 'Pending'                         │   │       │
│      │     │     - OR (status = 'Failed' AND retry_count < 3)   │   │       │
│      │     │   LIMIT: 100 (batchSize)                            │   │       │
│      │     └────────────────────────────────────────────────────┘   │       │
│      │                                                                │       │
│      │     ┌────────────────────────────────────────────────────┐   │       │
│      │     │ 🔄 CREDIT NOTES (credit_note_journal_entries)      │   │       │
│      │     │   WHERE: (same logic as invoices)                   │   │       │
│      │     │   LIMIT: 100                                        │   │       │
│      │     └────────────────────────────────────────────────────┘   │       │
│      │                                                                │       │
│      │     ┌────────────────────────────────────────────────────┐   │       │
│      │     │ 💰 PAYMENT RECEIPTS (payment_receipt_journal_...)  │   │       │
│      │     │   WHERE: (same logic as invoices)                   │   │       │
│      │     │   LIMIT: 100                                        │   │       │
│      │     └────────────────────────────────────────────────────┘   │       │
│      │                                                                │       │
│      │  C. Process Each Record:                                      │       │
│      │     For each invoice/credit note/payment receipt found:       │       │
│      │     ↓                                                          │       │
│      └──────┼──────────────────────────────────────────────────────┘       │
│             │                                                                │
│             ▼                                                                │
│  ┌─────────────────────────────────────────────────────────────────┐       │
│  │        📋 Journal Entry Services                                 │       │
│  │                                                                   │       │
│  │  ▪ InvoiceJournalEntryService                                   │       │
│  │    └─→ CreateJournalEntryForInvoiceAsync()                      │       │
│  │                                                                   │       │
│  │  ▪ CreditNoteJournalEntryService                                │       │
│  │    └─→ CreateReverseJournalEntryForCreditNoteAsync()            │       │
│  │                                                                   │       │
│  │  ▪ PaymentReceiptJournalEntryService                            │       │
│  │    └─→ CreateJournalEntryForPaymentReceiptAsync()               │       │
│  │                                                                   │       │
│  │  Each service does:                                              │       │
│  │  1. Check if already sent (invoice_zaaer_id)                    │       │
│  │  2. Build journal entry payload (accounts, debit, credit)        │       │
│  │  3. Call VoMJournalEntryService ─────────────┐                  │       │
│  └──────────────────────────────────────────────┼──────────────────┘       │
│                                                  │                           │
│                                                  ▼                           │
│  ┌─────────────────────────────────────────────────────────────────┐       │
│  │        🔌 VoMJournalEntryService                                 │       │
│  │                                                                   │       │
│  │  CreateJournalEntryAsync(request)                                │       │
│  │  1. Validate journal entry (balanced, accounts exist)            │       │
│  │  2. Get VoM authentication token                                 │       │
│  │  3. Build HTTP request                                           │       │
│  │  4. POST to VoM API ─────────────────────────────────┐           │       │
│  └──────────────────────────────────────────────────────┼───────────┘       │
│                                                          │                   │
└──────────────────────────────────────────────────────────┼───────────────────┘
                                                           │
                                                           │ HTTPS POST
                                                           ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         🌍 VoM External API                                  │
│                      (Ministry of Finance System)                            │
│                                                                               │
│  Endpoint: POST /api/accounting/journal-entries                             │
│                                                                               │
│  Request Body:                                                               │
│  {                                                                           │
│    "journal_date": "2025-12-20",                                            │
│    "code": "INV00046",                                                      │
│    "memo": "Invoice #INV00046",                                             │
│    "accounts": [                                                             │
│      { "id": 123, "debit": 1000.00, "credit": 0.00 },                      │
│      { "id": 456, "debit": 0.00, "credit": 150.00 },                       │
│      { "id": 789, "debit": 0.00, "credit": 850.00 }                        │
│    ]                                                                         │
│  }                                                                           │
│                                                                               │
│  ✅ Success Response:                                                        │
│  {                                                                           │
│    "success": true,                                                          │
│    "data": {                                                                 │
│      "id": 98765,  ← VoM Journal Entry ID                                   │
│      "code": "INV00046",                                                    │
│      ...                                                                     │
│    }                                                                         │
│  }                                                                           │
│                                                                               │
└──────────────────────────────────┬──────────────────────────────────────────┘
                                   │
                                   │ Response
                                   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                    🖥️  ASP.NET Core Application (continued)                  │
│                                                                               │
│  VoMJournalEntryService receives response                                   │
│  ↓                                                                            │
│  Returns to InvoiceJournalEntryService                                      │
│  ↓                                                                            │
│  Update Database Tracking Table:                                            │
│  ┌─────────────────────────────────────────────────────────────────┐       │
│  │  invoice_journal_entries                                         │       │
│  │  ─────────────────────────────────────────────────────────────  │       │
│  │  UPDATE invoice_journal_entries SET                              │       │
│  │    status = 'Sent',                                              │       │
│  │    vom_journal_entry_id = 98765,                                 │       │
│  │    vom_response = '{...full response...}',                       │       │
│  │    error_message = NULL,                                         │       │
│  │    updated_at = GETDATE()                                        │       │
│  │  WHERE invoice_zaaer_id = 12345;                                 │       │
│  └─────────────────────────────────────────────────────────────────┘       │
│                                                                               │
│  ✅ Record marked as "Sent" - won't be processed again!                     │
│                                                                               │
│  Repeat for all records in batch (up to 100)...                             │
│  Repeat for all tenants...                                                  │
│                                                                               │
│  4️⃣  Return Summary Statistics                                               │
│      {                                                                       │
│        "success": true,                                                      │
│        "duration": "00:05:23",                                               │
│        "invoices": { "sent": 42, "failed": 3, "total": 45 },               │
│        "creditNotes": { "sent": 12, "failed": 0, "total": 12 },            │
│        "paymentReceipts": { "sent": 75, "failed": 3, "total": 78 },        │
│        "summary": {                                                          │
│          "totalRecords": 135,                                                │
│          "totalSent": 129,                                                   │
│          "successRate": 95.56                                                │
│        }                                                                     │
│      }                                                                       │
│                                                                               │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         📊 DATABASE TRACKING                                 │
│                                                                               │
│  🗄️  Tenant Database (e.g., Dammam1DB)                                      │
│                                                                               │
│  ┌────────────────────────────────────────────────────────┐                │
│  │  invoice_journal_entries                               │                │
│  ├────┬────────────┬────────────┬──────────┬──────────────┤                │
│  │ id │ invoice_id │ zaaer_id   │ status   │ vom_id       │                │
│  ├────┼────────────┼────────────┼──────────┼──────────────┤                │
│  │ 1  │ 123        │ 12345      │ Sent     │ 98765        │                │
│  │ 2  │ 124        │ 12346      │ Pending  │ NULL         │                │
│  │ 3  │ 125        │ 12347      │ Failed   │ NULL         │                │
│  └────┴────────────┴────────────┴──────────┴──────────────┘                │
│                                                                               │
│  ┌────────────────────────────────────────────────────────┐                │
│  │  credit_note_journal_entries                           │                │
│  ├────┬────────────┬────────────┬──────────┬──────────────┤                │
│  │ id │ cn_id      │ zaaer_id   │ status   │ vom_id       │                │
│  ├────┼────────────┼────────────┼──────────┼──────────────┤                │
│  │ 1  │ 50         │ 5001       │ Sent     │ 87654        │                │
│  │ 2  │ 51         │ 5002       │ Pending  │ NULL         │                │
│  └────┴────────────┴────────────┴──────────┴──────────────┘                │
│                                                                               │
│  ┌────────────────────────────────────────────────────────┐                │
│  │  payment_receipt_journal_entries                       │                │
│  ├────┬────────────┬────────────┬──────────┬──────────────┤                │
│  │ id │ receipt_id │ zaaer_id   │ status   │ vom_id       │                │
│  ├────┼────────────┼────────────┼──────────┼──────────────┤                │
│  │ 1  │ 789        │ 78900      │ Sent     │ 55555        │                │
│  │ 2  │ 790        │ 78901      │ Sent     │ 55556        │                │
│  │ 3  │ 791        │ 78902      │ Failed   │ NULL         │                │
│  └────┴────────────┴────────────┴──────────┴──────────────┘                │
│                                                                               │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                           📝 LOGGING OUTPUT                                  │
│                                                                               │
│  File: logs/log-20251220.txt                                                │
│                                                                               │
│  [2025-12-20 00:00:00] ========================================             │
│  [2025-12-20 00:00:00] [VoM Auto Send Job] 🚀 Job Started                  │
│  [2025-12-20 00:00:00] [VoM Auto Send Job] ✅ API Key validated            │
│  [2025-12-20 00:00:01] [VoM Auto Send Job] Found 5 active tenants          │
│  [2025-12-20 00:00:01] [VoM Auto Send Job] ➡️ Processing: Dammam1          │
│  [2025-12-20 00:00:02] [VoM Auto Send Job] 📄 Processing Invoices...       │
│  [2025-12-20 00:00:03] [VoM Auto Send Job] Found 45 invoices to send       │
│  [2025-12-20 00:00:05] [VoM Auto Send Job] ✅ INV00046 sent successfully   │
│  [2025-12-20 00:00:07] [VoM Auto Send Job] ✅ INV00047 sent successfully   │
│  [2025-12-20 00:00:09] [VoM Auto Send Job] ❌ INV00048 failed: Invalid     │
│  ...                                                                          │
│  [2025-12-20 00:05:23] [VoM Auto Send Job] ✅ Job Completed                │
│  [2025-12-20 00:05:23] [VoM Auto Send Job] Duration: 00:05:23              │
│  [2025-12-20 00:05:23] [VoM Auto Send Job] Total Sent: 129/135             │
│  [2025-12-20 00:05:23] ========================================             │
│                                                                               │
└─────────────────────────────────────────────────────────────────────────────┘
```

## 🔄 Process Flow Summary

```
1. MonsterASP triggers job at midnight
   ↓
2. Controller validates API key
   ↓
3. Get all tenants from master DB
   ↓
4. For each tenant:
   a. Connect to tenant database
   b. Find unsent/failed records
   c. Process invoices → Send to VoM → Update status
   d. Process credit notes → Send to VoM → Update status
   e. Process payment receipts → Send to VoM → Update status
   ↓
5. Collect statistics
   ↓
6. Return summary JSON
   ↓
7. Log everything
```

## 🎯 Key Points

- ✅ **Runs Daily**: Automatic at midnight
- ✅ **Multi-Tenant**: All hotels processed
- ✅ **Smart Retry**: Failed records retry up to 3 times
- ✅ **Idempotent**: Won't send same record twice
- ✅ **Tracked**: Database records all statuses
- ✅ **Logged**: Complete audit trail
- ✅ **Secure**: API Key protected
- ✅ **Fast**: Batch processing (100 records/hotel)

