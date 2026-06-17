# 🎯 MonsterASP Scheduled Task - Updated Configuration

## ⚠️ IMPORTANT: HTTP Method Issue Fixed

**Problem:** MonsterASP was sending GET requests, but the endpoint only accepted POST, causing "Method Not Allowed" (405) error.

**Solution:** The endpoint now accepts **BOTH GET and POST** methods, so MonsterASP scheduled tasks will work correctly.

---

## ✅ **Configuration: Use Query Parameter for API Key**

MonsterASP does not show a "Custom Headers" field, so pass the API key as a **query parameter** in the URL.

### Step 1: Configure MonsterASP Task

| Field | Value |
|-------|-------|
| **Name (*)** | `VoMAutoSendJob` |
| **Description** | `Automatically sends pending invoices, credit notes, and payment receipts to VoM system` |
| **Plan [scheduler]** | `Every 30 minutes` (recommended) or `Daily at midnight` |
| **Domain** | Select `aleairy.tryasp.net` from dropdown |
| **Url Address** | `/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f&batchSize=50&maxRetries=3` |
| **HTTP Method** | GET (default, will work) or POST (also works) |

### ⚠️ Important Notes:
- The `apiKey` parameter is added to the URL
- Use the SAME API key as in `appsettings.json`
- The controller now accepts **both GET and POST** methods (fixed 405 error)
- Added `batchSize=50` and `maxRetries=3` for better control

---

## 🧪 Test the Configuration

### Using PowerShell (GET - MonsterASP Default):
```powershell
$apiKey = "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f"
$url = "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=$apiKey&batchSize=1&maxRetries=3"

# GET method (MonsterASP default)
Invoke-RestMethod -Uri $url -Method GET

# POST method (also works)
Invoke-RestMethod -Uri $url -Method POST
```

### Using curl:
```bash
# GET method (MonsterASP default)
curl -X GET "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f&batchSize=1&maxRetries=3"

# POST method (also works)
curl -X POST "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f&batchSize=1&maxRetries=3"
```

### Using Postman:

#### Method 1: Query Parameter (Recommended for MonsterASP)

1. **Create New Request**
   - Click **"New"** → **"HTTP Request"**
   - Name: `VoM Auto Send Job (Query Parameter)`

2. **Configure Request**
   - **Method**: `POST`
   - **URL**: `http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send`
   
3. **Add Query Parameter**
   - Click on **"Params"** tab
   - Add parameter:
     - **KEY**: `apiKey`
     - **VALUE**: `VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f`
   - The URL will automatically update to:
     ```
     http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f
     ```

4. **⚠️ IMPORTANT: Do NOT add X-Hotel-Code header**
   - This job processes ALL tenants/hotels automatically
   - The `X-Hotel-Code` header is NOT needed and will cause issues
   - Leave the **"Headers"** tab empty (or only add Content-Type if needed)

5. **Optional: Add More Parameters**
   - Click **"Params"** tab
   - Add additional parameters if needed:
     - **KEY**: `batchSize` | **VALUE**: `5` (process 5 records per hotel)
     - **KEY**: `maxRetries` | **VALUE**: `3` (retry failed records up to 3 times)

6. **Send Request**
   - Click **"Send"** button
   - Wait for response (may take 5-10 minutes for large datasets)

7. **View Response**
   - Check **"Body"** tab for JSON response
   - Status should be `200 OK`

---

#### Method 2: Header (Alternative Method)

1. **Create New Request**
   - Click **"New"** → **"HTTP Request"**
   - Name: `VoM Auto Send Job (Header)`

2. **Configure Request**
   - **Method**: `POST`
   - **URL**: `http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send`

3. **Add Header**
   - Click on **"Headers"** tab
   - Add header:
     - **KEY**: `X-API-Key`
     - **VALUE**: `VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f`
   - **⚠️ Do NOT add X-Hotel-Code header** (job processes all hotels)

4. **Optional: Add Query Parameters**
   - Click **"Params"** tab
   - Add:
     - **KEY**: `batchSize` | **VALUE**: `5`
     - **KEY**: `maxRetries` | **VALUE**: `3`

5. **Send Request**
   - Click **"Send"** button

---

#### Postman Collection (Import This)

Save this as `VoM-Auto-Send.postman_collection.json`:

```json
{
  "info": {
    "name": "VoM Auto Send Job",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Auto Send (Query Parameter)",
      "request": {
        "method": "POST",
        "header": [],
        "url": {
          "raw": "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f",
          "protocol": "http",
          "host": ["aleairy", "tryasp", "net"],
          "path": ["api", "jobs", "VoMAutoSendJob", "vom-auto-send"],
          "query": [
            {
              "key": "apiKey",
              "value": "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f",
              "description": "API Key for authentication"
            }
          ]
        }
      }
    },
    {
      "name": "Auto Send (Header)",
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
          "raw": "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send",
          "protocol": "http",
          "host": ["aleairy", "tryasp", "net"],
          "path": ["api", "jobs", "VoMAutoSendJob", "vom-auto-send"]
        }
      }
    },
    {
      "name": "Auto Send (Custom Batch Size)",
      "request": {
        "method": "POST",
        "header": [],
        "url": {
          "raw": "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f&batchSize=50&maxRetries=5",
          "protocol": "http",
          "host": ["aleairy", "tryasp", "net"],
          "path": ["api", "jobs", "VoMAutoSendJob", "vom-auto-send"],
          "query": [
            {
              "key": "apiKey",
              "value": "VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f"
            },
            {
              "key": "batchSize",
              "value": "50",
              "description": "Process 50 records per hotel"
            },
            {
              "key": "maxRetries",
              "value": "5",
              "description": "Retry failed records up to 5 times"
            }
          ]
        }
      }
    },
    {
      "name": "Auto Send (Test Invalid API Key)",
      "request": {
        "method": "POST",
        "header": [],
        "url": {
          "raw": "http://aleairy.tryasp.net/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey=invalid-key-12345",
          "protocol": "http",
          "host": ["aleairy", "tryasp", "net"],
          "path": ["api", "jobs", "VoMAutoSendJob", "vom-auto-send"],
          "query": [
            {
              "key": "apiKey",
              "value": "invalid-key-12345",
              "description": "Should return 401 Unauthorized"
            }
          ]
        }
      }
    }
  ]
}
```

**To Import:**
1. Open Postman
2. Click **"Import"** button (top left)
3. Select **"Raw text"** tab
4. Paste the JSON above
5. Click **"Import"**
6. You'll now have 4 pre-configured requests ready to use!

---

#### Postman Environment Variables (Optional)

Create an environment for easy API key management:

1. Click **"Environments"** (left sidebar)
2. Click **"+"** to create new environment
3. Name: `VoM Production`
4. Add variables:
   - **VARIABLE**: `apiKey` | **VALUE**: `VoM-2024-a8f3e9d2-4b7c-4d1e-9f2a-3c5b8e1d4a7f`
   - **VARIABLE**: `baseUrl` | **VALUE**: `http://aleairy.tryasp.net`
   - **VARIABLE**: `batchSize` | **VALUE**: `50`
   - **VARIABLE**: `maxRetries` | **VALUE**: `3`
5. Click **"Save"**
6. Select the environment from dropdown (top right)

Then in your requests, use:
```
{{baseUrl}}/api/jobs/VoMAutoSendJob/vom-auto-send?apiKey={{apiKey}}&batchSize={{batchSize}}
```

---

#### Postman Testing Scripts

Add this to **"Tests"** tab in Postman to automatically verify responses:

```javascript
// Test 1: Check status code
pm.test("Status code is 200", function () {
    pm.response.to.have.status(200);
});

// Test 2: Check response has success field
pm.test("Response has success field", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData).to.have.property('success');
});

// Test 3: Check job completed successfully
pm.test("Job completed successfully", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.success).to.eql(true);
});

// Test 4: Check summary exists
pm.test("Summary statistics exist", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData).to.have.property('summary');
    pm.expect(jsonData.summary).to.have.property('totalRecords');
    pm.expect(jsonData.summary).to.have.property('totalSent');
    pm.expect(jsonData.summary).to.have.property('successRate');
});

// Test 5: Log statistics
console.log("📊 Job Statistics:");
console.log("- Duration:", pm.response.json().duration);
console.log("- Total Records:", pm.response.json().summary.totalRecords);
console.log("- Total Sent:", pm.response.json().summary.totalSent);
console.log("- Success Rate:", pm.response.json().summary.successRate + "%");
```

---

#### Expected Postman Response

**Success (200 OK):**
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

**Invalid API Key (401 Unauthorized):**
```json
{
  "error": "Invalid API Key"
}
```

**Server Error (500):**
```json
{
  "success": false,
  "error": "Job execution failed",
  "details": "[Error message]"
}
```

---

## 🎯 Schedule Recommendations

### For Development/Testing:
- **Every 30 minutes** - Good for testing, catches issues quickly

### For Production:
- **Daily at midnight** (00:00) - Standard batch processing time
- **Every 6 hours** - More frequent updates
- **Every hour** - If you need near real-time sync

---

## ✅ Expected Response

```json
{
  "success": true,
  "message": "VoM Auto Send Job completed successfully",
  "duration": "00:05:23",
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
    "successRate": 95.56
  }
}
```

---

## 🛡️ Security Note

**API Key in URL:**
- ✅ Works with MonsterASP scheduled tasks
- ⚠️ API key visible in logs (MonsterASP internal logs)
- ✅ Still secure because:
  - MonsterASP is your hosting provider (trusted)
  - Only MonsterASP can trigger the task
  - API key prevents external access
  - HTTPS encrypts the URL in transit (use HTTPS in production!)

**Better Security (if MonsterASP adds header support):**
- Use `X-API-Key` header instead of query parameter
- Keeps API key out of URL
- Contact MonsterASP support to request this feature

---

## 📊 Monitoring

After the task runs, check:

1. **MonsterASP Task Logs** - Shows task execution history
2. **Application Logs** - `C:\zaaerIntegration\logs\log-*.txt`
3. **Database** - Check `credit_note_journal_entries`, `invoice_journal_entries`, `payment_receipt_journal_entries` for new "Sent" records

---

## ❓ Troubleshooting

### Issue: "Invalid API Key"
**Solution:** 
1. Verify API key in `appsettings.json` matches URL parameter
2. No extra spaces or special characters
3. Case-sensitive match required

### Issue: Task doesn't run
**Solution:**
1. Verify task is **enabled** in MonsterASP
2. Check schedule is correctly set
3. Verify domain is correct
4. Test URL manually with PowerShell first

### Issue: 500 Error
**Solution:**
1. Check application logs for specific error
2. Verify all SQL migrations are run
3. Check VoM API credentials in appsettings.json

---

**Last Updated**: December 20, 2025  
**Version**: 2.0 (Updated for MonsterASP UI)

