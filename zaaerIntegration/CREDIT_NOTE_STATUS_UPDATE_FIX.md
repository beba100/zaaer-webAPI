# Credit Note Status Update Fix

## المشكلة
عند إرسال Credit Note بنجاح إلى VoM، لا يتم تحديث `status_vom` والحقول الأخرى (`vom_sent_at`, `vom_payload`, `vom_error`, `vom_retry_count`) في جدول `credit_notes`.

## السبب الجذري
الـ `creditNote` يتم تحميله باستخدام `.AsNoTracking()` في الـ controller (`VoMJournalEntryController.cs` و `VoMInvoiceReturnController.cs`). هذا يعني أن Entity Framework لا يتتبع التغييرات على هذا الكائن.

عندما يحاول الـ service (`CreditNoteInvoiceReturnService`) تحديث `creditNote` باستخدام `Update()` و `SaveChangesAsync()`, لا يتم حفظ التغييرات لأن الكائن غير tracked.

## الحل المطبق

### 1. إعادة تحميل creditNote قبل التحديث
في `CreditNoteInvoiceReturnService.cs`، تم إضافة كود لإعادة تحميل `creditNote` من قاعدة البيانات قبل التحديث:

```csharp
// Reload creditNote from database to ensure it's tracked
var creditNoteToUpdate = await _context.CreditNotes
    .FirstOrDefaultAsync(cn => cn.CreditNoteId == creditNote.CreditNoteId);

if (creditNoteToUpdate != null)
{
    creditNoteToUpdate.StatusVoM = "sent";
    creditNoteToUpdate.VomSentAt = Utilities.KsaTime.Now;
    creditNoteToUpdate.VomError = null;
    creditNoteToUpdate.VomRetryCount = 0;
    
    // No need for Update() - Entity Framework tracks changes automatically
    await _context.SaveChangesAsync();
}
```

### 2. إصلاح Idempotency Check
تم إصلاح الـ idempotency check للتحقق من قاعدة البيانات مباشرة بدلاً من الاعتماد على القيمة المحملة بـ `AsNoTracking()`:

```csharp
// Reload creditNote from database to get the latest status_vom value
var creditNoteFromDb = await _context.CreditNotes
    .AsNoTracking()
    .FirstOrDefaultAsync(cn => cn.CreditNoteId == creditNote.CreditNoteId);

if (creditNoteFromDb != null && creditNoteFromDb.StatusVoM == "sent")
{
    return true; // Already sent successfully - prevent duplicate
}
```

### 3. إضافة Logging أفضل
تم إضافة logging لمعرفة ما يحدث:
- Logging لـ `SaveChangesAsync()` result
- Logging لـ StackTrace في حالة الأخطاء
- Logging أفضل للـ debugging

## الملفات المعدلة

1. **`zaaerIntegration/Services/CreditNoteInvoiceReturnService.cs`**
   - إصلاح تحديث `status_vom` بعد الإرسال الناجح
   - إصلاح تحديث `status_vom` بعد الفشل
   - إصلاح تحديث `status_vom` في حالة الـ exception
   - إصلاح idempotency check

## النتيجة المتوقعة

بعد الإصلاح:
1. ✅ عند إرسال Credit Note بنجاح، يتم تحديث `status_vom = 'sent'` في قاعدة البيانات
2. ✅ يتم تحديث `vom_sent_at`, `vom_payload`, `vom_error = null`, `vom_retry_count = 0`
3. ✅ عند الفشل، يتم تحديث `status_vom = 'failed'` و `vom_retry_count++`
4. ✅ الـ idempotency check يعمل بشكل صحيح - لا يتم إرسال Credit Note مرتين

## للاختبار

1. **اختبار الإرسال الناجح:**
   - أرسل Credit Note جديد إلى VoM
   - تحقق من قاعدة البيانات: `status_vom` يجب أن يكون `'sent'`
   - تحقق من `vom_sent_at` يجب أن يحتوي على timestamp

2. **اختبار الإرسال بعد الفشل:**
   - أرسل Credit Note فاشل مرة أخرى بعد التصحيح
   - تحقق من قاعدة البيانات: `status_vom` يجب أن يتغير من `'failed'` إلى `'sent'`
   - تحقق من `vom_retry_count` يجب أن يتم reset إلى `0`

3. **اختبار Idempotency:**
   - حاول إرسال Credit Note مرتين
   - يجب أن يعود `true` في المرة الثانية بدون إرسال فعلي

## ملاحظات مهمة

- **Entity Framework Tracking**: عند استخدام `AsNoTracking()`, يجب إعادة تحميل الكائن قبل التحديث
- **SaveChangesAsync()**: لا حاجة لاستخدام `Update()` إذا كان الكائن tracked - Entity Framework يتتبع التغييرات تلقائياً
- **Logging**: تم إضافة logging مفصل لمساعدة في debugging

