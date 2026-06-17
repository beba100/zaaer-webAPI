# 📅 MonsterASP Scheduled Task Setup Guide

## 🎯 Purpose
This guide explains how to configure MonsterASP scheduled task to automatically send journal entries to VoM daily.

---

## 📝 Step 1: Add API Key to appsettings.json

Add this configuration to your `appsettings.json` file:

```json
{
  "Jobs": {
    "VoMAutoSend": {
      "ApiKey": "YOUR-SECURE-API-KEY-HERE-CHANGE-THIS",
      "Enabled": true,
      "MaxRetries": 3,
      "BatchSize": 100
    }
  }
}
```

**⚠️ IMPORTANT**: Change `YOUR-SECURE-API-KEY-HERE-CHANGE-THIS` to a strong random key!

### Generate a secure API key using PowerShell:
```powershell
[Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([System.Guid]::NewGuid().ToString() + [System.Guid]::NewGuid().ToString()))
```

Or use this example:
```
VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f
```

---

## 🖥️ Step 2: Configure MonsterASP Scheduled Task

### Navigate to MonsterASP Control Panel:
1. Login to: https://admin.monsterasp.net/
2. Go to: **Website** → **Scheduled Tasks**
3. Click: **"Add New Task"**

### Task Configuration:

| Field | Value |
|-------|-------|
| **Name (*)** | `VoMAutoSendJob` |
| **Description** | `Automatically sends pending invoices, credit notes, and payment receipts to VoM system` |
| **Plan [scheduler]** | `Every 30 minutes` (or `Daily at midnight`) |
| **Domain** | Select: `aleairy.tryasp.net` (or your domain) from dropdown |
| **Url Address** | `/api/jobs/VoMAutoSendJob/vom-auto-send` |

### ⚠️ IMPORTANT: API Key Authentication

**MonsterASP does NOT show a "Custom Headers" section in the UI shown in your image.**

There are 3 options to pass the API Key:

#### **Option 1: Query Parameter (RECOMMENDED for MonsterASP)**
Add the API key as a query parameter in the URL:

**Url Address:**
```
/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f
```

Then modify the controller to accept it from query:

```csharp
[HttpPost("vom-auto-send")]
public async Task<IActionResult> ExecuteAutoSendJob(
    [FromHeader(Name = "X-API-Key")] string? apiKeyHeader,
    [FromQuery] string? apiKey,  // Added: accept from query
    [FromQuery] int maxRetries = 3,
    [FromQuery] int batchSize = 50)
{
    // Use either header or query parameter
    var providedKey = apiKeyHeader ?? apiKey;
    
    var configuredApiKey = _configuration["Jobs:VoMAutoSend:ApiKey"];
    if (providedKey != configuredApiKey)
    {
        return Unauthorized(new { error = "Invalid API Key" });
    }
    ...
}
```

#### **Option 2: No Authentication (NOT RECOMMENDED)**
Comment out API key check in controller (only if your hosting is secure).

#### **Option 3: Contact MonsterASP Support**
Ask MonsterASP support if they support custom headers for scheduled tasks.

---

## 🔄 Step 3: Test the Job Manually

Before scheduling, test it manually using PowerShell or Postman:

### Using PowerShell:
```powershell
$headers = @{
    "X-API-Key" = "YOUR-SECURE-API-KEY-HERE-CHANGE-THIS"
}

Invoke-RestMethod -Uri "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send" `
    -Method POST `
    -Headers $headers `
    -ContentType "application/json"
```

### Using curl:
```bash
curl -X POST "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send" \
     -H "X-API-Key: YOUR-SECURE-API-KEY-HERE-CHANGE-THIS" \
     -H "Content-Type: application/json"
```

### Using Postman:
1. **Method**: POST
2. **URL**: `http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send`
3. **Headers**:
   - `X-API-Key`: `YOUR-SECURE-API-KEY-HERE-CHANGE-THIS`
   - `Content-Type`: `application/json`

---

## ✅ Expected Response

### Success Response:
```json
{
  "success": true,
  "message": "VoM Auto Send Job completed successfully",
  "startTime": "2025-12-20T00:00:00",
  "endTime": "2025-12-20T00:05:23",
  "duration": "00:05:23",
  "tenants": {
    "processed": 5,
    "errors": 0
  },
  "invoices": {
    "total": 45,
    "sent": 42,
    "failed": 3
  },
  "creditNotes": {
    "total": 12,
    "sent": 12,
    "failed": 0
  },
  "paymentReceipts": {
    "total": 78,
    "sent": 75,
    "failed": 3
  },
  "summary": {
    "totalRecords": 135,
    "totalSent": 129,
    "totalFailed": 6,
    "successRate": 95.56
  }
}
```

### Error Response (Invalid API Key):
```json
{
  "error": "Invalid API Key"
}
```

---

## 📊 Step 4: Monitor Job Execution

### Check Logs:
1. Navigate to your application's **logs** folder
2. Look for entries like:
```
[VoM Auto Send Job] 🚀 Job Started at 2025-12-20 00:00:00
[VoM Auto Send Job] ✅ API Key validated successfully
[VoM Auto Send Job] Found 5 active tenants
[VoM Auto Send Job] ➡️ Processing Tenant: Dammam1 - الدمام 1
[VoM Auto Send Job] 📄 Processing Invoices for Dammam1...
[VoM Auto Send Job] Found 45 invoices to send for Dammam1
[VoM Auto Send Job] ✅ Invoice INV00046 sent successfully
...
[VoM Auto Send Job] ✅ Job Completed Successfully
[VoM Auto Send Job] Duration: 00:05:23
[VoM Auto Send Job] Total Invoices: 42/45 sent
[VoM Auto Send Job] Total Credit Notes: 12/12 sent
[VoM Auto Send Job] Total Payment Receipts: 75/78 sent
```

### Check VoM Logs API:
```
GET http://aleairy.tryasp.net/api/vom/VoMLogs/recent?lines=200
```

---

## 🛡️ Security Best Practices

1. **Strong API Key**: Use a long, random API key (minimum 32 characters)
2. **HTTPS Only**: Always use HTTPS in production (not HTTP)
3. **Rotate Keys**: Change API key periodically (every 3-6 months)
4. **Monitor Logs**: Check logs regularly for unauthorized access attempts
5. **Restrict Access**: Only MonsterASP scheduled task should call this endpoint

---

## 🔧 Advanced Configuration

### Change Schedule Frequency:
In MonsterASP task settings, you can choose:
- **Daily at midnight** (recommended)
- **Every 6 hours**
- **Every 12 hours**
- **Weekly** (Sunday at midnight)
- **Custom** (cron expression)

### Adjust Batch Size:
To process more/fewer records per run, add query parameter:
```
/api/jobs/VoMAutoSendJob/vom-auto-send?batchSize=200
```

### Adjust Max Retries:
To retry failed records more times:
```
/api/jobs/VoMAutoSendJob/vom-auto-send?maxRetries=5
```

### Combined Parameters:
```
/api/jobs/VoMAutoSendJob/vom-auto-send?maxRetries=5&batchSize=200
```

---

## ❓ Troubleshooting

### Issue: "Invalid API Key" Error
**Solution**: Verify that:
1. API key in `appsettings.json` matches the one in MonsterASP task header
2. Header name is exactly `X-API-Key` (case-sensitive)
3. No extra spaces in API key value

### Issue: Job Takes Too Long
**Solution**: Reduce batch size:
```
?batchSize=50
```

### Issue: Some Records Always Fail
**Solution**: Check:
1. VoM API credentials in `appsettings.json`
2. Account IDs exist in `chart_of_accounts` table
3. VoM API logs for specific error messages

### Issue: Job Doesn't Run Automatically
**Solution**: Verify:
1. MonsterASP task is **Enabled**
2. Schedule is set correctly
3. Domain and URL are correct
4. Custom header is added with API key

---

## 📧 Need Help?

If you encounter issues:
1. Check application logs in `/logs` folder
2. Test endpoint manually with Postman
3. Verify API key configuration
4. Check VoM API status

---

## ✨ Features

✅ **Automatic Sending**: Runs daily without manual intervention  
✅ **Multi-Tenant**: Processes all active tenants automatically  
✅ **All Types Supported**: Invoices, Credit Notes, Payment Receipts  
✅ **Smart Retry**: Only retries failed records (configurable max retries)  
✅ **Secure**: API Key authentication prevents unauthorized access  
✅ **Performance**: Batch processing with configurable batch size  
✅ **Detailed Logging**: Complete audit trail in logs  
✅ **Summary Report**: JSON response with statistics  

---

**Last Updated**: December 20, 2025  
**Version**: 1.0

