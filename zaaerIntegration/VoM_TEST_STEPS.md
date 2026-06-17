# 🧪 VoM API - Live Testing Guide
## دليل اختبار VoM API مباشرة

---

## 🎯 **الهدف:**
اختبار `https://kimoo.getvom.com/api/accounting/accounts` مباشرة قبل استخدامه في التطبيق.

---

## 📋 **الخطوات:**

### **الخطوة 1: الحصول على Token**

#### **Option A: استخدام Postman / HTTP Client**

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

**الاستجابة المتوقعة:**
```json
{
  "status": 200,
  "data": {
    "token": "abcd1234...",
    "user": {...}
  },
  "success": true
}
```

**انسخ الـ Token من الاستجابة!**

---

#### **Option B: استخدام cURL**

```bash
curl -X POST "https://kimoo.getvom.com/api/companyuser/login" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -H "Api-Agent: zapier" \
  -H "Accept-Language: en" \
  -d "{\"email\":\"Info@aleairygroup.com\",\"password\":\"Aa1xyjPlNHd\"}"
```

---

### **الخطوة 2: استخدام Token لجلب الحسابات**

#### **Option A: استخدام Postman / HTTP Client**

```http
GET https://kimoo.getvom.com/api/accounting/accounts
Content-Type: application/json
Accept: application/json
Api-Agent: zapier
Accept-Language: en
Authorization: Bearer YOUR_TOKEN_HERE
```

**استبدل `YOUR_TOKEN_HERE` بالـ Token من الخطوة 1!**

---

#### **Option B: استخدام cURL**

```bash
curl -X GET "https://kimoo.getvom.com/api/accounting/accounts" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -H "Api-Agent: zapier" \
  -H "Accept-Language: en" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**استبدل `YOUR_TOKEN_HERE` بالـ Token من الخطوة 1!**

---

#### **Option C: استخدام JavaScript (في Console المتصفح)**

```javascript
// Step 1: Login
const loginResponse = await fetch('https://kimoo.getvom.com/api/companyuser/login', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
    'Api-Agent': 'zapier',
    'Accept-Language': 'en'
  },
  body: JSON.stringify({
    email: 'Info@aleairygroup.com',
    password: 'Aa1xyjPlNHd'
  })
});

const loginData = await loginResponse.json();
const token = loginData.data.token;
console.log('Token:', token);

// Step 2: Get Accounts
const accountsResponse = await fetch('https://kimoo.getvom.com/api/accounting/accounts', {
  method: 'GET',
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
    'Api-Agent': 'zapier',
    'Accept-Language': 'en',
    'Authorization': `Bearer ${token}`
  }
});

const accountsData = await accountsResponse.json();
console.log('Accounts:', accountsData);
```

---

## ✅ **الاستجابة المتوقعة:**

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

## 🔄 **بعد التأكد من عمل VoM API:**

### **الطريقة 1: استخدام Backend الخاص بنا**

بعد التأكد من عمل VoM API، استخدم endpoint الخاص بنا:

```http
GET http://localhost:7062/api/vom/VoMAccount?language=en
```

أو بعد النشر:
```http
GET http://voom.tryasp.net/api/vom/VoMAccount?language=en
```

**مميزات استخدام Backend الخاص بنا:**
- ✅ لا حاجة لنسخ Token يدوياً
- ✅ تسجيل الدخول التلقائي
- ✅ تحديث Token تلقائياً
- ✅ معالجة الأخطاء

---

### **الطريقة 2: استخدام VoM API مباشرة**

إذا كنت تريد استخدام VoM API مباشرة من Frontend:

```javascript
// في Frontend (JavaScript)
async function getVoMAccounts() {
  // 1. Login
  const loginRes = await fetch('https://kimoo.getvom.com/api/companyuser/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Api-Agent': 'zapier',
      'Accept-Language': 'en'
    },
    body: JSON.stringify({
      email: 'Info@aleairygroup.com',
      password: 'Aa1xyjPlNHd'
    })
  });
  
  const loginData = await loginRes.json();
  const token = loginData.data.token;
  
  // 2. Get Accounts
  const accountsRes = await fetch('https://kimoo.getvom.com/api/accounting/accounts', {
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'Api-Agent': 'zapier',
      'Accept-Language': 'en',
      'Authorization': `Bearer ${token}`
    }
  });
  
  return await accountsRes.json();
}
```

---

## 🐛 **استكشاف الأخطاء:**

### **خطأ: 401 Unauthorized**
- ✅ تأكد من استخدام Token صحيح
- ✅ تأكد من إضافة `Authorization: Bearer TOKEN`

### **خطأ: 400 Bad Request**
- ✅ تأكد من Headers المطلوبة:
  - `Content-Type: application/json`
  - `Accept: application/json`
  - `Api-Agent: zapier` (أو `android` أو `ios`)
  - `Accept-Language: en` أو `ar`

### **خطأ: CORS**
- ✅ إذا كنت تختبر من المتصفح، قد تواجه مشكلة CORS
- ✅ استخدم Backend الخاص بنا بدلاً من ذلك

---

## 📝 **ملاحظات:**

1. ✅ **Token ينتهي بعد فترة** - قد تحتاج لتسجيل الدخول مرة أخرى
2. ✅ **استخدم Backend الخاص بنا** لتجنب مشاكل CORS و Token Management
3. ✅ **جميع Headers مطلوبة** - لا تنس أي header

---

**Last Updated:** 2025-01-XX
