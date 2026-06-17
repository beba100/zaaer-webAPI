# VoM Data Debugging Guide

## المشكلة
1. Credit Notes المرسلة لا تظهر كـ "Sent" - لا يزال يظهر زر "Send to VoM"
2. لا تظهر كل السجلات في الجريد

## الإصلاحات المطبقة

### 1. إصلاح قراءة status_vom في Frontend
- تم إضافة محاولات متعددة لقراءة `status_vom` من حقول مختلفة:
  - `item.StatusVoM`
  - `item.status_vom`
  - `item.statusVoM`
  - `item.Status_vom`

### 2. إضافة Logging مفصل في Backend
- تم إضافة logging لـ:
  - عدد السجلات لكل نوع (Invoice, CreditNote, PaymentReceipt)
  - breakdown حسب status_vom لكل نوع
  - عينات من Credit Notes مع status_vom

### 3. إصلاح Date Filters
- إرسال التاريخ كـ `yyyy-MM-dd` فقط بدون وقت أو timezone
- استخدام `DbType.Date` في SQL parameters

## كيفية التحقق من المشكلة

### 1. فحص Logs في Backend
ابحث عن هذه الرسائل في Application Logs:
```
[VoM Data] Credit Notes Status Breakdown (ALL tenants): sent=X, pending=Y, failed=Z
[VoM Data] Credit Note Sample: Number=CRED0001, StatusVoM=sent, HotelCode=Baha4, Date=2025-12-20
```

### 2. فحص Console Logs في Browser
افتح Developer Tools (F12) وابحث عن:
```
[SendToVoM] Credit Note Status Check: {number: "CRED0001", StatusVoM: "sent", ...}
```

### 3. فحص قاعدة البيانات مباشرة
قم بتشغيل هذا الاستعلام على قاعدة بيانات Baha4:
```sql
SELECT 
    credit_note_no,
    status_vom,
    vom_sent_at,
    credit_note_date
FROM credit_notes
WHERE credit_note_no IN ('CRED0001', 'CRED0002')
ORDER BY credit_note_date DESC;
```

### 4. فحص API Response مباشرة
افتح هذا الرابط في المتصفح (استبدل التاريخ حسب الحاجة):
```
https://aleairy.tryasp.net/api/partner-requests/all-hotels/vom-data?dateFrom=2025-11-21&dateTo=2025-12-21
```

ابحث عن Credit Notes وتحقق من وجود `StatusVoM: "sent"` في الـ response.

## الخطوات التالية للتحقق

1. **افتح الـ grid "Send to VoM"**
2. **افتح Developer Tools (F12) → Console tab**
3. **انقر على Refresh**
4. **ابحث عن logs تبدأ بـ `[SendToVoM]`**
5. **تحقق من:**
   - عدد السجلات المحملة
   - breakdown حسب النوع
   - breakdown حسب status_vom
   - عينات من Credit Notes

6. **افتح Application Logs في Backend**
7. **ابحث عن logs تبدأ بـ `[VoM Data]`**
8. **تحقق من:**
   - عدد السجلات لكل tenant
   - breakdown حسب status_vom
   - عينات من Credit Notes

## إذا كانت المشكلة لا تزال موجودة

### المشكلة 1: Credit Notes لا تظهر كـ "Sent"
**التحقق:**
1. تحقق من قاعدة البيانات: هل `status_vom = 'sent'` في جدول `credit_notes`؟
2. تحقق من API Response: هل `StatusVoM: "sent"` موجود في الـ JSON؟
3. تحقق من Console Logs: هل `statusVoM` يتم قراءته بشكل صحيح؟

**الحل المحتمل:**
- إذا كان `status_vom = 'sent'` في قاعدة البيانات لكن لا يظهر في الجريد:
  - تحقق من أن الـ SQL query يجلب `status_vom` بشكل صحيح
  - تحقق من أن الـ frontend يقرأ `StatusVoM` بشكل صحيح

### المشكلة 2: لا تظهر كل السجلات
**التحقق:**
1. تحقق من Application Logs: كم عدد السجلات التي يتم جلبها من كل tenant؟
2. تحقق من Date Filters: هل التاريخ المرسل صحيح؟
3. تحقق من SQL Query: هل الـ WHERE clause يعمل بشكل صحيح؟

**الحل المحتمل:**
- إذا كان عدد السجلات في Logs أكبر من المعروض في الجريد:
  - تحقق من أن الـ frontend لا يقوم بفلترة السجلات بعد تحميلها
  - تحقق من أن الـ grid لا يقوم بإخفاء السجلات

## Test Queries

### Query 1: فحص Credit Notes في Baha4
```sql
SELECT 
    credit_note_no,
    status_vom,
    vom_sent_at,
    credit_note_date,
    hotel_id
FROM credit_notes
WHERE hotel_id = (SELECT hotel_id FROM hotel_settings WHERE zaaer_id = (SELECT zaaer_id FROM hotel_settings LIMIT 1))
    AND credit_note_date >= '2025-11-21'
    AND credit_note_date <= '2025-12-21'
ORDER BY credit_note_date DESC;
```

### Query 2: فحص Payment Receipts في Baha4
```sql
SELECT 
    receipt_no,
    status_vom,
    vom_sent_at,
    receipt_date,
    hotel_id
FROM payment_receipts
WHERE hotel_id = (SELECT hotel_id FROM hotel_settings WHERE zaaer_id = (SELECT zaaer_id FROM hotel_settings LIMIT 1))
    AND receipt_date >= '2025-11-21'
    AND receipt_date <= '2025-12-21'
ORDER BY receipt_date DESC;
```

### Query 3: فحص Invoices في Baha4
```sql
SELECT 
    invoice_no,
    status_vom,
    vom_sent_at,
    invoice_date,
    hotel_id
FROM invoices
WHERE hotel_id = (SELECT hotel_id FROM hotel_settings WHERE zaaer_id = (SELECT zaaer_id FROM hotel_settings LIMIT 1))
    AND invoice_date >= '2025-11-21'
    AND invoice_date <= '2025-12-21'
ORDER BY invoice_date DESC;
```

## ملاحظات مهمة

1. **Date Filters**: التاريخ يُرسل كـ `yyyy-MM-dd` فقط بدون وقت
2. **Status Field**: الـ SQL query يجلب `status_vom AS StatusVoM`
3. **Frontend Reading**: الـ frontend يقرأ من `StatusVoM` أو `status_vom`
4. **No Pagination**: الـ API يرجع كل السجلات بدون pagination

