# 🚀 VoM API - Quick Start Guide
## دليل البدء السريع لـ VoM API

---

## ✅ **الخطوة 1: تشغيل التطبيق**

### **Option A: باستخدام Visual Studio**
1. افتح المشروع في Visual Studio
2. اضغط `F5` أو `Ctrl+F5` لتشغيل التطبيق
3. سيتم فتح المتصفح تلقائياً على `https://localhost:7062` أو `http://localhost:5000`

### **Option B: باستخدام Command Line**
```bash
cd "c:\‏‏zaaerIntegration\zaaerIntegration"
dotnet run
```

### **Option C: نشر على السيرفر**
```bash
dotnet publish -c Release
# ثم انشر الملفات إلى http://voom.tryasp.net/
```

---

## 🧪 **الخطوة 2: اختبار Login (الحصول على Token)**

### **الطريقة 1: استخدام Postman / HTTP Client**

```http
POST http://localhost:7062/api/vom/VoMAuth/login
Content-Type: application/json

{
  "email": "Info@aleairygroup.com",
  "password": "Aa1xyjPlNHd"
}
```

**الاستجابة المتوقعة:**
```json
{
  "token": "abcd1234...",
  "message": "Login successful. Use this token in VoM:BearerToken configuration."
}
```

### **الطريقة 2: استخدام cURL**
```bash
curl -X POST "http://localhost:7062/api/vom/VoMAuth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"Info@aleairygroup.com\",\"password\":\"Aa1xyjPlNHd\"}"
```

### **الطريقة 3: استخدام المتصفح (Swagger)**
1. افتح: `https://localhost:7062/swagger`
2. ابحث عن `POST /api/vom/VoMAuth/login`
3. اضغط `Try it out`
4. أدخل البيانات:
   ```json
   {
     "email": "Info@aleairygroup.com",
     "password": "Aa1xyjPlNHd"
   }
   ```
5. اضغط `Execute`

---

## 📊 **الخطوة 3: جلب الحسابات (Accounts)**

### **الطريقة 1: استخدام Postman / HTTP Client**

```http
GET http://localhost:7062/api/vom/VoMAccount?language=en
Content-Type: application/json
```

**أو بالعربية:**
```http
GET http://localhost:7062/api/vom/VoMAccount?language=ar
Content-Type: application/json
```

### **الطريقة 2: استخدام cURL**
```bash
curl -X GET "http://localhost:7062/api/vom/VoMAccount?language=en" \
  -H "Content-Type: application/json"
```

### **الطريقة 3: استخدام المتصفح**
افتح مباشرة:
```
http://localhost:7062/api/vom/VoMAccount?language=en
```

### **الطريقة 4: استخدام Swagger**
1. افتح: `https://localhost:7062/swagger`
2. ابحث عن `GET /api/vom/VoMAccount`
3. اضغط `Try it out`
4. أدخل `language` = `en` أو `ar`
5. اضغط `Execute`

---

## 🎯 **الاستجابة المتوقعة:**

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
        "description": null,
        "used_in_payment": null,
        "default_transaction_type": "debit",
        "subcategory_id": 4,
        "status": 1,
        "is_main": 1,
        "currency_code": "SAR",
        "current_total_debit": "0.00",
        "current_total_credit": "0.00",
        "current_balance": "0.00",
        "accounting_sub_category": {
          "id": 4,
          "name_ar": "الاصول الثابتة",
          "name_en": "Fixed Assets",
          "code": "011"
        }
      }
    ],
    "accounting_sub_categories": {
      "Assets": {
        "1": "Cash and Cash Equivalents",
        "2": "Current Assets",
        "3": "Non Current Assets",
        "4": "Fixed Assets"
      }
    },
    "parent_accounts": {
      "1": "Cash",
      "2": "Bank Accounts"
    }
  },
  "errors": null,
  "success": true
}
```

---

## 🔄 **كيف يعمل النظام:**

1. ✅ **عند أول طلب** إلى `/api/vom/VoMAccount`:
   - النظام يتحقق من وجود Token في الذاكرة
   - إذا لم يوجد، يقوم بتسجيل الدخول تلقائياً باستخدام:
     - Email: `Info@aleairygroup.com`
     - Password: `Aa1xyjPlNHd`
   - يحصل على Token من VoM API
   - يحفظ Token في الذاكرة
   - يستخدم Token في الطلب

2. ✅ **في الطلبات التالية**:
   - يستخدم Token المحفوظ في الذاكرة
   - إذا انتهت صلاحية Token، يقوم بتحديثه تلقائياً

---

## 🌐 **للنشر على السيرفر (http://voom.tryasp.net/)**

### **بعد النشر:**

```http
GET http://voom.tryasp.net/api/vom/VoMAccount?language=en
```

أو:

```http
POST http://voom.tryasp.net/api/vom/VoMAuth/login
Content-Type: application/json

{
  "email": "Info@aleairygroup.com",
  "password": "Aa1xyjPlNHd"
}
```

---

## 🐛 **استكشاف الأخطاء:**

### **خطأ: Unauthorized (401)**
- ✅ تأكد من أن بيانات الاعتماد صحيحة في `appsettings.json`
- ✅ تحقق من أن VoM API متاح: `https://kimoo.getvom.com`

### **خطأ: Failed to communicate with VoM API (502)**
- ✅ تحقق من الاتصال بالإنترنت
- ✅ تأكد من أن السيرفر يمكنه الوصول إلى `https://kimoo.getvom.com`

### **خطأ: Login failed: No token received**
- ✅ تحقق من صحة Email و Password
- ✅ راجع Logs في `logs/log-.txt`

---

## 📝 **ملاحظات مهمة:**

1. ✅ **النظام يعمل تلقائياً** - لا حاجة لتسجيل الدخول يدوياً
2. ✅ **Token يتم تحديثه تلقائياً** عند انتهاء صلاحيته
3. ✅ **جميع الطلبات تحتوي على Headers المطلوبة**:
   - `Content-Type: application/json`
   - `Accept: application/json`
   - `Api-Agent: zapier`
   - `Accept-Language: en` أو `ar`
   - `Authorization: Bearer {token}`

---

## 🎉 **جاهز للاستخدام!**

ابدأ الآن باختبار:
```
GET http://localhost:7062/api/vom/VoMAccount?language=en
```

**أو بعد النشر:**
```
GET http://voom.tryasp.net/api/vom/VoMAccount?language=en
```

---

**Last Updated:** 2025-01-XX
