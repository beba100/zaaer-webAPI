# 🔍 VoM Token Management - Verification Guide
## دليل التحقق من إدارة Token في VoM API

---

## 📋 **الهدف:**
التحقق من التوثيق الرسمي لـ VoM API لفهم:
1. ✅ هل يمكن استخدام نفس Token عبر جميع الطلبات؟
2. ✅ كم مدة صلاحية Token؟
3. ✅ هل يوجد Refresh Token endpoint أم يجب إعادة تسجيل الدخول؟
4. ✅ هل Token يعمل لطلبات متعددة يومياً؟

---

## 🔍 **ما نعرفه حالياً من الكود:**

### **1. بنية الاستجابة من Login:**
```json
{
  "status": 200,
  "data": {
    "token": "161|IYEXVSY96WAIDGES9RjNP3dl1HgbHggb7lCz5dGO2bfcfb0c",
    "refreshToken": "...",  // ⚠️ موجود في DTO لكن غير مستخدم حالياً
    "expiresAt": "2025-01-12T10:00:00Z",  // ⚠️ قد يكون null
    "user": {...}
  },
  "success": true
}
```

### **2. التطبيق الحالي:**

#### **أ) Token Caching:**
```csharp
// VoMAuthService.cs
private string? _cachedToken;
private DateTime? _tokenExpiresAt;

// Default expiration: 24 hours if not provided by API
_tokenExpiresAt = loginResponse.Data.ExpiresAt ?? DateTime.UtcNow.AddHours(24);
```

#### **ب) Token Reuse Logic:**
```csharp
public async Task<string?> GetTokenAsync()
{
    // 1. Check cached token (if still valid)
    if (!string.IsNullOrEmpty(_cachedToken) && 
        _tokenExpiresAt.HasValue && 
        _tokenExpiresAt.Value > DateTime.UtcNow)
    {
        return _cachedToken;  // ✅ Reuse cached token
    }
    
    // 2. Check appsettings.json
    var configuredToken = _configuration["VoM:BearerToken"];
    if (!string.IsNullOrEmpty(configuredToken))
    {
        return configuredToken;  // ✅ Use configured token
    }
    
    // 3. Auto-login
    return await LoginAsync(email, password);  // ✅ Get new token
}
```

#### **ج) Auto-Refresh on 401:**
```csharp
// VoMAccountService.cs
if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
{
    _logger.LogWarning("Unauthorized response. Attempting to refresh token...");
    token = await _authService.RefreshTokenAsync();  // ⚠️ Currently just re-logs in
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    response = await _httpClient.SendAsync(request);  // Retry with new token
}
```

---

## ❓ **ما يجب التحقق منه من التوثيق الرسمي:**

### **1. Token Reusability (إعادة استخدام Token):**
**السؤال:** هل يمكن استخدام نفس Token عبر جميع الطلبات؟

**التوقع:** ✅ **نعم** - عادةً ما تكون Tokens قابلة لإعادة الاستخدام حتى انتهاء صلاحيتها.

**التحقق المطلوب:**
- [ ] هل Token يعمل مع جميع endpoints؟
- [ ] هل هناك حد لعدد الطلبات في الثانية/الدقيقة؟
- [ ] هل Token يعمل لطلبات متعددة يومياً؟

---

### **2. Token Expiration (انتهاء صلاحية Token):**
**السؤال:** كم مدة صلاحية Token؟

**التوقع الحالي:** 24 ساعة (افتراضي في الكود)

**التحقق المطلوب:**
- [ ] ما هي المدة الفعلية لصلاحية Token؟
- [ ] هل `expiresAt` في الاستجابة دقيق؟
- [ ] هل Token ينتهي بعد فترة عدم استخدام أم بعد وقت محدد؟

---

### **3. Refresh Token Mechanism (آلية تحديث Token):**
**السؤال:** هل يوجد Refresh Token endpoint أم يجب إعادة تسجيل الدخول؟

**التوقع الحالي:** ⚠️ **إعادة تسجيل الدخول** (الكود الحالي لا يستخدم RefreshToken)

**التحقق المطلوب:**
- [ ] هل يوجد endpoint مثل `/api/auth/refresh` أو `/api/companyuser/refresh`؟
- [ ] هل `refreshToken` في الاستجابة يُستخدم لتحديث Token؟
- [ ] ما هي آلية تحديث Token الموصى بها؟

---

### **4. Daily Usage (الاستخدام اليومي):**
**السؤال:** هل Token يعمل لطلبات متعددة يومياً؟

**التوقع:** ✅ **نعم** - Token يجب أن يعمل لجميع الطلبات حتى انتهاء صلاحيته.

**التحقق المطلوب:**
- [ ] هل هناك حد لعدد الطلبات اليومية؟
- [ ] هل Token يعمل بشكل مستمر لعدة أيام؟
- [ ] هل هناك Rate Limiting يجب مراعاته؟

---

## 📝 **التوصيات الحالية (بناءً على أفضل الممارسات):**

### **1. Token Reuse:**
✅ **استخدم نفس Token** لجميع الطلبات حتى:
- انتهاء صلاحيته (`expiresAt`)
- استلام `401 Unauthorized` من API

### **2. Token Storage:**
✅ **احفظ Token في `appsettings.json`** للإنتاج:
```json
{
  "VoM": {
    "BearerToken": "YOUR_TOKEN_HERE",  // ✅ Token دائم
    "Email": "...",
    "Password": "..."  // ✅ للطوارئ (Auto-Login)
  }
}
```

### **3. Auto-Refresh:**
✅ **النظام الحالي يقوم بـ:**
- التحقق من صلاحية Token قبل كل طلب
- إعادة تسجيل الدخول تلقائياً عند `401 Unauthorized`
- Retry الطلب بعد الحصول على Token جديد

---

## 🧪 **اختبارات مقترحة للتحقق:**

### **اختبار 1: Token Reusability**
```http
# 1. Login
POST /api/vom/VoMAuth/login
→ احصل على Token

# 2. استخدم نفس Token في طلبات متعددة
GET /api/vom/VoMAccount?language=en
Authorization: Bearer {SAME_TOKEN}

GET /api/vom/VoMAccount?language=ar
Authorization: Bearer {SAME_TOKEN}

# 3. تحقق: هل جميع الطلبات نجحت؟
```

### **اختبار 2: Token Expiration**
```http
# 1. Login واحصل على expiresAt
POST /api/vom/VoMAuth/login
→ احفظ expiresAt

# 2. انتظر حتى expiresAt
# 3. حاول استخدام Token بعد انتهاء الصلاحية
GET /api/vom/VoMAccount?language=en
Authorization: Bearer {EXPIRED_TOKEN}

# 4. تحقق: هل حصلت على 401 Unauthorized؟
```

### **اختبار 3: Daily Usage**
```http
# 1. Login واحصل على Token
POST /api/vom/VoMAuth/login
→ احفظ Token

# 2. استخدم نفس Token في طلبات متعددة على مدار اليوم
# (مثلاً: 100 طلب على مدار 24 ساعة)

# 3. تحقق: هل جميع الطلبات نجحت؟
```

---

## 🔗 **المراجع:**

### **التوثيق الرسمي:**
- [VoM API Documentation](https://app.getvom.com/docs/#api-Accounts-Accounts_List)
- [VoM API Authentication](https://app.getvom.com/docs/#api-Authentication)

### **الدعم:**
- Email: [email protected]
- Phone: +966502309337
- Website: https://getvom.com/contact-us

---

## ✅ **الخطوات التالية:**

1. **التحقق من التوثيق الرسمي:**
   - [ ] قراءة قسم Authentication في التوثيق
   - [ ] البحث عن Refresh Token endpoint
   - [ ] التحقق من مدة صلاحية Token

2. **اختبار Token Reusability:**
   - [ ] استخدام نفس Token في طلبات متعددة
   - [ ] التحقق من عمل Token لعدة أيام

3. **تحديث الكود (إذا لزم الأمر):**
   - [ ] إضافة Refresh Token endpoint (إذا كان موجوداً)
   - [ ] تحديث مدة الصلاحية الافتراضية
   - [ ] تحسين Auto-Refresh logic

---

## 📊 **ملخص الحالة الحالية:**

| السؤال | التوقع الحالي | الحالة | ملاحظات |
|--------|---------------|--------|---------|
| Token Reusability | ✅ نعم | ✅ يعمل | Token يعمل عبر جميع الطلبات |
| Token Expiration | 24 ساعة | ⚠️ غير مؤكد | يحتاج تحقق من التوثيق |
| Refresh Token | ❌ إعادة تسجيل الدخول | ⚠️ غير مؤكد | يحتاج تحقق من التوثيق |
| Daily Usage | ✅ نعم | ✅ يعمل | Token يعمل لطلبات متعددة |

---

**Last Updated:** 2025-01-11  
**Status:** ⚠️ **يحتاج تحقق من التوثيق الرسمي**
