# 🎨 VoM API UI Test Interface Guide
## دليل استخدام واجهة اختبار VoM API

---

## 🚀 **Quick Start | البدء السريع**

### **1. الوصول إلى الواجهة:**

افتح المتصفح واذهب إلى:
```
https://aleairy.tryasp.net/vom-test.html
```

أو محلياً:
```
http://localhost:5000/vom-test.html
```

---

## 📋 **Features | المميزات**

### **✅ 1. Login Section**
- تسجيل الدخول إلى VoM API
- الحصول على Bearer Token تلقائياً
- حفظ Token في الواجهة

### **✅ 2. Refresh Token**
- تحديث Token إذا انتهت صلاحيته

### **✅ 3. Get Accounts**
- جلب جميع الحسابات من VoM API
- استخدام Token تلقائياً

### **✅ 4. Real-time Logs**
- عرض الـ logs في الوقت الفعلي
- Frontend logs (من JavaScript)
- Backend logs (من Server)

### **✅ 5. Response Viewer**
- عرض الاستجابة بشكل منسق (JSON)
- عرض حالة الطلب (Success/Error)

---

## 🎯 **How to Use | كيفية الاستخدام**

### **الخطوة 1: Login**

1. ✅ **أدخل Email و Password** (مملوءة مسبقاً)
2. ✅ **اضغط "Login to VoM API"**
3. ✅ **انتظر** - سيظهر Token تلقائياً في حقل "Bearer Token"

---

### **الخطوة 2: Get Accounts**

1. ✅ **تأكد من وجود Token** (يتم ملؤه تلقائياً بعد Login)
2. ✅ **اختر اللغة** (English/Arabic)
3. ✅ **اضغط "Get All Accounts"**
4. ✅ **شاهد النتائج** في Response Section

---

### **الخطوة 3: View Logs**

#### **Frontend Logs:**
- ✅ تظهر تلقائياً في قسم "Logs"
- ✅ تعرض جميع العمليات (Login, Get Accounts, etc.)

#### **Backend Logs:**
- ✅ اضغط "Load Backend Logs"
- ✅ سيتم جلب آخر 50 سطر من Server Logs
- ✅ تعرض معلومات مفصلة من Server

---

## 📊 **Log Types | أنواع الـ Logs**

### **✅ Success (أخضر)**
- عمليات ناجحة
- Login successful
- Accounts retrieved

### **⚠️ Warning (أصفر)**
- تحذيرات
- معلومات مهمة

### **❌ Error (أحمر)**
- أخطاء
- فشل في الطلبات
- مشاكل في الاتصال

### **ℹ️ Info (أزرق)**
- معلومات عامة
- بدء العمليات
- حالات النظام

---

## 🔧 **Configuration | الإعدادات**

### **Base URL:**
```
https://aleairy.tryasp.net
```

يمكن تغييره إذا كان لديك URL مختلف.

---

### **Language:**
- `en` - English
- `ar` - Arabic

---

## 📝 **Log Format | تنسيق الـ Logs**

### **Frontend Logs:**
```
[HH:MM:SS] 🔄 Attempting login for: Info@aleairygroup.com
[HH:MM:SS] ✅ Login successful! Token received (234.56ms)
```

### **Backend Logs:**
```
[VoM Login] Attempting login for email: Info@aleairygroup.com | IP: 192.168.1.1
[VoM Login] ✅ Login successful for email: Info@aleairygroup.com | Duration: 234.56ms
```

---

## 🎁 **Advanced Features | مميزات متقدمة**

### **1. Auto Token Management**
- ✅ Token يتم حفظه تلقائياً بعد Login
- ✅ يتم استخدامه تلقائياً في Get Accounts

### **2. Real-time Updates**
- ✅ Logs تظهر فوراً
- ✅ Response يتم تحديثه تلقائياً

### **3. Error Handling**
- ✅ معالجة شاملة للأخطاء
- ✅ رسائل خطأ واضحة
- ✅ Stack traces للأخطاء

### **4. Performance Metrics**
- ✅ عرض مدة كل طلب (milliseconds)
- ✅ تتبع الأداء

---

## 🔍 **Troubleshooting | حل المشاكل**

### **Problem: Token not saved**

**Solution:**
- ✅ تأكد من نجاح Login
- ✅ تحقق من Response في Response Section

---

### **Problem: 401 Unauthorized**

**Solution:**
- ✅ قم بتسجيل الدخول مرة أخرى
- ✅ اضغط "Refresh Token"

---

### **Problem: Backend Logs not loading**

**Solution:**
- ✅ تأكد من أن Server يعمل
- ✅ تحقق من Base URL
- ✅ تحقق من Server Logs في ملفات الـ logs

---

## 📋 **Checklist | قائمة التحقق**

- [ ] الواجهة تعمل بشكل صحيح
- [ ] Login يعمل ويعيد Token
- [ ] Get Accounts يعمل مع Token
- [ ] Frontend Logs تظهر
- [ ] Backend Logs يمكن تحميلها
- [ ] Response Viewer يعمل

---

## 🎯 **Next Steps | الخطوات التالية**

1. ✅ جرب Login
2. ✅ جرب Get Accounts
3. ✅ شاهد Logs
4. ✅ جرب Load Backend Logs

---

**Last Updated:** 2025-01-XX
