# 🚀 VoM Auto Send Job - Quick Test Script

## Test the job locally before deploying to MonsterASP

### 1️⃣ Test with PowerShell (Windows)

```powershell
# Set your API key (from appsettings.json)
$apiKey = "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f"

# Set your URL (local or production)
$url = "https://localhost:7095/api/jobs/VoMAutoSendJob/vom-auto-send"
# OR for production:
# $url = "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send"

# Prepare headers
$headers = @{
    "X-API-Key" = $apiKey
    "Content-Type" = "application/json"
}

# Execute request
Write-Host "🚀 Testing VoM Auto Send Job..." -ForegroundColor Cyan
Write-Host "URL: $url" -ForegroundColor Yellow
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri $url `
        -Method POST `
        -Headers $headers `
        -TimeoutSec 300
    
    Write-Host "✅ SUCCESS!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📊 Job Statistics:" -ForegroundColor Cyan
    Write-Host "─────────────────────────────────────" -ForegroundColor Gray
    Write-Host "Duration: $($response.duration)" -ForegroundColor White
    Write-Host ""
    Write-Host "📄 Invoices:" -ForegroundColor Yellow
    Write-Host "  Total: $($response.invoices.total)" -ForegroundColor White
    Write-Host "  Sent: $($response.invoices.sent)" -ForegroundColor Green
    Write-Host "  Failed: $($response.invoices.failed)" -ForegroundColor Red
    Write-Host ""
    Write-Host "🔄 Credit Notes:" -ForegroundColor Yellow
    Write-Host "  Total: $($response.creditNotes.total)" -ForegroundColor White
    Write-Host "  Sent: $($response.creditNotes.sent)" -ForegroundColor Green
    Write-Host "  Failed: $($response.creditNotes.failed)" -ForegroundColor Red
    Write-Host ""
    Write-Host "💰 Payment Receipts:" -ForegroundColor Yellow
    Write-Host "  Total: $($response.paymentReceipts.total)" -ForegroundColor White
    Write-Host "  Sent: $($response.paymentReceipts.sent)" -ForegroundColor Green
    Write-Host "  Failed: $($response.paymentReceipts.failed)" -ForegroundColor Red
    Write-Host ""
    Write-Host "📈 Summary:" -ForegroundColor Cyan
    Write-Host "  Total Records: $($response.summary.totalRecords)" -ForegroundColor White
    Write-Host "  Total Sent: $($response.summary.totalSent)" -ForegroundColor Green
    Write-Host "  Success Rate: $($response.summary.successRate)%" -ForegroundColor Green
    Write-Host "─────────────────────────────────────" -ForegroundColor Gray
    
    # Display full response
    Write-Host ""
    Write-Host "📋 Full Response:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 10 | Write-Host
}
catch {
    Write-Host "❌ ERROR!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error Message:" -ForegroundColor Yellow
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Yellow
        Write-Host "Status Description: $($_.Exception.Response.StatusDescription)" -ForegroundColor Yellow
    }
}
```

---

### 2️⃣ Test with curl (Linux/Mac/Git Bash)

```bash
#!/bin/bash

# Set your API key (from appsettings.json)
API_KEY="VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f"

# Set your URL (local or production)
URL="https://localhost:7095/api/jobs/VoMAutoSendJob/vom-auto-send"
# OR for production:
# URL="http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send"

echo "🚀 Testing VoM Auto Send Job..."
echo "URL: $URL"
echo ""

# Execute request
curl -X POST "$URL" \
     -H "X-API-Key: $API_KEY" \
     -H "Content-Type: application/json" \
     -w "\n\n⏱️  Time: %{time_total}s\n📊 Status: %{http_code}\n" \
     -s | jq '.'
```

---

### 3️⃣ Test with Postman

**Import this collection:**

```json
{
  "info": {
    "name": "VoM Auto Send Job",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Execute VoM Auto Send Job",
      "request": {
        "method": "POST",
        "header": [
          {
            "key": "X-API-Key",
            "value": "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f",
            "type": "text"
          },
          {
            "key": "Content-Type",
            "value": "application/json",
            "type": "text"
          }
        ],
        "url": {
          "raw": "https://localhost:7095/api/jobs/VoMAutoSendJob/vom-auto-send",
          "protocol": "https",
          "host": [
            "localhost"
          ],
          "port": "7095",
          "path": [
            "api",
            "jobs",
            "VoMAutoSendJob",
            "vom-auto-send"
          ]
        }
      }
    },
    {
      "name": "Execute with Custom Parameters",
      "request": {
        "method": "POST",
        "header": [
          {
            "key": "X-API-Key",
            "value": "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f",
            "type": "text"
          }
        ],
        "url": {
          "raw": "https://localhost:7095/api/jobs/VoMAutoSendJob/vom-auto-send?maxRetries=5&batchSize=50",
          "protocol": "https",
          "host": [
            "localhost"
          ],
          "port": "7095",
          "path": [
            "api",
            "jobs",
            "VoMAutoSendJob",
            "vom-auto-send"
          ],
          "query": [
            {
              "key": "maxRetries",
              "value": "5"
            },
            {
              "key": "batchSize",
              "value": "50"
            }
          ]
        }
      }
    }
  ]
}
```

---

### 4️⃣ Test Invalid API Key (Security Test)

```powershell
# Test with wrong API key - should return 401 Unauthorized
$headers = @{
    "X-API-Key" = "wrong-api-key-12345"
}

Invoke-RestMethod -Uri "https://localhost:7095/api/jobs/VoMAutoSendJob/vom-auto-send" `
    -Method POST `
    -Headers $headers
```

**Expected Result:** 
```json
{
  "error": "Invalid API Key"
}
```
**Status Code:** 401 Unauthorized

---

### 5️⃣ Monitor Logs After Test

After running the job, check your logs folder:

```powershell
# View latest log file
Get-Content "C:\zaaerIntegration\zaaerIntegration\logs\log-*.txt" -Tail 200 | Select-String "VoM Auto Send"
```

**Look for:**
- `[VoM Auto Send Job] 🚀 Job Started`
- `[VoM Auto Send Job] ✅ API Key validated`
- `[VoM Auto Send Job] Found X active tenants`
- `[VoM Auto Send Job] ✅ Invoice INV00046 sent successfully`
- `[VoM Auto Send Job] ✅ Job Completed Successfully`

---

## 🎯 Quick Reference

| Parameter | Default | Description |
|-----------|---------|-------------|
| `maxRetries` | 3 | Max retry attempts for failed records |
| `batchSize` | 100 | Records to process per hotel |

### Example URLs:

**Default:**
```
POST /api/jobs/VoMAutoSendJob/vom-auto-send
```

**With custom batch size:**
```
POST /api/jobs/VoMAutoSendJob/vom-auto-send?batchSize=50
```

**With custom retries:**
```
POST /api/jobs/VoMAutoSendJob/vom-auto-send?maxRetries=5
```

**Both parameters:**
```
POST /api/jobs/VoMAutoSendJob/vom-auto-send?maxRetries=5&batchSize=200
```

---

## ✅ Success Indicators

1. **HTTP Status**: 200 OK
2. **Response**: `"success": true`
3. **Logs**: No errors in application logs
4. **Database**: Records marked as "Sent" in `invoice_journal_entries`, `payment_receipt_journal_entries` tables
5. **VoM**: Records appear in VoM system

---

## 🐛 Troubleshooting

### Issue: SSL Certificate Error (localhost)
```powershell
# Skip SSL validation for localhost testing only
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
```

### Issue: Timeout
```powershell
# Increase timeout to 10 minutes
Invoke-RestMethod -Uri $url -Method POST -Headers $headers -TimeoutSec 600
```

---

**Ready to test?** Copy and run the PowerShell script above! 🚀

