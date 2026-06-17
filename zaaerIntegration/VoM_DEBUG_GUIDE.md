# 🔍 VoM Login 500 Error - Debug Guide
## دليل تصحيح خطأ 500 في Login

---

## 🐛 **المشكلة:**
```
500 Internal Server Error
```

---

## ✅ **الخطوات للتصحيح:**

### **1. تحقق من Request Body في Postman:**

✅ **تأكد من:**
- Body tab → raw → JSON
- JSON صحيح:
  ```json
  {
    "email": "Info@aleairygroup.com",
    "password": "Aa1xyjPlNHd"
  }
  ```

---

### **2. تحقق من Headers:**

✅ **Headers المطلوبة:**
```
Content-Type: application/json
Accept: application/json
```

---

### **3. تحقق من Server Logs:**

افتح ملف الـ logs:
```
logs/log-YYYYMMDD.txt
```

ابحث عن:
- `Login request received`
- `Attempting login`
- `HTTP error`
- `Unexpected error`

---

### **4. تحقق من appsettings.json:**

✅ **تأكد من:**
```json
"VoM": {
  "BaseUrl": "https://kimoo.getvom.com",
  "ApiAgent": "zapier",
  "Email": "Info@aleairygroup.com",
  "Password": "Aa1xyjPlNHd"
}
```

---

### **5. اختبار مباشر:**

#### **في Postman:**

**Request:**
```
POST https://aleairy.tryasp.net/api/vom/VoMAuth/login
```

**Headers:**
```
Content-Type: application/json
Accept: application/json
```

**Body (raw JSON):**
```json
{
  "email": "Info@aleairygroup.com",
  "password": "Aa1xyjPlNHd"
}
```

---

## 🔧 **الحلول المحتملة:**

### **الحل 1: Request Body فارغ**

**المشكلة:** Body غير موجود أو فارغ

**الحل:**
- ✅ تأكد من وجود Body في Postman
- ✅ اختر `raw` → `JSON`
- ✅ تأكد من صحة JSON format

---

### **الحل 2: مشكلة في الاتصال بـ VoM API**

**المشكلة:** لا يمكن الاتصال بـ `https://kimoo.getvom.com`

**الحل:**
- ✅ تحقق من الاتصال بالإنترنت
- ✅ تحقق من أن VoM API متاح
- ✅ تحقق من SSL certificate

---

### **الحل 3: مشكلة في Credentials**

**المشكلة:** Email أو Password غير صحيح

**الحل:**
- ✅ تحقق من صحة Email و Password
- ✅ جرب Login مباشرة على VoM API

---

## 📋 **Checklist:**

- [ ] Request Body موجود وصحيح
- [ ] Headers موجودة
- [ ] appsettings.json صحيح
- [ ] Server logs تم فحصها
- [ ] VoM API متاح

---

## 🎯 **الخطوات التالية:**

1. ✅ أعد المحاولة مع Body صحيح
2. ✅ تحقق من Server Logs
3. ✅ إذا استمرت المشكلة، أرسل Server Logs

---

**Last Updated:** 2025-01-XX
