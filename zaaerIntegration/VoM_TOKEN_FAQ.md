# ❓ VoM Token - الأسئلة الشائعة
## Frequently Asked Questions about VoM Token

---

## ✅ **السؤال 1: هل يمكن استخدام نفس Token عبر جميع الطلبات؟**

### **الإجابة: ✅ نعم**

**التفاصيل:**
- ✅ Token الذي تحصل عليه من Login يمكن استخدامه في **جميع** الطلبات إلى VoM API
- ✅ Token يعمل مع جميع endpoints (Accounts, وغيرها)
- ✅ لا حاجة لتسجيل الدخول قبل كل طلب
- ✅ استخدم نفس Token حتى ينتهي صلاحيته

**مثال:**
```http
# 1. Login مرة واحدة
POST /api/vom/VoMAuth/login
→ Token: "161|IYEXVSY96WAIDGES9RjNP3dl1HgbHggb7lCz5dGO2bfcfb0c"

# 2. استخدم نفس Token في جميع الطلبات
GET /api/vom/VoMAccount?language=en
Authorization: Bearer 161|IYEXVSY96WAIDGES9RjNP3dl1HgbHggb7lCz5dGO2bfcfb0c

GET /api/vom/VoMAccount?language=ar
Authorization: Bearer 161|IYEXVSY96WAIDGES9RjNP3dl1HgbHggb7lCz5dGO2bfcfb0c

# ✅ نفس Token يعمل مع جميع الطلبات!
```

---

## ✅ **السؤال 2: هل يجب تحديث Token أم يمكن استخدامه دائماً؟**

### **الإجابة: ⚠️ يعتمد على مدة الصلاحية**

**التفاصيل:**
- ⚠️ Token له **مدة صلاحية** (عادة 24 ساعة أو أكثر)
- ✅ يمكن استخدام Token **لعدة أيام** إذا لم ينتهِ صلاحيته
- ✅ النظام يقوم بـ **Auto-Refresh** تلقائياً عند انتهاء الصلاحية

**كيف يعمل النظام:**
1. ✅ عند أول طلب: يحصل على Token ويحفظه
2. ✅ في الطلبات التالية: يستخدم نفس Token (إذا كان صالحاً)
3. ✅ عند انتهاء الصلاحية: يقوم بتسجيل الدخول تلقائياً للحصول على Token جديد
4. ✅ عند `401 Unauthorized`: يحاول تحديث Token تلقائياً

---

## ✅ **السؤال 3: هل Token يعمل لطلبات متعددة يومياً؟**

### **الإجابة: ✅ نعم**

**التفاصيل:**
- ✅ Token يعمل لـ **جميع الطلبات** حتى انتهاء صلاحيته
- ✅ يمكن إرسال **مئات الطلبات** يومياً باستخدام نفس Token
- ✅ لا يوجد حد لعدد الطلبات (ما لم يحدده VoM API)

**مثال:**
```http
# نفس Token لـ 1000 طلب في اليوم
GET /api/vom/VoMAccount?language=en  # Request 1
Authorization: Bearer {SAME_TOKEN}

GET /api/vom/VoMAccount?language=en  # Request 2
Authorization: Bearer {SAME_TOKEN}

# ... 998 طلب آخر
# ✅ جميعها تستخدم نفس Token!
```

---

## 🔍 **ما يجب التحقق منه من التوثيق الرسمي:**

### **1. مدة صلاحية Token:**
- [ ] كم مدة صلاحية Token الفعلية؟
- [ ] هل Token ينتهي بعد 24 ساعة أم أكثر؟
- [ ] هل `expiresAt` في الاستجابة دقيق؟

### **2. Refresh Token:**
- [ ] هل يوجد Refresh Token endpoint؟
- [ ] هل يجب استخدام Refresh Token أم إعادة تسجيل الدخول؟
- [ ] ما هي آلية تحديث Token الموصى بها؟

### **3. Rate Limiting:**
- [ ] هل هناك حد لعدد الطلبات في الثانية؟
- [ ] هل هناك حد لعدد الطلبات في اليوم؟
- [ ] ما هي سياسة Rate Limiting في VoM API؟

---

## 📋 **التوصيات:**

### **للإنتاج (Production):**

#### **1. حفظ Token في appsettings.json:**
```json
{
  "VoM": {
    "BearerToken": "161|IYEXVSY96WAIDGES9RjNP3dl1HgbHggb7lCz5dGO2bfcfb0c",
    "Email": "Info@aleairygroup.com",
    "Password": "Aa1xyjPlNHd"
  }
}
```

**المميزات:**
- ✅ Token يعمل حتى بعد إعادة تشغيل التطبيق
- ✅ لا حاجة لتسجيل الدخول يدوياً
- ✅ Auto-Login كـ Backup إذا انتهى Token

#### **2. استخدام نفس Token لجميع الطلبات:**
- ✅ استخدم Token من `appsettings.json` أو الذاكرة
- ✅ لا حاجة لتسجيل الدخول قبل كل طلب
- ✅ النظام يقوم بـ Auto-Refresh تلقائياً

---

## 🧪 **كيفية التحقق:**

### **الطريقة 1: اختبار Token Reusability**
```http
# 1. Login
POST https://aleairy.tryasp.net/api/vom/VoMAuth/login
{
  "email": "Info@aleairygroup.com",
  "password": "Aa1xyjPlNHd"
}

# 2. استخدم نفس Token في طلبات متعددة
GET https://aleairy.tryasp.net/api/vom/VoMAccount?language=en
Authorization: Bearer {TOKEN_FROM_STEP_1}

GET https://aleairy.tryasp.net/api/vom/VoMAccount?language=ar
Authorization: Bearer {SAME_TOKEN_FROM_STEP_1}

# ✅ إذا نجحت جميع الطلبات = Token قابل لإعادة الاستخدام!
```

### **الطريقة 2: فحص Logs**
بعد تسجيل الدخول، تحقق من Logs لرؤية:
- ✅ Token Length
- ✅ Expires At (من API أو افتراضي)
- ✅ Refresh Token (موجود أم لا)

---

## 📊 **الخلاصة:**

| السؤال | الإجابة | الحالة |
|--------|---------|--------|
| هل يمكن استخدام نفس Token عبر جميع الطلبات؟ | ✅ **نعم** | ✅ مؤكد |
| هل Token يعمل لطلبات متعددة يومياً؟ | ✅ **نعم** | ✅ مؤكد |
| كم مدة صلاحية Token؟ | ⚠️ **24 ساعة (افتراضي)** | ⚠️ يحتاج تحقق |
| هل يوجد Refresh Token endpoint؟ | ⚠️ **غير مؤكد** | ⚠️ يحتاج تحقق |

---

## 🔗 **المراجع:**

- [VoM API Documentation](https://app.getvom.com/docs/#api-Accounts-Accounts_List)
- [VoM Token Verification Guide](./VoM_TOKEN_VERIFICATION.md)
- [VoM Token Management Guide](./VoM_TOKEN_MANAGEMENT.md)

---

**Last Updated:** 2025-01-11
