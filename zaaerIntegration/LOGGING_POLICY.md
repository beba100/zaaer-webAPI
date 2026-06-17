# 📋 Logging Policy - Production Guidelines

## 🎯 Objective

**Logs should be a diagnostic tool, not a history of every system operation.**

---

## ✅ What to Log (Production Mode)

### 1. **Error Level**
- Database failures
- External API failures (VoM, Zaaer)
- Data inconsistency
- Unexpected exceptions

### 2. **Critical Level**
- Security breaches
- Data corruption
- System failures that require immediate attention

### 3. **Warning Level** (Only Real Issues)
- Retry attempts (max 3)
- Missing configuration
- Deprecated API usage
- Performance degradation warnings

### 4. **Security/Auth Issues**
- Failed authentication attempts
- Unauthorized access attempts
- Token expiration

---

## ❌ What NOT to Log (Production Mode)

- ❌ Debug/Trace/Verbose logs
- ❌ Routine Info logs (form loading, data fetching, normal operations)
- ❌ Success messages for normal operations
- ❌ Logs inside loops or bulk operations (except Start/End/Error)
- ❌ StackTrace for Warning level (only for Error/Critical)

---

## 📊 Log Categories

### Separate Log Files:
1. **errors.log** - All errors and exceptions
2. **security.log** - Authentication and authorization events
3. **sync.log** - External system sync (VoM, Zaaer)
4. **database.log** - Database operations and failures
5. **api.log** - API requests/responses (only errors)

---

## 🔄 Duplicate Error Handling

**Same error repeating?**
- ✅ Log first occurrence
- ✅ Update occurrence counter
- ✅ Update last occurrence timestamp
- ❌ Don't log every repetition

**Example:**
```
[ERROR][SYNC] Failed to sync invoice 123 to VoM (Occurrences: 5, Last: 2025-12-31 14:30:00)
```

---

## 📝 Required Context in Every Log

Every log entry MUST include:
- ✅ **HotelId** (if applicable)
- ✅ **UserId** (if available)
- ✅ **Action/Endpoint** (what operation)
- ✅ **EntityId** (InvoiceId, ReceiptId, OrderId...)
- ✅ **Exception Message** (if error)
- ✅ **InnerException** (if exists)

**Optional (only for Error/Critical):**
- StackTrace (only if needed for debugging)

---

## 🚫 No Logging in Loops/Bulk Operations

### ❌ BAD:
```csharp
foreach (var invoice in invoices)
{
    _logger.LogInformation("Processing invoice {InvoiceId}", invoice.Id); // ❌
}
```

### ✅ GOOD:
```csharp
_logger.LogInformation("Starting bulk invoice sync: {Count} invoices", invoices.Count);

try
{
    foreach (var invoice in invoices)
    {
        // Process without logging
    }
    
    _logger.LogInformation("Bulk invoice sync completed: {Count} invoices", invoices.Count);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Bulk invoice sync failed after processing {ProcessedCount} invoices", processedCount);
}
```

---

## ⚙️ Configuration Control

### appsettings.json:
```json
{
  "Logging": {
    "MinimumLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "System": "Warning"
    },
    "LogCategories": {
      "Sync": "Error",
      "Database": "Error",
      "Security": "Warning",
      "Api": "Error"
    }
  }
}
```

### Environment Variable Override:
```
ASPNETCORE_LOGGING__MINIMUMLEVEL__DEFAULT=Warning
```

---

## 🏷️ Log Tags/Categories

Every log should have clear tags:

- `[ERROR][DB]` - Database error
- `[ERROR][SYNC]` - Sync error
- `[ERROR][API]` - API error
- `[WARN][SECURITY]` - Security warning
- `[WARN][PERFORMANCE]` - Performance warning
- `[CRITICAL][SECURITY]` - Critical security issue

---

## 📈 Target Metrics

- **Error logs:** < 1% of total logs
- **Warning logs:** < 5% of total logs
- **Info logs:** 0% in production (only in Development)

---

## 🔍 Example: Good Log Entry

```csharp
_logger.LogError(
    "[ERROR][SYNC] Failed to sync invoice to VoM | " +
    "HotelId: {HotelId}, InvoiceId: {InvoiceId}, UserId: {UserId} | " +
    "Error: {ErrorMessage}",
    hotelId, invoiceId, userId, ex.Message);
```

**Output:**
```
[2025-12-31 14:30:00] [ERROR][SYNC] Failed to sync invoice to VoM | HotelId: 3, InvoiceId: 123, UserId: 24 | Error: Connection timeout
```

---

## ✅ Implementation Checklist

- [ ] Update appsettings.json: Default level = Warning
- [ ] Configure Serilog with category-based file separation
- [ ] Implement duplicate error detection (occurrence counter)
- [ ] Remove Info logs from production code
- [ ] Add required context (HotelId, UserId, Action, EntityId) to all logs
- [ ] Remove logs from loops/bulk operations
- [ ] Add log tags/categories
- [ ] Test logging configuration
- [ ] Document logging guidelines for team

