# 🚀 VoM API Testing Guide - Postman
## دليل اختبار VoM API على Postman

---

## 📋 **Prerequisites | المتطلبات**

- ✅ Postman مثبت
- ✅ URL الخاص بك: `https://aleairy.tryasp.net`
- ✅ بيانات الدخول:
  - Email: `Info@aleairygroup.com`
  - Password: `Aa1xyjPlNHd`

---

## 🔐 **1. Test Login Endpoint | اختبار Login**

### **Request Configuration:**

#### **Method & URL:**
```
POST https://aleairy.tryasp.net/api/vom/VoMAuth/login
```

#### **Headers:**
```
Content-Type: application/json
Accept: application/json
```

#### **Body (raw JSON):**
```json
{
  "email": "Info@aleairygroup.com",
  "password": "Aa1xyjPlNHd"
}
```

---

### **Step-by-Step في Postman:**

1. ✅ **افتح Postman**
2. ✅ **اختر Method:** `POST`
3. ✅ **أدخل URL:** `https://aleairy.tryasp.net/api/vom/VoMAuth/login`
4. ✅ **اذهب إلى تبويب "Headers":**
   - أضف: `Content-Type: application/json`
   - أضف: `Accept: application/json`
5. ✅ **اذهب إلى تبويب "Body":**
   - اختر: `raw`
   - اختر: `JSON` (من القائمة المنسدلة)
   - الصق الكود التالي:
   ```json
   {
     "email": "Info@aleairygroup.com",
     "password": "Aa1xyjPlNHd"
   }
   ```
6. ✅ **اضغط "Send"**

---

### **Expected Response | الاستجابة المتوقعة:**

```json
{
  "status": 200,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "...",
    "expiresAt": "2025-01-11T14:30:00Z",
    "user": {
      "id": 1,
      "email": "Info@aleairygroup.com",
      ...
    }
  },
  "success": true,
  "errors": null
}
```

---

## 📊 **2. Test Accounts Endpoint | اختبار Accounts**

### **Request Configuration:**

#### **Method & URL:**
```
GET https://aleairy.tryasp.net/api/vom/VoMAccount?language=en
```

#### **Headers:**
```
Content-Type: application/json
Accept: application/json
Authorization: Bearer YOUR_TOKEN_HERE
```

**ملاحظة:** استبدل `YOUR_TOKEN_HERE` بالـ Token من Login Response

---

### **Step-by-Step في Postman:**

1. ✅ **اختر Method:** `GET`
2. ✅ **أدخل URL:** `https://aleairy.tryasp.net/api/vom/VoMAccount?language=en`
3. ✅ **اذهب إلى تبويب "Headers":**
   - أضف: `Content-Type: application/json`
   - أضف: `Accept: application/json`
   - أضف: `Authorization: Bearer {YOUR_TOKEN}`
     - **مثال:** `Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
4. ✅ **اضغط "Send"**

---

### **Expected Response | الاستجابة المتوقعة:**

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
  "success": true,
  "errors": null
}
```

---

## 🔄 **3. Using Postman Environment Variables | استخدام متغيرات Postman**

### **Setup Environment:**

1. ✅ **انقر على "Environments" في Postman**
2. ✅ **اضغط "+" لإنشاء Environment جديد**
3. ✅ **أضف المتغيرات التالية:**

| Variable | Initial Value | Current Value |
|----------|---------------|---------------|
| `base_url` | `https://aleairy.tryasp.net` | `https://aleairy.tryasp.net` |
| `vom_token` | (اتركه فارغاً) | (سيتم ملؤه تلقائياً) |

---

### **استخدام المتغيرات:**

#### **Login URL:**
```
POST {{base_url}}/api/vom/VoMAuth/login
```

#### **Accounts URL:**
```
GET {{base_url}}/api/vom/VoMAccount?language=en
```

#### **Authorization Header:**
```
Bearer {{vom_token}}
```

---

## 🎯 **4. Postman Collection | مجموعة Postman**

### **Create Collection:**

1. ✅ **انقر على "Collections"**
2. ✅ **اضغط "+ New Collection"**
3. ✅ **اسمها:** `VoM API Integration`

---

### **Add Requests:**

#### **Request 1: Login**
- **Name:** `VoM Login`
- **Method:** `POST`
- **URL:** `{{base_url}}/api/vom/VoMAuth/login`
- **Body:**
  ```json
  {
    "email": "Info@aleairygroup.com",
    "password": "Aa1xyjPlNHd"
  }
  ```
- **Tests (Script):**
  ```javascript
  if (pm.response.code === 200) {
      var jsonData = pm.response.json();
      pm.environment.set("vom_token", jsonData.data.token);
      console.log("Token saved:", jsonData.data.token);
  }
  ```

#### **Request 2: Get Accounts**
- **Name:** `Get VoM Accounts`
- **Method:** `GET`
- **URL:** `{{base_url}}/api/vom/VoMAccount?language=en`
- **Headers:**
  ```
  Authorization: Bearer {{vom_token}}
  ```

---

## ⚠️ **Common Errors | الأخطاء الشائعة**

### **1. 500 Internal Server Error**

**السبب:** Body فارغ أو غير صحيح

**الحل:**
- ✅ تأكد من وجود Body في تبويب "Body"
- ✅ اختر `raw` و `JSON`
- ✅ تأكد من صحة JSON format

---

### **2. 401 Unauthorized**

**السبب:** Token غير صحيح أو منتهي الصلاحية

**الحل:**
- ✅ قم بتسجيل الدخول مرة أخرى للحصول على Token جديد
- ✅ تأكد من إضافة `Bearer ` قبل Token

---

### **3. 400 Bad Request**

**السبب:** بيانات غير صحيحة في Body

**الحل:**
- ✅ تأكد من وجود `email` و `password`
- ✅ تأكد من صحة تنسيق JSON

---

## 📸 **Screenshots Guide | دليل الصور**

### **Login Request:**

```
┌─────────────────────────────────────────┐
│ POST | https://aleairy.tryasp.net/...  │
├─────────────────────────────────────────┤
│ Headers (2)                             │
│   Content-Type: application/json         │
│   Accept: application/json               │
├─────────────────────────────────────────┤
│ Body (raw JSON)                         │
│ {                                       │
│   "email": "Info@aleairygroup.com",     │
│   "password": "Aa1xyjPlNHd"             │
│ }                                       │
└─────────────────────────────────────────┘
```

---

### **Accounts Request:**

```
┌─────────────────────────────────────────┐
│ GET | https://aleairy.tryasp.net/...    │
├─────────────────────────────────────────┤
│ Headers (3)                              │
│   Content-Type: application/json        │
│   Accept: application/json               │
│   Authorization: Bearer eyJhbGc...      │
└─────────────────────────────────────────┘
```

---

## ✅ **Quick Test Checklist | قائمة الاختبار السريع**

- [ ] Login Request يعمل ويعيد Token
- [ ] Token يتم حفظه في Environment Variable
- [ ] Accounts Request يعمل مع Token
- [ ] Response يحتوي على البيانات المتوقعة
- [ ] Headers صحيحة في جميع الطلبات

---

## 🎁 **Bonus: Postman Pre-request Script**

### **Auto-Login إذا انتهى Token:**

```javascript
// في Accounts Request → Pre-request Script
var token = pm.environment.get("vom_token");

if (!token) {
    // Auto-login
    pm.sendRequest({
        url: pm.environment.get("base_url") + "/api/vom/VoMAuth/login",
        method: 'POST',
        header: {
            'Content-Type': 'application/json'
        },
        body: {
            mode: 'raw',
            raw: JSON.stringify({
                email: "Info@aleairygroup.com",
                password: "Aa1xyjPlNHd"
            })
        }
    }, function (err, res) {
        if (res.code === 200) {
            var jsonData = res.json();
            pm.environment.set("vom_token", jsonData.data.token);
        }
    });
}
```

---

**Last Updated:** 2025-01-XX
