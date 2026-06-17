# VoM Integration Guide
## دليل تكامل VoM

---

## 📋 **Overview | نظرة عامة**

This guide explains how to set up and use the VoM (Value of Money) partner integration in your application.

---

## 🔑 **1. BearerToken Configuration | إعداد BearerToken**

### **Option 1: Manual Token (Recommended for Production)**
**VoM will provide you with a Bearer Token** after you register/login to their system. Once you have it:

1. Open `appsettings.json`
2. Add your token:
```json
"VoM": {
  "BaseUrl": "https://kimoo.getvom.com",
  "ApiAgent": "tenant",
  "BearerToken": "your-token-here-from-vom"
}
```

### **Option 2: Auto-Login (For Development)**
If VoM provides login credentials, you can configure auto-login:

```json
"VoM": {
  "BaseUrl": "https://kimoo.getvom.com",
  "ApiAgent": "tenant",
  "Email": "your-email@example.com",
  "Password": "your-password"
}
```

The system will automatically login and get a token when needed.

### **Option 3: Use Login Endpoint**
You can also login programmatically using the login endpoint:

```http
POST http://voom.tryasp.net/api/vom/VoMAuth/login
Content-Type: application/json

{
  "email": "your-email@example.com",
  "password": "your-password"
}
```

This will return a token that you can then add to `appsettings.json`.

---

## 🚀 **2. How to Test | كيفية الاختبار**

### **Your Host:** `http://voom.tryasp.net/`

### **Step 1: Deploy Your Application**
1. Build and publish your application
2. Deploy to `http://voom.tryasp.net/`
3. Make sure `appsettings.json` is configured with VoM settings

### **Step 2: Test the Accounts Endpoint**

#### **Using Browser:**
```
http://voom.tryasp.net/api/vom/VoMAccount?language=en
```

#### **Using cURL:**
```bash
curl -X GET "http://voom.tryasp.net/api/vom/VoMAccount?language=en" \
  -H "Content-Type: application/json"
```

#### **Using Postman/HTTP Client:**
```http
GET http://voom.tryasp.net/api/vom/VoMAccount?language=en
Content-Type: application/json
```

### **Step 3: Expected Response**
```json
{
  "status": 200,
  "data": {
    "accounts": [
      {
        "id": 7,
        "name_ar": "الاراضي",
        "name_en": "Lands",
        "code": "011-1",
        ...
      }
    ],
    "accounting_sub_categories": {...},
    "parent_accounts": {...}
  },
  "success": true
}
```

---

## 📍 **3. Available Endpoints | الـ Endpoints المتاحة**

### **Accounts Endpoint**
- **URL:** `/api/vom/VoMAccount`
- **Method:** `GET`
- **Query Parameters:**
  - `language` (optional): `"en"` or `"ar"` (default: `"en"`)

### **Authentication Endpoints**

#### **Login**
- **URL:** `/api/vom/VoMAuth/login`
- **Method:** `POST`
- **Body:**
```json
{
  "email": "your-email@example.com",
  "password": "your-password"
}
```

#### **Refresh Token**
- **URL:** `/api/vom/VoMAuth/refresh`
- **Method:** `POST`

---

## ⚙️ **4. Configuration | الإعدادات**

### **appsettings.json Structure:**
```json
{
  "VoM": {
    "BaseUrl": "https://kimoo.getvom.com",
    "ApiAgent": "tenant",
    "BearerToken": "",           // Option 1: Direct token
    "Email": "",                 // Option 2: Auto-login email
    "Password": ""               // Option 2: Auto-login password
  }
}
```

### **Configuration Options:**

| Setting | Description | Required |
|---------|-------------|----------|
| `BaseUrl` | VoM API base URL | Yes |
| `ApiAgent` | API agent identifier (`"tenant"`, `"android"`, `"ios"`, `"zapier"`) | Yes |
| `BearerToken` | Direct Bearer token (if provided by VoM) | No* |
| `Email` | Login email (for auto-login) | No* |
| `Password` | Login password (for auto-login) | No* |

\* Either `BearerToken` OR (`Email` + `Password`) must be provided.

---

## 🔒 **5. Security Considerations | اعتبارات الأمان**

1. **Never commit tokens to version control**
   - Use environment variables or secure configuration
   - Add `appsettings.json` to `.gitignore` if it contains secrets

2. **Token Storage**
   - Tokens are cached in memory during runtime
   - Tokens expire based on VoM API settings
   - System auto-refreshes tokens when expired (if credentials configured)

3. **HTTPS**
   - Your host (`http://voom.tryasp.net/`) should use HTTPS in production
   - VoM API uses HTTPS (`https://kimoo.getvom.com`)

---

## 🐛 **6. Troubleshooting | استكشاف الأخطاء**

### **Error: Unauthorized (401)**
- **Cause:** Missing or invalid Bearer Token
- **Solution:** 
  1. Check `appsettings.json` has `BearerToken` configured
  2. Or configure `Email` and `Password` for auto-login
  3. Or use `/api/vom/VoMAuth/login` endpoint to get a token

### **Error: Failed to communicate with VoM API (502)**
- **Cause:** Network issue or VoM API is down
- **Solution:** 
  1. Check internet connectivity
  2. Verify VoM API is accessible: `https://kimoo.getvom.com`
  3. Check firewall settings

### **Error: JSON deserialization error**
- **Cause:** VoM API response format changed
- **Solution:** Check VoM API documentation for latest response format

---

## 📝 **7. Next Steps | الخطوات التالية**

1. ✅ **Get Bearer Token from VoM**
   - Contact VoM support or use their login portal
   - Add token to `appsettings.json`

2. ✅ **Deploy Application**
   - Build and deploy to `http://voom.tryasp.net/`
   - Verify configuration

3. ✅ **Test Endpoint**
   - Use browser, Postman, or cURL
   - Verify response structure

4. ✅ **Add More VoM Endpoints** (if needed)
   - Follow the same pattern as `VoMAccountService`
   - Create new services in `Services/VoM/`
   - Create new controllers in `Controllers/VoM/`

---

## 📞 **Support | الدعم**

If you encounter issues:
1. Check application logs (`logs/log-.txt`)
2. Verify VoM API is accessible
3. Contact VoM support for API documentation and token access

---

**Last Updated:** 2025-01-XX
**Version:** 1.0.0
