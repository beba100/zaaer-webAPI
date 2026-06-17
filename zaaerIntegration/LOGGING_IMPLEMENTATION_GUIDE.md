# 📋 Logging Implementation Guide - Production Ready

## ✅ What Has Been Implemented

### 1. **appsettings.json Updated**
- Default log level: **Warning** (production mode)
- Category-based log level overrides
- Serilog configuration with file separation

### 2. **Program.cs Updated**
- Serilog configured with **category-based file separation**:
  - `logs/errors/errors-.txt` - All errors and critical
  - `logs/security/security-.txt` - Security/Auth events
  - `logs/sync/sync-.txt` - External system sync (VoM, Zaaer)
  - `logs/database/database-.txt` - Database operations
  - `logs/log-.txt` - General logs (Warning+)
  - `logs/performance/performance-.txt` - Performance metrics

### 3. **SmartLogger Service Created**
- **Duplicate error detection** (occurrence counter)
- **Context builder** (HotelId, UserId, Action, EntityId)
- **Automatic cleanup** of old error occurrences

---

## 📝 Next Steps (Required)

### Step 1: Register SmartLogger Service

**File:** `Program.cs`

Add after line 154 (after VoMLogger registration):

```csharp
// Register Smart Logger (duplicate error detection + context)
builder.Services.AddSingleton<SmartLogger>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("SmartLogger");
    return new SmartLogger(logger);
});
```

---

### Step 2: Update Services to Use SmartLogger

**Replace excessive LogInformation with SmartLogger:**

#### Example: PaymentReceiptJournalEntryService.cs

**❌ REMOVE (Routine Info logs):**
```csharp
_logger?.LogInformation("[Payment Receipt Journal Entry] 🔍 Checking for existing VoM journal entry...");
_logger?.LogInformation("[Payment Receipt Journal Entry] Building journal entry...");
_logger?.LogInformation("[Payment Receipt Journal Entry] ✅ Successfully sent journal entry...");
```

**✅ KEEP (Only Errors/Warnings):**
```csharp
// Use SmartLogger for errors
_smartLogger.LogError(
    category: "SYNC",
    message: "Failed to sync payment receipt to VoM",
    hotelId: receipt.HotelId,
    userId: receipt.CreatedBy,
    action: "CreateJournalEntry",
    entityId: receipt.ReceiptId,
    exception: ex);

// Use SmartLogger for warnings (real issues only)
_smartLogger.LogWarning(
    category: "SYNC",
    message: "VoM API returned warning",
    hotelId: receipt.HotelId,
    action: "CreateJournalEntry",
    entityId: receipt.ReceiptId);
```

---

### Step 3: Remove Logs from Loops/Bulk Operations

**❌ BAD:**
```csharp
foreach (var invoice in invoices)
{
    _logger.LogInformation("Processing invoice {InvoiceId}", invoice.Id); // ❌
    await ProcessInvoice(invoice);
}
```

**✅ GOOD:**
```csharp
_logger.LogWarning("[SYNC] Starting bulk invoice sync: {Count} invoices", invoices.Count);

try
{
    int processedCount = 0;
    foreach (var invoice in invoices)
    {
        await ProcessInvoice(invoice);
        processedCount++;
    }
    
    _logger.LogWarning("[SYNC] Bulk invoice sync completed: {Count} invoices", processedCount);
}
catch (Exception ex)
{
    _smartLogger.LogError(
        category: "SYNC",
        message: "Bulk invoice sync failed",
        action: "BulkSync",
        exception: ex);
}
```

---

### Step 4: Add Required Context to All Error Logs

**Every error log MUST include:**
- ✅ HotelId (if applicable)
- ✅ UserId (if available)
- ✅ Action/Endpoint
- ✅ EntityId (InvoiceId, ReceiptId, OrderId...)

**Example:**
```csharp
_smartLogger.LogError(
    category: "DB",                    // Category tag
    message: "Failed to save invoice", // Clear message
    hotelId: invoice.HotelId,          // ✅ Required context
    userId: invoice.CreatedBy,         // ✅ Required context
    action: "SaveInvoice",             // ✅ Required context
    entityId: invoice.InvoiceId,       // ✅ Required context
    exception: ex);                    // ✅ Exception with message
```

---

### Step 5: Update Log Tags/Categories

**Use clear tags in log messages:**

- `[ERROR][DB]` - Database error
- `[ERROR][SYNC]` - Sync error
- `[ERROR][API]` - API error
- `[ERROR][VoM]` - VoM-specific error
- `[WARN][SECURITY]` - Security warning
- `[WARN][PERFORMANCE]` - Performance warning
- `[CRITICAL][SECURITY]` - Critical security issue

---

## 🎯 Target Metrics

After implementation:
- **Error logs:** < 1% of total logs
- **Warning logs:** < 5% of total logs
- **Info logs:** 0% in production

---

## 📊 Log File Structure

```
logs/
├── errors/
│   └── errors-2025-12-31.txt      ← All errors + critical
├── security/
│   └── security-2025-12-31.txt    ← Security/Auth events
├── sync/
│   └── sync-2025-12-31.txt        ← VoM/Zaaer sync errors
├── database/
│   └── database-2025-12-31.txt    ← DB operation errors
├── performance/
│   └── performance-2025-12-31.txt ← Performance metrics
└── log-2025-12-31.txt             ← General logs (Warning+)
```

---

## ⚙️ Configuration Control

### Change Log Level via appsettings.json:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"  // Change to "Information" for debugging
    }
  }
}
```

### Environment Variable Override:

```bash
ASPNETCORE_LOGGING__LOGLEVEL__DEFAULT=Information
```

---

## ✅ Implementation Checklist

- [x] Update appsettings.json: Default level = Warning
- [x] Configure Serilog with category-based file separation
- [x] Create SmartLogger service with duplicate error detection
- [ ] Register SmartLogger in Program.cs
- [ ] Remove Info logs from PaymentReceiptJournalEntryService
- [ ] Remove Info logs from VoMJournalEntryService
- [ ] Remove Info logs from other services
- [ ] Add required context (HotelId, UserId, Action, EntityId) to all error logs
- [ ] Remove logs from loops/bulk operations
- [ ] Add log tags/categories
- [ ] Test logging configuration
- [ ] Verify log files are created correctly

---

## 🔍 Testing

1. **Test Error Logging:**
```csharp
_smartLogger.LogError("SYNC", "Test error", hotelId: 3, userId: 24, action: "Test", entityId: 123);
```
**Expected:** Log appears in `logs/errors/errors-.txt` and `logs/sync/sync-.txt`

2. **Test Duplicate Detection:**
```csharp
// Call same error 5 times
for (int i = 0; i < 5; i++)
{
    _smartLogger.LogError("SYNC", "Duplicate test", hotelId: 3, entityId: 123);
}
```
**Expected:** Only 1 log entry with "Occurrences: 5"

3. **Test Category Separation:**
- Error with `[SECURITY]` → Should appear in `logs/security/security-.txt`
- Error with `[SYNC]` → Should appear in `logs/sync/sync-.txt`
- Error with `[DB]` → Should appear in `logs/database/database-.txt`

---

## 📚 Reference

See `LOGGING_POLICY.md` for complete logging policy and guidelines.

