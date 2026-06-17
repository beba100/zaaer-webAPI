# 🔄 VoM API Flow, JSON Parsing & Error Handling

## 📋 **Complete Flow Overview**

This document explains:
1. ✅ How data is sent to VoM API
2. ✅ JSON parsing with proper error handling
3. ✅ When `status_vom` is updated to "sent" or "failed"
4. ✅ When records are inserted into log tables

---

## 🚀 **Step-by-Step Flow**

### **1. API Call Initiation**

**Location:** `Services/InvoiceJournalEntryService.cs` → `CreateJournalEntryForInvoiceAsync()`

```csharp
// Step 1: Build VoM Journal Entry Request
var journalEntryRequest = BuildJournalEntryRequest(invoice, costCenter, vatTax?.Id, lodgingTax?.Id);

// Step 2: Send to VoM API
var response = await _voMJournalEntryService.CreateJournalEntryAsync(journalEntryRequest, "ar");
```

---

### **2. JSON Serialization (Request)**

**Location:** `Services/VoM/VoMJournalEntryService.cs` → `CreateJournalEntryAsync()`

```csharp
// Step 1: Serialize request to JSON
var serializeOptions = new JsonSerializerOptions
{
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};

var requestJson = JsonSerializer.Serialize(request, serializeOptions);

// Step 2: Remove null values (VoM doesn't accept null keys)
requestJson = JsonNullRemover.RemoveNullValues(requestJson);

// Step 3: Create HTTP content
var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
```

**Example JSON Sent:**
```json
{
  "journal_date": "10-12-2025",
  "code": "INV001",
  "memo": "قيد فاتورة رقم INV001 - الدمام 1",
  "accounts": [
    {
      "id": 51,
      "debit": 1000.00,
      "credit": 0,
      "cost_center_id": 3,
      "tax_status": 2,
      "description": "فاتورة"
    },
    {
      "id": 175,
      "debit": 0,
      "credit": 1000.00,
      "tax_status": 2,
      "description": "صندوق"
    }
  ]
}
```

---

### **3. HTTP Request with Authentication**

**Location:** `Services/VoM/VoMBaseService.cs` → `CreateAuthenticatedRequestAsync()`

```csharp
// Step 1: Get Bearer Token
var token = await AuthService.GetTokenAsync();

// Step 2: Create request with headers
var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
{
    Content = content
};

// Step 3: Add required headers
request.Headers.Add("Accept-Language", language ?? "en");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

// Step 4: Execute with retry on 401
var response = await ExecuteWithRetryAsync(request);
```

**Headers Sent:**
```
Authorization: Bearer {token}
Content-Type: application/json
Api-Agent: zapier
Accept-Language: ar
```

---

### **4. JSON Parsing (Response) with Error Handling**

**Location:** `Services/VoM/VoMJournalEntryService.cs` → `CreateJournalEntryAsync()`

#### **✅ Success Case:**
```csharp
// Step 1: Read response content
var responseContent = await response.Content.ReadAsStringAsync();

// Step 2: Configure deserialization options
var deserializeOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
};

// Step 3: Deserialize response
VoMJournalEntryResponseDto? journalEntryResponse = null;

try
{
    journalEntryResponse = JsonSerializer.Deserialize<VoMJournalEntryResponseDto>(
        responseContent,
        deserializeOptions);
}
catch (JsonException jsonEx)
{
    // ERROR HANDLING: If deserialization fails, try to parse manually
    Logger.LogWarning("[VoM Journal Entry] JSON deserialization error: {Error}. Response: {Response}", 
        jsonEx.Message, responseContent);
    
    // Try to parse as basic structure
    using (JsonDocument doc = JsonDocument.Parse(responseContent))
    {
        var root = doc.RootElement;
        journalEntryResponse = new VoMJournalEntryResponseDto
        {
            Status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetInt32() : (int)response.StatusCode,
            Success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean(),
            Data = null,
            Errors = root.TryGetProperty("errors", out var errorsProp) ? JsonSerializer.Deserialize<object>(errorsProp.GetRawText()) : null,
            Message = root.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : null
        };
    }
}
```

**Example Success Response:**
```json
{
  "status": 200,
  "success": true,
  "data": {
    "id": 12345,
    "code": "INV001",
    "journal_date": "10-12-2025",
    "memo": "قيد فاتورة رقم INV001",
    "created_at": "2025-12-10T10:30:00Z"
  },
  "errors": null,
  "message": null
}
```

#### **❌ Error Case:**
```csharp
// If all parsing fails, create minimal error response
catch
{
    journalEntryResponse = new VoMJournalEntryResponseDto
    {
        Status = (int)response.StatusCode,
        Success = false,
        Data = null,
        Errors = responseContent,
        Message = "Failed to parse response from VoM API"
    };
}
```

**Example Error Response:**
```json
{
  "status": 400,
  "success": false,
  "data": null,
  "errors": {
    "journal_date": ["Invalid date format"],
    "accounts": ["Debit and Credit must balance"]
  },
  "message": "Validation failed"
}
```

---

### **5. Update Tables Based on Response**

**Location:** `Services/InvoiceJournalEntryService.cs` → `CreateJournalEntryForInvoiceAsync()`

#### **✅ SUCCESS: Update `status_vom = "sent"`**

**Timing:** Immediately after successful VoM API response (Status 200, Success=True, Data != null)

```csharp
if (response.Success && response.Status >= 200 && response.Status < 300 && response.Data != null)
{
    // STEP 1: Update invoice.status_vom to "sent" (IMMEDIATELY)
    await UpdateInvoiceStatusAsync(invoice, journalEntryRequest, response, response.Data.Id);
    
    // STEP 2: Save to journal_entries table for audit (IMMEDIATELY)
    await SaveSuccessfulJournalEntryAsync(invoice, journalEntryRequest, response);
    
    // STEP 3: Log to VoM logger (IMMEDIATELY)
    _voMLogger?.LogInvoiceJournalEntry(...);
    
    return true;
}
```

**What Gets Updated:**

| Table | Field | Value | When |
|-------|-------|-------|------|
| `invoices` | `status_vom` | `"sent"` | ✅ Immediately after success |
| `invoices` | `vom_payload` | JSON request | ✅ Immediately after success |
| `invoices` | `vom_sent_at` | Current timestamp | ✅ Immediately after success |
| `invoices` | `vom_error` | `NULL` | ✅ Immediately after success |
| `invoice_journal_entries` | `status` | `"Sent"` | ✅ Immediately after success |
| `invoice_journal_entries` | `vom_journal_entry_id` | VoM ID | ✅ Immediately after success |

**Code:**
```csharp
private async Task UpdateInvoiceStatusAsync(...)
{
    invoice.StatusVoM = "sent";
    invoice.VomPayload = JsonSerializer.Serialize(request, ...);
    invoice.VomSentAt = KsaTime.Now;
    invoice.VomError = null;
    
    _context.Invoices.Update(invoice);
    await _context.SaveChangesAsync(); // ✅ SAVED IMMEDIATELY
}
```

---

#### **❌ FAILURE: Update `status_vom = "failed"`**

**Timing:** Immediately after failed VoM API response (Status != 200 OR Success=False)

```csharp
else
{
    // STEP 1: Build error message
    var errorMessage = response.Message ?? JsonSerializer.Serialize(response.Errors);
    var responseJson = JsonSerializer.Serialize(response, ...);
    
    // STEP 2: Update invoice.status_vom to "failed" (IMMEDIATELY)
    await UpdateInvoiceStatusFailedAsync(invoice, journalEntryRequest, errorMessage, responseJson);
    
    // STEP 3: Save to journal_entries table for audit (IMMEDIATELY)
    await SaveFailedJournalEntryAsync(invoice, errorMessage, responseJson);
    
    // STEP 4: Log to VoM logger (IMMEDIATELY)
    _voMLogger?.LogInvoiceJournalEntry(..., "Failed", errorMessage, ...);
    
    return false;
}
```

**What Gets Updated:**

| Table | Field | Value | When |
|-------|-------|-------|------|
| `invoices` | `status_vom` | `"failed"` | ✅ Immediately after failure |
| `invoices` | `vom_payload` | JSON request | ✅ Immediately after failure |
| `invoices` | `vom_error` | Error message | ✅ Immediately after failure |
| `invoices` | `vom_retry_count` | `++` (incremented) | ✅ Immediately after failure |
| `invoice_journal_entries` | `status` | `"Failed"` | ✅ Immediately after failure |
| `invoice_journal_entries` | `error_message` | Error details | ✅ Immediately after failure |

**Code:**
```csharp
private async Task UpdateInvoiceStatusFailedAsync(...)
{
    invoice.StatusVoM = "failed";
    invoice.VomPayload = JsonSerializer.Serialize(request, ...);
    invoice.VomError = errorMessage?.Length > 2000 ? errorMessage.Substring(0, 2000) : errorMessage;
    invoice.VomRetryCount++; // Increment retry count
    
    _context.Invoices.Update(invoice);
    await _context.SaveChangesAsync(); // ✅ SAVED IMMEDIATELY
}
```

---

#### **💥 EXCEPTION: Update `status_vom = "failed"`**

**Timing:** Immediately when exception occurs (network error, timeout, etc.)

```csharp
catch (Exception ex)
{
    try
    {
        // STEP 1: Update invoice.status_vom to "failed" (IMMEDIATELY)
        invoice.StatusVoM = "failed";
        invoice.VomError = ex.Message?.Length > 2000 ? ex.Message.Substring(0, 2000) : ex.Message;
        invoice.VomRetryCount++;
        _context.Invoices.Update(invoice);
        await _context.SaveChangesAsync(); // ✅ SAVED IMMEDIATELY
        
        // STEP 2: Save to journal_entries table for audit (IMMEDIATELY)
        await SaveFailedJournalEntryAsync(invoice, ex.Message ?? "Unknown error", null);
    }
    catch (Exception saveEx)
    {
        _logger?.LogError(saveEx, "Failed to save error record");
    }
    
    return false;
}
```

---

## 📊 **Complete Timeline**

```
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 0ms                                                      │
│  ACTION: User clicks "Send to VoM"                             │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 10-50ms                                                  │
│  ACTION: Build VoM Journal Entry Request                       │
│  - Validate invoice                                             │
│  - Get Cost Center                                              │
│  - Get Tax IDs                                                  │
│  - Build accounts array                                         │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 50-100ms                                                 │
│  ACTION: Serialize JSON Request                                │
│  - JsonSerializer.Serialize(request)                            │
│  - Remove null values                                           │
│  - Create StringContent                                         │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 100-200ms                                                │
│  ACTION: Get Bearer Token                                       │
│  - Check cached token                                           │
│  - Or login to VoM API                                          │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 200-500ms                                                │
│  ACTION: Send HTTP Request to VoM                              │
│  POST https://kimoo.getvom.com/api/accounting/journal-entries  │
│  Headers: Authorization, Content-Type, Api-Agent                │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 500-2000ms (Network Latency)                            │
│  ACTION: Wait for VoM API Response                             │
│  - Network round-trip                                           │
│  - VoM processing time                                          │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 2000-2100ms                                              │
│  ACTION: Read Response Content                                  │
│  - response.Content.ReadAsStringAsync()                        │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 2100-2200ms                                              │
│  ACTION: Parse JSON Response                                    │
│  - Try JsonSerializer.Deserialize()                            │
│  - If fails: Try JsonDocument.Parse()                          │
│  - If fails: Create minimal error response                     │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 2200-2300ms                                              │
│  ACTION: Check Response Status                                  │
│  - If Success && Status 200 && Data != null:                   │
│    → Update status_vom = "sent"                                │
│  - Else:                                                        │
│    → Update status_vom = "failed"                              │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 2300-2400ms                                              │
│  ACTION: Save to Database (IMMEDIATELY)                        │
│  ✅ Update invoices.status_vom                                  │
│  ✅ Update invoices.vom_payload                                 │
│  ✅ Update invoices.vom_sent_at (or vom_error)                 │
│  ✅ Insert into invoice_journal_entries                         │
│  ✅ Log to VoM logger                                           │
└─────────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────────┐
│  TIME: 2400ms                                                   │
│  RESULT: Return success/failure to caller                      │
└─────────────────────────────────────────────────────────────────┘
```

**Total Time:** ~2-3 seconds (depending on network latency)

---

## 🔍 **Error Handling Details**

### **1. JSON Serialization Errors**

**Handled By:** `JsonSerializer.Serialize()` with proper options

```csharp
// ✅ Handles null values automatically
var serializeOptions = new JsonSerializerOptions
{
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
};

// ✅ Removes null keys (VoM doesn't accept them)
requestJson = JsonNullRemover.RemoveNullValues(requestJson);
```

**If Error:** Request is never sent (exception thrown before HTTP call)

---

### **2. HTTP Request Errors**

**Handled By:** `ExecuteWithRetryAsync()` in `VoMBaseService.cs`

```csharp
// ✅ Automatic token refresh on 401
if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
{
    Logger.LogWarning("Unauthorized response. Attempting to refresh token...");
    var token = await AuthService.RefreshTokenAsync();
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    response = await HttpClient.SendAsync(request); // Retry once
}
```

**If Error:** Exception thrown, caught in `CreateJournalEntryForInvoiceAsync()`

---

### **3. JSON Deserialization Errors**

**Handled By:** Multi-layer fallback in `VoMJournalEntryService.cs`

```csharp
try
{
    // ✅ Try 1: Standard deserialization
    journalEntryResponse = JsonSerializer.Deserialize<VoMJournalEntryResponseDto>(...);
}
catch (JsonException jsonEx)
{
    try
    {
        // ✅ Try 2: Manual parsing with JsonDocument
        using (JsonDocument doc = JsonDocument.Parse(responseContent))
        {
            // Extract status, success, errors manually
        }
    }
    catch
    {
        // ✅ Try 3: Create minimal error response
        journalEntryResponse = new VoMJournalEntryResponseDto
        {
            Status = (int)response.StatusCode,
            Success = false,
            Errors = responseContent
        };
    }
}
```

**If Error:** Response is still processed (with error status)

---

### **4. Database Save Errors**

**Handled By:** Try-catch in update methods

```csharp
private async Task UpdateInvoiceStatusAsync(...)
{
    try
    {
        invoice.StatusVoM = "sent";
        _context.Invoices.Update(invoice);
        await _context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        // ✅ Log error but don't throw
        // VoM send was successful, just status update failed
        _logger?.LogError(ex, "Failed to update invoice status_vom");
    }
}
```

**If Error:** Logged but doesn't fail the entire operation

---

## 📝 **Summary Table**

| Event | When | What Gets Updated | Database Table |
|-------|------|-------------------|----------------|
| **✅ Success** | Immediately after VoM API returns 200 OK | `status_vom = "sent"`<br>`vom_payload`<br>`vom_sent_at`<br>`vom_error = NULL` | `invoices` |
| **✅ Success** | Immediately after VoM API returns 200 OK | `status = "Sent"`<br>`vom_journal_entry_id`<br>`vom_response` | `invoice_journal_entries` |
| **❌ Failure** | Immediately after VoM API returns error | `status_vom = "failed"`<br>`vom_payload`<br>`vom_error`<br>`vom_retry_count++` | `invoices` |
| **❌ Failure** | Immediately after VoM API returns error | `status = "Failed"`<br>`error_message`<br>`vom_response` | `invoice_journal_entries` |
| **💥 Exception** | Immediately when exception occurs | `status_vom = "failed"`<br>`vom_error`<br>`vom_retry_count++` | `invoices` |
| **💥 Exception** | Immediately when exception occurs | `status = "Failed"`<br>`error_message` | `invoice_journal_entries` |

---

## 🎯 **Key Points**

1. ✅ **All updates happen IMMEDIATELY** after VoM API response (no delays)
2. ✅ **JSON parsing has 3-layer fallback** (standard → manual → minimal)
3. ✅ **Database saves are wrapped in try-catch** (won't fail entire operation)
4. ✅ **Both `invoices` and `invoice_journal_entries` are updated** (dual tracking)
5. ✅ **Error messages are truncated** to 2000 characters (database limit)
6. ✅ **Retry count is incremented** on failure (for retry logic)

---

**Date:** December 20, 2025  
**Status:** ✅ Production Ready  
**Architecture:** status_vom on tables (new)

