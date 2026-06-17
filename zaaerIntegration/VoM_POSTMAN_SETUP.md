# 📦 VoM API Postman Collection Setup
## إعداد Postman Collection لـ VoM API

---

## 🚀 **Quick Setup | الإعداد السريع**

### **الخطوة 1: استيراد Collection**

1. ✅ **افتح Postman**
2. ✅ **اضغط على "Import"** (أعلى يسار)
3. ✅ **اختر ملف:** `VoM_API.postman_collection.json`
4. ✅ **اضغط "Import"**

---

### **الخطوة 2: استيراد Environment**

1. ✅ **اضغط على "Import" مرة أخرى**
2. ✅ **اختر ملف:** `VoM_API.postman_environment.json`
3. ✅ **اضغط "Import"**
4. ✅ **اختر Environment:** `VoM API Environment` (من القائمة المنسدلة أعلى يمين)

---

## 📋 **Collection Structure | هيكل Collection**

### **1. Authentication Folder**
   - ✅ **VoM Login** - تسجيل الدخول والحصول على Token
   - ✅ **Refresh Token** - تحديث Token

### **2. Accounts Folder**
   - ✅ **Get All Accounts** - جلب جميع الحسابات

---

## 🔧 **Environment Variables | المتغيرات**

| Variable | Value | Description |
|----------|-------|-------------|
| `base_url` | `https://aleairy.tryasp.net` | Base URL للتطبيق |
| `vom_token` | (فارغ - سيتم ملؤه تلقائياً) | Bearer Token من Login |
| `language` | `en` | اللغة: `en` أو `ar` |

---

## ✅ **How to Use | كيفية الاستخدام**

### **1. Login (أولاً):**

1. ✅ **افتح:** `VoM API Integration` → `Authentication` → `VoM Login`
2. ✅ **اضغط "Send"**
3. ✅ **النتيجة:** Token سيتم حفظه تلقائياً في `vom_token`

---

### **2. Get Accounts:**

1. ✅ **افتح:** `VoM API Integration` → `Accounts` → `Get All Accounts`
2. ✅ **تأكد من:** Environment `VoM API Environment` مفعل
3. ✅ **اضغط "Send"**
4. ✅ **النتيجة:** قائمة الحسابات

---

## 🎯 **Features | المميزات**

### **✅ Auto Token Saving**
- عند Login، Token يتم حفظه تلقائياً في `vom_token`
- لا حاجة لنسخ/لصق Token يدوياً

### **✅ Environment Variables**
- جميع URLs تستخدم `{{base_url}}`
- سهولة تغيير البيئة (Development/Production)

### **✅ Pre-request Scripts**
- إعداد تلقائي للمتغيرات إذا لم تكن موجودة

---

## 📝 **Manual Setup (بدون ملفات) | الإعداد اليدوي**

### **1. Create Collection:**

1. ✅ **New Collection** → اسمه: `VoM API Integration`

---

### **2. Create Folder: Authentication**

#### **Request 1: VoM Login**

- **Method:** `POST`
- **URL:** `{{base_url}}/api/vom/VoMAuth/login`
- **Headers:**
  ```
  Content-Type: application/json
  Accept: application/json
  ```
- **Body (raw JSON):**
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
      if (jsonData.token) {
          pm.environment.set("vom_token", jsonData.token);
          console.log("✅ Token saved!");
      }
  }
  ```

---

#### **Request 2: Refresh Token**

- **Method:** `POST`
- **URL:** `{{base_url}}/api/vom/VoMAuth/refresh`
- **Headers:**
  ```
  Content-Type: application/json
  Accept: application/json
  ```

---

### **3. Create Folder: Accounts**

#### **Request: Get All Accounts**

- **Method:** `GET`
- **URL:** `{{base_url}}/api/vom/VoMAccount?language={{language}}`
- **Headers:**
  ```
  Content-Type: application/json
  Accept: application/json
  Authorization: Bearer {{vom_token}}
  ```

---

## 🔄 **Change Environment | تغيير البيئة**

### **Development:**
```
base_url = https://aleairy.tryasp.net
```

### **Production:**
```
base_url = https://your-production-url.com
```

---

## 📸 **Screenshots Guide**

### **After Import:**

```
┌─────────────────────────────────────┐
│ Collections                         │
│  📁 VoM API Integration             │
│    📁 Authentication                │
│      📄 VoM Login                   │
│      📄 Refresh Token               │
│    📁 Accounts                      │
│      📄 Get All Accounts            │
└─────────────────────────────────────┘
```

---

## ⚠️ **Troubleshooting | حل المشاكل**

### **Problem: Token not saved**

**Solution:**
- ✅ تأكد من تفعيل Environment: `VoM API Environment`
- ✅ تأكد من وجود Test Script في Login Request

---

### **Problem: 401 Unauthorized**

**Solution:**
- ✅ قم بتسجيل الدخول مرة أخرى (VoM Login)
- ✅ تأكد من وجود `Bearer ` قبل Token في Authorization header

---

### **Problem: Variables not working**

**Solution:**
- ✅ تأكد من تفعيل Environment
- ✅ تأكد من وجود المتغيرات في Environment

---

## ✅ **Checklist | قائمة التحقق**

- [ ] Collection مستورد بنجاح
- [ ] Environment مستورد ومفعل
- [ ] Login Request يعمل ويحفظ Token
- [ ] Get Accounts Request يعمل مع Token
- [ ] جميع المتغيرات تعمل بشكل صحيح

---

**Last Updated:** 2025-01-XX
