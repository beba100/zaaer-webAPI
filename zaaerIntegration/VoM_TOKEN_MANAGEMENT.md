# 🔑 VoM Token Management Guide
## دليل إدارة Token لـ VoM

---

## 📋 **كيف يعمل Token Management:**

### **الوضع الحالي:**

1. ✅ **عند أول طلب** إلى `/api/vom/VoMAccount`:
   - النظام يتحقق من وجود Token في الذاكرة
   - إذا لم يوجد، يقوم بتسجيل الدخول تلقائياً
   - يحصل على Token ويحفظه في الذاكرة
   - يستخدم Token في الطلب

2. ⚠️ **مشكلة:** Token في الذاكرة فقط
   - ينتهي عند إعادة تشغيل التطبيق
   - ينتهي عند انتهاء صلاحيته (عادة 24 ساعة)

---

## ✅ **الحل: حفظ Token في appsettings.json**

### **الخطوة 1: الحصول على Token**

#### **الطريقة 1: استخدام Login Endpoint**

```http
POST https://voom.tryasp.net/api/vom/VoMAuth/login
Content-Type: application/json

{
  "email": "Info@aleairygroup.com",
  "password": "Aa1xyjPlNHd"
}
```

**الاستجابة:**
```json
{
  "token": "abcd1234...",
  "message": "Login successful..."
}
```

#### **الطريقة 2: استخدام VoM API مباشرة**

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

---

### **الخطوة 2: حفظ Token في appsettings.json**

افتح `appsettings.json` وأضف Token:

```json
{
  "VoM": {
    "BaseUrl": "https://kimoo.getvom.com",
    "ApiAgent": "zapier",
    "BearerToken": "YOUR_TOKEN_HERE",  // ✅ أضف Token هنا
    "Email": "Info@aleairygroup.com",
    "Password": "Aa1xyjPlNHd"
  }
}
```

**استبدل `YOUR_TOKEN_HERE` بالـ Token من الخطوة 1!**

---

## 🔄 **كيف يعمل النظام بعد إضافة Token:**

### **الأولوية:**

1. ✅ **Token في appsettings.json** (أولوية عالية)
   - إذا كان موجوداً، يستخدمه مباشرة
   - لا حاجة لتسجيل الدخول

2. ✅ **Token في الذاكرة** (إذا كان صالحاً)
   - يستخدم Token المحفوظ في الذاكرة
   - إذا انتهت صلاحيته، يحاول Auto-Login

3. ✅ **Auto-Login** (إذا لم يوجد Token)
   - يستخدم Email و Password من appsettings.json
   - يحصل على Token جديد ويحفظه في الذاكرة

---

## 📝 **مثال عملي:**

### **السيناريو 1: Token في appsettings.json**

```json
"VoM": {
  "BearerToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**النتيجة:**
- ✅ يستخدم Token مباشرة
- ✅ لا حاجة لتسجيل الدخول
- ✅ يعمل حتى بعد إعادة تشغيل التطبيق

---

### **السيناريو 2: بدون Token (Auto-Login)**

```json
"VoM": {
  "BearerToken": "",  // فارغ
  "Email": "Info@aleairygroup.com",
  "Password": "Aa1xyjPlNHd"
}
```

**النتيجة:**
- ✅ يقوم بتسجيل الدخول تلقائياً عند أول طلب
- ✅ يحفظ Token في الذاكرة
- ⚠️ Token ينتهي عند إعادة تشغيل التطبيق

---

## 🎯 **التوصية:**

### **للإنتاج (Production):**
```json
"VoM": {
  "BearerToken": "YOUR_LONG_LIVED_TOKEN",  // ✅ Token دائم
  "Email": "Info@aleairygroup.com",
  "Password": "Aa1xyjPlNHd"  // ✅ للطوارئ (Auto-Login)
}
```

**المميزات:**
- ✅ Token يعمل دائماً
- ✅ Auto-Login كـ Backup إذا انتهى Token
- ✅ لا حاجة لتسجيل الدخول يدوياً

---

## 🔄 **تحديث Token:**

إذا انتهى Token، قم بـ:

1. **الحصول على Token جديد** من Login Endpoint
2. **تحديث appsettings.json**:
   ```json
   "BearerToken": "NEW_TOKEN_HERE"
   ```
3. **إعادة تشغيل التطبيق** (أو استخدام Auto-Login)

---

## 🧪 **الاختبار:**

### **1. اختبار بدون Token (Auto-Login):**
```http
GET https://voom.tryasp.net/api/vom/VoMAccount?language=en
```
- ✅ سيقوم بتسجيل الدخول تلقائياً
- ✅ سيحصل على Token ويستخدمه

### **2. اختبار مع Token في appsettings.json:**
```http
GET https://voom.tryasp.net/api/vom/VoMAccount?language=en
```
- ✅ سيستخدم Token من appsettings.json مباشرة
- ✅ لا حاجة لتسجيل الدخول

---

## 📝 **ملاحظات مهمة:**

1. ✅ **Token في appsettings.json = دائم** (حتى إعادة التشغيل)
2. ✅ **Token في الذاكرة = مؤقت** (ينتهي عند إعادة التشغيل)
3. ✅ **Auto-Login = Backup** (إذا لم يوجد Token)
4. ⚠️ **لا ترفع appsettings.json إلى Git** إذا كان يحتوي على Token

---

**Last Updated:** 2025-01-XX
