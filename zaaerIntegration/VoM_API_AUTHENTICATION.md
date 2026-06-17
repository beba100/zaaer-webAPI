# 🔐 VoM API Authentication Guide
## دليل مصادقة VoM API

---

## 📋 **Overview | نظرة عامة**

جميع endpoints في VoM API تستخدم نفس آلية المصادقة. هذا الدليل يشرح كيفية عمل المصادقة مع جميع endpoints.

---

## 🔑 **Authentication Method | طريقة المصادقة**

VoM API يستخدم **Bearer Token Authentication** لجميع endpoints المحمية.

### **الخطوات:**

1. ✅ **تسجيل الدخول** → الحصول على Token
2. ✅ **استخدام Token** في جميع الطلبات التالية
3. ✅ **تحديث Token** عند انتهاء صلاحيته

---

## 📝 **Required Headers | الـ Headers المطلوبة**

جميع endpoints في VoM API تحتاج نفس الـ Headers:

### **1. Content-Type**
```
Content-Type: application/json
```
- **مطلوب:** نعم
- **النوع:** String
- **القيمة الافتراضية:** `application/json`
- **الوصف:** نوع محتوى الطلب

---

### **2. Accept**
```
Accept: application/json
```
- **مطلوب:** نعم
- **النوع:** String
- **القيمة الافتراضية:** `application/json`
- **الوصف:** الصيغة المقبولة للاستجابة

---

### **3. Api-Agent**
```
Api-Agent: zapier
```
- **مطلوب:** نعم
- **النوع:** String
- **القيم المسموحة:** 
  - `"android"`
  - `"ios"`
  - `"zapier"`
- **الوصف:** الوكيل الذي أرسل الطلب
- **ملاحظة:** استخدم `"zapier"` للـ API integrations

---

### **4. Accept-Language**
```
Accept-Language: en
```
- **مطلوب:** نعم
- **النوع:** String
- **القيم المسموحة:** 
  - `"ar"` (العربية)
  - `"en"` (الإنجليزية)
- **الوصف:** اللغة المقبولة للاستجابة

---

### **5. Authorization**
```
Authorization: Bearer YOUR_TOKEN_HERE
```
- **مطلوب:** نعم (لجميع endpoints المحمية)
- **النوع:** String
- **القيمة الافتراضية:** `Bearer`
- **الوصف:** Bearer Token للمصادقة
- **ملاحظة:** استبدل `YOUR_TOKEN_HERE` بالـ Token من Login

---

## 🔄 **Authentication Flow | تدفق المصادقة**

### **الخطوة 1: Login (تسجيل الدخول)**

```http
POST https://kimoo.getvom.com/api/companyuser/login
Content-Type: application/json
Accept: application/json
Api-Agent: zapier
Accept-Language: en

{
  "email": "Info@aleairygroup.com",
  "password": "Aa1xyjPlNHd"
}
```

**الاستجابة:**
```json
{
  "status": 200,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": {...}
  },
  "success": true
}
```

---

### **الخطوة 2: استخدام Token في جميع الطلبات**

```http
GET https://kimoo.getvom.com/api/accounting/accounts
Content-Type: application/json
Accept: application/json
Api-Agent: zapier
Accept-Language: en
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## ✅ **جميع Endpoints تستخدم نفس الـ Headers**

### **أمثلة:**

#### **1. Accounts List**
```http
GET /api/accounting/accounts
Authorization: Bearer {token}
Accept-Language: en
```

#### **2. Create Account (مثال)**
```http
POST /api/accounting/accounts
Authorization: Bearer {token}
Accept-Language: en
Content-Type: application/json
```

#### **3. Update Account (مثال)**
```http
PUT /api/accounting/accounts/{id}
Authorization: Bearer {token}
Accept-Language: en
Content-Type: application/json
```

#### **4. Delete Account (مثال)**
```http
DELETE /api/accounting/accounts/{id}
Authorization: Bearer {token}
Accept-Language: en
```

---

## 🔧 **Implementation in Our Code | التطبيق في الكود**

### **الخدمة الحالية:**

```csharp
// VoMAccountService.cs
var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
request.Headers.Add("Accept-Language", language ?? "en");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
```

### **جميع Endpoints الأخرى:**

نفس الطريقة! فقط استخدم:
- ✅ نفس الـ Headers
- ✅ نفس الـ Token
- ✅ نفس الـ Base URL

---

## 📋 **Summary | الملخص**

| Header | Required | Values | Description |
|--------|----------|--------|-------------|
| `Content-Type` | ✅ Yes | `application/json` | Request content type |
| `Accept` | ✅ Yes | `application/json` | Response format |
| `Api-Agent` | ✅ Yes | `zapier`, `android`, `ios` | API agent identifier |
| `Accept-Language` | ✅ Yes | `ar`, `en` | Response language |
| `Authorization` | ✅ Yes | `Bearer {token}` | Authentication token |

---

## 🎯 **Important Notes | ملاحظات مهمة**

1. ✅ **جميع endpoints تستخدم نفس الـ Headers**
2. ✅ **Token من Login يعمل مع جميع endpoints**
3. ✅ **Token ينتهي بعد فترة** - يحتاج تحديث
4. ✅ **Auto-refresh** - النظام يقوم بتحديث Token تلقائياً

---

## 🔄 **Token Management | إدارة Token**

### **في الكود الحالي:**

```csharp
// VoMAccountService يستخدم VoMAuthService
var token = await _authService.GetTokenAsync();
```

**الأولوية:**
1. Token في الذاكرة (إذا كان صالحاً)
2. Token في appsettings.json
3. Auto-Login (إذا لم يوجد Token)

---

## 📝 **Example: Adding New VoM Endpoint**

عند إضافة endpoint جديد من VoM:

```csharp
public async Task<SomeResponseDto> GetSomeDataAsync(string? language = "en")
{
    var token = await _authService.GetTokenAsync();
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/some/endpoint");
    request.Headers.Add("Accept-Language", language ?? "en");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    
    var response = await ExecuteWithRetryAsync(request);
    // ... rest of the code
}
```

---

**Last Updated:** 2025-01-XX
