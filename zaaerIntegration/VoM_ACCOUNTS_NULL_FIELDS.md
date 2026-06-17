# 🔍 VoM Accounts API - Null Fields Issue
## مشكلة الحقول الفارغة في VoM Accounts API

---

## 📋 **المشكلة:**

عند استدعاء `/api/accounting/accounts`، معظم الحقول تأتي `null` ما عدا:
- ✅ `id`
- ✅ `code`
- ✅ `status`

**مثال:**
```json
{
  "id": 7,
  "nameAr": null,
  "nameEn": null,
  "code": "011-1",
  "description": null,
  "status": 1,
  // ... جميع الحقول الأخرى null
}
```

---

## 🔍 **الأسباب المحتملة:**

### **1. البيانات في قاعدة البيانات VoM فارغة:**
- ⚠️ الحسابات في VoM قد لا تحتوي على بيانات في هذه الحقول
- ✅ هذا طبيعي إذا كانت الحسابات جديدة أو غير مكتملة

### **2. API يحتاج Query Parameters:**
- ⚠️ بعض APIs تحتاج `?include=...` أو `?fields=...` لجلب البيانات الكاملة
- ✅ قد يحتاج `?with=subcategory,balances` أو `?expand=all`

### **3. أسماء الحقول مختلفة:**
- ✅ **تم الحل:** VoM API يستخدم `snake_case` (name_ar, name_en, used_in_payment)
- ✅ تم تحديث جميع `JsonPropertyName` attributes لاستخدام `snake_case`

### **4. Permissions أو Scope:**
- ⚠️ Token قد لا يحتوي على permissions لعرض جميع الحقول
- ✅ قد يحتاج token بصلاحيات أعلى

---

## ✅ **الحلول المطبقة:**

### **1. إضافة JsonPropertyName Attributes (snake_case):**
```csharp
[JsonPropertyName("name_ar")]
public string? NameAr { get; set; }

[JsonPropertyName("name_en")]
public string? NameEn { get; set; }

[JsonPropertyName("used_in_payment")]
public bool? UsedInPayment { get; set; }

[JsonPropertyName("accounting_sub_category")]
public VoMAccountingSubCategoryDto? AccountingSubCategory { get; set; }
```

### **2. تحسين Logging:**
```csharp
_logger.LogInformation("[VoM Accounts] Raw API Response (first {Length} chars): {Preview}...", 
    previewLength, content.Substring(0, previewLength));
```

### **3. دعم Query Parameters (قيد التطوير):**
```csharp
// يمكن إضافة query parameters مثل:
// ?include=subcategory,balances
// ?fields=id,code,nameAr,nameEn
// ?expand=all
```

---

## 🧪 **خطوات التحقق:**

### **1. فحص الاستجابة الخام من API:**
بعد التحديث، تحقق من Logs لرؤية الـ JSON الخام:
```
[VoM Accounts] Raw API Response (first 2000 chars): {...}
```

### **2. اختبار Query Parameters:**
جرب إضافة query parameters:
```http
GET /api/accounting/accounts?include=subcategory,balances
GET /api/accounting/accounts?fields=id,code,nameAr,nameEn,description
GET /api/accounting/accounts?expand=all
```

### **3. فحص التوثيق الرسمي:**
- [VoM API Documentation](https://app.getvom.com/docs/#api-Accounts-Accounts_List)
- ابحث عن:
  - Query Parameters
  - Response Fields
  - Include/Expand options

---

## 📝 **ما يجب التحقق منه:**

### **من التوثيق الرسمي:**
- [ ] ما هي Query Parameters المتاحة؟
- [ ] هل هناك `include` أو `expand` parameter؟
- [ ] ما هي الحقول الافتراضية في الاستجابة؟
- [ ] هل هناك endpoint مختلف للحصول على بيانات كاملة؟

### **من API Response:**
- [ ] فحص الـ JSON الخام من API
- [ ] التحقق من أسماء الحقول الفعلية
- [ ] التحقق من وجود بيانات في قاعدة البيانات VoM

---

## 🔧 **التوصيات:**

### **1. فحص Logs:**
بعد التحديث، تحقق من Logs لرؤية:
- ✅ الـ JSON الخام من API
- ✅ أسماء الحقول الفعلية
- ✅ القيم الفعلية (null أم موجودة)

### **2. اختبار Query Parameters:**
جرب إضافة query parameters مختلفة:
```http
GET /api/accounting/accounts?include=subcategory
GET /api/accounting/accounts?with=balances
GET /api/accounting/accounts?expand=true
```

### **3. الاتصال بدعم VoM:**
إذا استمرت المشكلة:
- Email: [email protected]
- Phone: +966502309337
- اسأل عن:
  - Query parameters المتاحة
  - كيفية جلب جميع الحقول
  - هل البيانات موجودة في قاعدة البيانات

---

## 📊 **الحالة الحالية:**

| الحل | الحالة | ملاحظات |
|------|--------|---------|
| JsonPropertyName Attributes (snake_case) | ✅ تم | تم تحديث جميع الحقول لاستخدام snake_case |
| Enhanced Logging | ✅ تم | لرؤية الـ JSON الخام |
| Query Parameters Support | ⚠️ قيد التطوير | يحتاج تحقق من التوثيق |
| Documentation Check | ⚠️ مطلوب | فحص التوثيق الرسمي |

---

**Last Updated:** 2025-01-11  
**Status:** ⚠️ **يحتاج تحقق من API Response الفعلي**
