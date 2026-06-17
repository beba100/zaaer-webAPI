# 🔍 Troubleshooting: Missing X-Hotel-Code Header
## استكشاف الأخطاء: مشكلة X-Hotel-Code Header المفقود

---

## 📋 **المشكلة:**

عند استدعاء أي endpoint، قد تظهر رسالة الخطأ:

```json
{
  "error": "Unauthorized",
  "message": "Missing X-Hotel-Code header. Please provide a valid hotel code.",
  "hint": "Please provide 'X-Hotel-Code' header with a valid hotel code (e.g., Dammam1)",
  "path": "/api/zaaer/Invoice/123",
  "method": "GET"
}
```

---

## 🔍 **كيفية التحقق من المشكلة:**

### **1. فحص الـ Logs:**

بعد التحديث، ستجد في الـ logs معلومات مفصلة عن الطلبات التي تفتقد `X-Hotel-Code` header:

```
[WRN] [SECURITY] Missing X-Hotel-Code header | Path: /api/zaaer/Invoice/123 | Method: GET | UserAgent: Mozilla/5.0... | RemoteIP: 192.168.1.100
```

**المعلومات المتوفرة:**
- ✅ **Path**: المسار المحدد الذي يسبب المشكلة
- ✅ **Method**: HTTP Method (GET, POST, PUT, DELETE)
- ✅ **UserAgent**: المتصفح أو الأداة المستخدمة
- ✅ **RemoteIP**: عنوان IP للمستخدم

---

### **2. البحث في الـ Logs:**

#### **في Windows (PowerShell):**
```powershell
# البحث عن جميع الطلبات المفقودة X-Hotel-Code
Select-String -Path "logs\security*.log" -Pattern "Missing X-Hotel-Code header"

# البحث عن path محدد
Select-String -Path "logs\security*.log" -Pattern "/api/zaaer/Invoice"
```

#### **في Linux/Mac:**
```bash
# البحث عن جميع الطلبات المفقودة X-Hotel-Code
grep "Missing X-Hotel-Code header" logs/security*.log

# البحث عن path محدد
grep "/api/zaaer/Invoice" logs/security*.log
```

---

## 🛠️ **الحلول:**

### **الحل 1: إضافة X-Hotel-Code Header**

إذا كان الطلب من **Frontend (JavaScript)**:

```javascript
// ✅ الصحيح
const response = await fetch(`${API_BASE_URL}/api/zaaer/Invoice/123`, {
    headers: {
        'X-Hotel-Code': getCurrentHotelCode(),  // أو 'Dammam1'
        'Content-Type': 'application/json'
    }
});
```

إذا كان الطلب من **Postman/API Client**:

```
Headers:
X-Hotel-Code: Dammam1
Content-Type: application/json
```

---

### **الحل 2: إضافة Path إلى القائمة البيضاء**

إذا كان الـ endpoint **لا يحتاج** إلى `X-Hotel-Code` (مثل health checks أو monitoring):

**ملف:** `Middleware/TenantMiddleware.cs`

```csharp
// إضافة path جديد إلى القائمة البيضاء
var isWhitelisted = path.Contains("/swagger") || 
    path.Contains("/health") || 
    path.Contains("/api/your-new-endpoint") ||  // ← أضف هنا
    // ... باقي القائمة
```

---

### **الحل 3: إصلاح طلبات Zaaer System**

إذا كانت الطلبات تأتي من **نظام Zaaer** بدون header:

**الخيار 1:** تحديث نظام Zaaer لإرسال `X-Hotel-Code` header

**الخيار 2:** إضافة endpoint خاص لـ Zaaer بدون الحاجة إلى header (غير موصى به لأسباب أمنية)

---

## 📊 **Endpoints التي لا تحتاج X-Hotel-Code:**

القائمة الحالية للـ paths المعفاة:

- ✅ `/swagger` - Swagger UI
- ✅ `/health` - Health checks
- ✅ `/api/tenant` - Tenant management
- ✅ `/api/vom/*` - VoM endpoints
- ✅ `/api/settings/*` - VoM settings
- ✅ `/api/jobs/*` - Automated jobs
- ✅ `/api/partner-requests/all-hotels` - Partner requests (all hotels)
- ✅ `/api/config/devextreme-license` - DevExtreme license
- ✅ `/favicon.ico` - Favicon
- ✅ `/robots.txt` - Robots.txt
- ✅ Static files (`.html`, `.css`, `.js`, `.png`, etc.)
- ✅ File upload paths (`/uploads`, `/files`, etc.)

---

## 🎯 **خطوات التحقق السريعة:**

1. ✅ **افتح الـ logs** (`logs/security-YYYY-MM-DD.log`)
2. ✅ **ابحث عن** `"Missing X-Hotel-Code header"`
3. ✅ **حدد الـ Path** المسبب للمشكلة
4. ✅ **حدد المصدر** (UserAgent, RemoteIP)
5. ✅ **طبق الحل المناسب** (إضافة header أو إضافة path للقائمة البيضاء)

---

## 📝 **مثال على Log Entry:**

```
2026-01-01 01:36:38.841 [WRN] [SECURITY] Missing X-Hotel-Code header | Path: /api/zaaer/Invoice/123 | Method: GET | UserAgent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 | RemoteIP: 192.168.1.100
```

**التفسير:**
- **Path**: `/api/zaaer/Invoice/123` - هذا هو الـ endpoint المسبب للمشكلة
- **Method**: `GET` - نوع الطلب
- **UserAgent**: `Mozilla/5.0...` - المتصفح المستخدم
- **RemoteIP**: `192.168.1.100` - عنوان IP للمستخدم

---

## ⚠️ **ملاحظات أمنية:**

- ❌ **لا تضيف** endpoints حساسة (مثل `/api/zaaer/*`) إلى القائمة البيضاء
- ✅ **استخدم** `X-Hotel-Code` header دائماً للـ endpoints التي تحتاج tenant isolation
- ✅ **تحقق** من صحة الـ hotel code في Master Database

---

## 🔗 **روابط مفيدة:**

- [ALL_ENDPOINTS_FIXED.md](./ALL_ENDPOINTS_FIXED.md) - قائمة بجميع الـ endpoints التي تم إصلاحها
- [FIXING_ALL_ENDPOINTS.md](./FIXING_ALL_ENDPOINTS.md) - دليل إصلاح الـ endpoints

