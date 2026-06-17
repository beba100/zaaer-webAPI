# 🔄 VoM Reverse Entry Workflow - شرح كامل للعملية

## 📋 **السيناريوهات الكاملة (Complete Scenarios)**

---

## ✅ **السيناريو 1: سند قبض عادي (Normal Payment Receipt)**

### **الخطوة 1: إنشاء السند**
```
المستخدم ينشئ سند قبض جديد:
- ReceiptNo: "REC001"
- AmountPaid: 1000.00
- ReceiptStatus: "active"
- StatusVoM: "pending" (افتراضي)
- VomReverseSent: 0 (افتراضي)
```

### **الخطوة 2: إرسال السند إلى VoM (Normal Send)**
```
عندما يتم إرسال السند إلى VoM:
1. يتم استدعاء: CreateJournalEntryForPaymentReceiptAsync(receipt)
2. يتم بناء القيد العادي:
   - Line 1: Account 51 (Customer) → Credit: 1000, Debit: 0
   - Line 2: Account 175 (Cash Box) → Debit: 1000, Credit: 0
3. يتم إرساله إلى VoM API
4. عند النجاح:
   ✅ StatusVoM = "sent"
   ✅ VomSentAt = الآن
   ✅ VomPayload = JSON القيد
   ✅ VomReverseSent = 0 (لا يزال 0 - لم يتم إرسال عكسي بعد)
```

**النتيجة:**
```
REC001:
- ReceiptStatus: "active"
- StatusVoM: "sent" ✅
- VomReverseSent: 0
- VomSentAt: 2025-12-20 10:00:00
```

---

## 🔄 **السيناريو 2: إلغاء السند (Cancelling Receipt)**

### **الحالة 2.1: السند لم يُرسل إلى VoM بعد (تم إلغاؤه)**

```
المستخدم يلغي السند:
- ReceiptStatus: "active" → "cancelled"
- StatusVoM: "pending" (لم يُرسل بعد)
- VomReverseSent: 0
```

**ماذا يحدث؟**
```
✅ يتم إرسال القيد العادي أولاً (CreateJournalEntryForPaymentReceiptAsync)
✅ بعد النجاح، يتم إرسال القيد العكسي مباشرة (CreateReverseJournalEntryForCancelledReceiptAsync)

السبب: المحاسب يريد أن يكون لديه في النظام المحاسبي:
- أصل السند (القيد العادي)
- المعكوس (القيد العكسي)
حتى لو تم إلغاء السند قبل إرساله
```

**النتيجة:**
```
REC001:
- ReceiptStatus: "cancelled"
- StatusVoM: "sent" ✅ (تم إرسال القيد العادي أولاً)
- VomReverseSent: 1 ✅ (تم إرسال القيد العكسي)

في VoM:
- القيد العادي: REC001 (Credit Customer 1000, Debit Cash 1000)
- القيد العكسي: REV-REC001 (Debit Customer 1000, Credit Cash 1000)
- النتيجة: صفر (تم إلغاء القيد)
```

---

### **الحالة 2.2: السند تم إرساله إلى VoM ثم تم إلغاؤه**

```
الحالة الأولية:
- ReceiptStatus: "active"
- StatusVoM: "sent" ✅ (تم الإرسال بنجاح)
- VomReverseSent: 0
- VomSentAt: 2025-12-20 10:00:00

المستخدم يلغي السند:
- ReceiptStatus: "active" → "cancelled"
```

**ماذا يحدث؟**

#### **الطريقة 1: عبر Job (تلقائي - كل 30 دقيقة)**

```
1. Job يعمل كل 30 دقيقة
2. Job يبحث عن:
   - ReceiptStatus = "cancelled"
   - StatusVoM = "sent" ✅
   - VomReverseSent = 0 ✅
   - VomRetryCount < maxRetries
3. Job يستدعي: CreateReverseJournalEntryForCancelledReceiptAsync(receipt)
4. يتم بناء القيد العكسي:
   - Code: "REV-REC001" (كود جديد)
   - Line 1: Account 51 (Customer) → Debit: 1000, Credit: 0 (عكس!)
   - Line 2: Account 175 (Cash Box) → Debit: 0, Credit: 1000 (عكس!)
   - Memo: "قيد عكسي لسند قبض رقم REC001"
5. يتم إرساله إلى VoM API
6. عند النجاح:
   ✅ VomReverseSent = 1 (تم الإرسال العكسي)
   ✅ VomError = NULL
   ✅ يتم حفظ في payment_receipt_journal_entries
```

**النتيجة:**
```
REC001:
- ReceiptStatus: "cancelled"
- StatusVoM: "sent" (لا يتغير - يبقى "sent")
- VomReverseSent: 1 ✅ (تم إرسال العكسي)
- VomSentAt: 2025-12-20 10:00:00 (تاريخ الإرسال الأصلي)

في VoM:
- القيد الأصلي: REC001 (Credit Customer 1000, Debit Cash 1000)
- القيد العكسي: REV-REC001 (Debit Customer 1000, Credit Cash 1000)
- النتيجة: صفر (تم إلغاء القيد الأصلي)
```

---

#### **الطريقة 2: عبر API مباشر (فوري - عند الإلغاء)**

```
عندما يتم تحديث السند عبر API:
1. يتم تحديث ReceiptStatus = "cancelled"
2. يتم حفظ التغييرات في DB
3. بعد الحفظ، يتم استدعاء:
   CreateReverseJournalEntryForCancelledReceiptAsync(receipt)
4. نفس العملية كما في Job (بناء وإرسال القيد العكسي)
```

---

## 🔍 **السيناريو 3: محاولة إرسال عكسي مرتين (Idempotency)**

```
الحالة:
- ReceiptStatus: "cancelled"
- StatusVoM: "sent"
- VomReverseSent: 1 ✅ (تم إرسال العكسي بالفعل)

ماذا يحدث إذا حاول Job إرسال العكسي مرة أخرى؟
```

**النتيجة:**
```
❌ لا شيء! الشرط في الكود:
if (receipt.VomReverseSent)
{
    _logger.LogInformation("Reverse entry already sent");
    return true; // لا يتم إرسال مرة أخرى
}
```

**النتيجة:**
```
REC001:
- VomReverseSent: 1 (لا يتغير)
- لا يتم إرسال قيد عكسي جديد
- لا يوجد تكرار في VoM
```

---

## 🔍 **السيناريو 4: فشل إرسال القيد العكسي**

```
الحالة:
- ReceiptStatus: "cancelled"
- StatusVoM: "sent"
- VomReverseSent: 0

Job يحاول إرسال القيد العكسي:
1. يتم بناء القيد العكسي
2. يتم إرساله إلى VoM API
3. ❌ فشل الإرسال (مثلاً: خطأ في VoM API)
```

**ماذا يحدث؟**
```
✅ VomReverseSent = 0 (لا يتغير - لم ينجح الإرسال)
✅ StatusVoM = "sent" (لا يتغير - القيد الأصلي لا يزال "sent")
✅ VomError = "خطأ من VoM API..."
✅ VomRetryCount++ (يتم زيادة عدد المحاولات)
✅ يتم حفظ في payment_receipt_journal_entries مع Status = "Failed"
```

**النتيجة:**
```
REC001:
- ReceiptStatus: "cancelled"
- StatusVoM: "sent" (لا يزال "sent")
- VomReverseSent: 0 (لم ينجح الإرسال العكسي)
- VomRetryCount: 1
- VomError: "خطأ من VoM API..."

في المرة القادمة (Job التالي):
- Job سيجد: VomReverseSent = 0 و VomRetryCount < maxRetries
- Job سيحاول إرسال العكسي مرة أخرى
```

---

## 📊 **جدول الحالات (State Table)**

| ReceiptStatus | StatusVoM | VomReverseSent | ماذا يحدث؟ |
|---------------|-----------|----------------|-------------|
| `active` | `pending` | `0` | ✅ إرسال عادي (عند الطلب) |
| `active` | `sent` | `0` | ✅ تم الإرسال - لا شيء |
| `cancelled` | `pending` | `0` | ✅ **إرسال عادي أولاً ثم عكسي** (Job أو API) |
| `cancelled` | `sent` | `0` | ✅ **إرسال قيد عكسي فقط** (Job أو API) |
| `cancelled` | `sent` | `1` | ✅ تم الإرسال العكسي - لا شيء |
| `cancelled` | `failed` | `0` | ❌ لا شيء (القيد الأصلي فشل) |

---

## 🔄 **الـ Workflow الكامل (Complete Flow)**

```
┌─────────────────────────────────────────────────────────────┐
│ 1. إنشاء سند قبض                                            │
│    ReceiptStatus = "active"                                 │
│    StatusVoM = "pending"                                    │
│    VomReverseSent = 0                                       │
└─────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. إرسال السند إلى VoM (Normal Send)                       │
│    CreateJournalEntryForPaymentReceiptAsync()               │
│    → StatusVoM = "sent" ✅                                  │
│    → VomSentAt = الآن                                       │
│    → VomReverseSent = 0 (لا يزال 0)                        │
└─────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. المستخدم يلغي السند                                      │
│    ReceiptStatus = "active" → "cancelled"                   │
│    StatusVoM = "sent" (لا يتغير)                            │
│    VomReverseSent = 0 (لا يزال 0)                          │
└─────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. Job يعمل (كل 30 دقيقة) أو API يستدعي مباشرة             │
│    يبحث عن:                                                 │
│    - ReceiptStatus = "cancelled"                             │
│    - StatusVoM = "sent" ✅                                  │
│    - VomReverseSent = 0 ✅                                  │
└─────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────┐
│ 5. بناء القيد العكسي                                        │
│    CreateReverseJournalEntryForCancelledReceiptAsync()      │
│    Code: "REV-REC001"                                       │
│    Line 1: Account 51 → Debit: 1000, Credit: 0 (عكس!)      │
│    Line 2: Account 175 → Debit: 0, Credit: 1000 (عكس!)     │
└─────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────┐
│ 6. إرسال القيد العكسي إلى VoM                               │
│    POST /api/accounting/journal-entries                     │
│    → VoM API يعيد: Success ✅                              │
└─────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────┐
│ 7. تحديث الحالة                                             │
│    VomReverseSent = 1 ✅                                    │
│    StatusVoM = "sent" (لا يتغير)                           │
│    VomError = NULL                                          │
│    → حفظ في payment_receipt_journal_entries                │
└─────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────┐
│ 8. النتيجة النهائية                                         │
│    في VoM:                                                  │
│    - القيد الأصلي: REC001 (Credit 1000, Debit 1000)        │
│    - القيد العكسي: REV-REC001 (Debit 1000, Credit 1000)    │
│    - النتيجة: صفر (تم إلغاء القيد)                         │
│                                                              │
│    في DB:                                                   │
│    - ReceiptStatus: "cancelled"                             │
│    - StatusVoM: "sent"                                       │
│    - VomReverseSent: 1 ✅                                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 🎯 **الخلاصة (Summary)**

### **القواعد الأساسية:**

1. ✅ **إرسال عادي:** عندما `StatusVoM = "pending"` → إرسال عادي → `StatusVoM = "sent"`

2. ✅ **إرسال عكسي:** فقط عندما:
   - `ReceiptStatus = "cancelled"` **و**
   - `StatusVoM = "sent"` **و**
   - `VomReverseSent = 0`

3. ✅ **منع التكرار:** بعد نجاح الإرسال العكسي → `VomReverseSent = 1` → لا يتم الإرسال مرة أخرى

4. ✅ **معالجة الأخطاء:** عند فشل الإرسال العكسي:
   - `VomReverseSent` يبقى `0`
   - `VomRetryCount++`
   - Job سيحاول مرة أخرى في المرة القادمة

---

## 📝 **أمثلة عملية (Practical Examples)**

### **مثال 1: سند تم إلغاؤه قبل الإرسال**
```
REC002:
- ReceiptStatus: "cancelled"
- StatusVoM: "pending"
- النتيجة: ❌ لا يتم إرسال قيد عكسي (لم يُرسل أصلاً)
```

### **مثال 2: سند تم إرساله ثم تم إلغاؤه**
```
REC003:
- ReceiptStatus: "cancelled"
- StatusVoM: "sent"
- VomReverseSent: 0
- النتيجة: ✅ يتم إرسال قيد عكسي → VomReverseSent = 1
```

### **مثال 3: سند تم إلغاؤه وتم إرسال العكسي**
```
REC004:
- ReceiptStatus: "cancelled"
- StatusVoM: "sent"
- VomReverseSent: 1
- النتيجة: ✅ لا يتم إرسال مرة أخرى (تم بالفعل)
```

---

**هل هذا واضح الآن؟** 🎯

**المرحلة التالية:** يمكنك الآن:
1. ✅ تشغيل SQL Script
2. ✅ إعادة بناء التطبيق
3. ✅ اختبار السيناريوهات

