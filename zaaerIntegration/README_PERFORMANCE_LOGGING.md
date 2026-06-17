# 📊 Performance Monitoring Logging System
# نظام تسجيل مراقبة الأداء

## 📁 ملفات الـ Logs

### 1. ملف Log العام
- **المسار**: `logs/log-YYYYMMDD.txt`
- **المحتوى**: جميع الـ logs العادية (أخطاء، معلومات، تحذيرات)
- **التحديث**: يومي

### 2. ملف Performance Log (الجديد) ⭐
- **المسار**: `logs/performance/performance-YYYYMMDD.txt`
- **المحتوى**: فقط الـ logs الخاصة بمراقبة الأداء
- **التحديث**: يومي
- **الاحتفاظ**: آخر 30 يوم
- **الحجم الأقصى**: 10 MB لكل ملف

## 🔍 كيفية البحث في Performance Logs

### البحث عن Auto-Linking Operations
```bash
# البحث عن جميع عمليات الربط التلقائي
grep "[AUTO-LINK]" logs/performance/performance-*.txt

# البحث عن عمليات الربط التلقائي الناجحة
grep "[AUTO-LINK].*Success: True" logs/performance/performance-*.txt

# البحث عن عمليات الربط التلقائي الفاشلة
grep "[AUTO-LINK].*Success: False" logs/performance/performance-*.txt
```

### البحث عن Database Queries
```bash
# البحث عن جميع الاستعلامات
grep "[QUERY]" logs/performance/performance-*.txt

# البحث عن الاستعلامات البطيئة (> 1000ms)
grep "SLOW QUERY" logs/performance/performance-*.txt

# البحث عن استعلامات معينة
grep "GetUnallocatedReceiptsAsync" logs/performance/performance-*.txt
```

### البحث عن Allocation Operations
```bash
# البحث عن جميع عمليات التخصيص
grep "[ALLOCATION]" logs/performance/performance-*.txt

# البحث عن عمليات التخصيص لفاتورة معينة
grep "InvoiceId: 18" logs/performance/performance-*.txt
```

## 📝 Format الـ Log Messages

### Auto-Link Performance
```
[PERFORMANCE] [AUTO-LINK] Operation: AutoLinkReceiptsToInvoice | EntityType: Invoice | EntityId: 18 | Elapsed: 245ms | Success: True | Timestamp: 2025-01-15 14:30:25.123 | Details: InvoiceTotal: 150.00 SAR | HotelId: 20 | ReservationId: 1956
```

### Query Performance
```
[PERFORMANCE] [QUERY] QueryName: GetUnallocatedReceiptsAsync | Elapsed: 156ms | Records: 3 | Timestamp: 2025-01-15 14:30:25.234 | Details: InvoiceId: 18 | HotelId: 20 | ReservationId: 1956 | CustomerId: 1958
```

### Allocation Performance
```
[PERFORMANCE] [ALLOCATION] Operation: LinkReceiptsToInvoice | InvoiceId: 18 | ReceiptId: 30 | Amount: 150.00 SAR | Elapsed: 89ms | Success: True | Timestamp: 2025-01-15 14:30:25.345 | Details: LinkedReceipts: 30 | TotalAmount: 150.00 SAR
```

## ⚠️ علامات التحذير

### Slow Query Warning
- **> 1000ms**: `⚠️ SLOW QUERY`
- **> 500ms**: `⚠️ MODERATE QUERY`

### Slow Endpoint Warning
- **> 2000ms**: `⚠️ SLOW ENDPOINT`
- **> 1000ms**: `⚠️ MODERATE ENDPOINT`

## 📊 أمثلة على الاستخدام

### مثال 1: مراقبة أداء Auto-Linking
```bash
# البحث عن جميع عمليات الربط التلقائي في آخر 7 أيام
grep "[AUTO-LINK]" logs/performance/performance-*.txt | tail -100

# حساب متوسط وقت الربط التلقائي
grep "[AUTO-LINK]" logs/performance/performance-*.txt | grep -oP "Elapsed: \K\d+" | awk '{sum+=$1; count++} END {print "Average: " sum/count "ms"}'
```

### مثال 2: مراقبة أداء الاستعلامات
```bash
# البحث عن الاستعلامات البطيئة
grep "SLOW QUERY" logs/performance/performance-*.txt

# البحث عن استعلامات معينة
grep "GetUnallocatedReceiptsAsync" logs/performance/performance-*.txt | grep -oP "Elapsed: \K\d+" | sort -n
```

### مثال 3: مراقبة عمليات التخصيص
```bash
# البحث عن جميع عمليات التخصيص لفاتورة معينة
grep "InvoiceId: 18" logs/performance/performance-*.txt

# البحث عن عمليات التخصيص الفاشلة
grep "[ALLOCATION].*Success: False" logs/performance/performance-*.txt
```

## 🔧 التكوين

### تحديث إعدادات الـ Logging في `Program.cs`
```csharp
.WriteTo.Logger(lc => lc
    .Filter.ByIncludingOnly(logEvent => logEvent.MessageTemplate.Text.Contains("[PERFORMANCE]"))
    .WriteTo.File(
        path: "logs/performance/performance-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 30, // Keep last 30 days
        fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB per file
        rollOnFileSizeLimit: true
    )
)
```

## 📈 Metrics التي يتم تسجيلها

1. **Auto-Linking Operations**
   - وقت التنفيذ (Elapsed Time)
   - نجاح/فشل العملية
   - نوع الـ Entity (Invoice/Receipt)
   - ID الـ Entity
   - تفاصيل إضافية (المبلغ، HotelId، ReservationId)

2. **Database Queries**
   - وقت التنفيذ
   - عدد السجلات المسترجعة
   - اسم الاستعلام
   - تفاصيل إضافية

3. **Allocation Operations**
   - وقت التنفيذ
   - InvoiceId و ReceiptId
   - المبلغ المخصص
   - نجاح/فشل العملية
   - الاستراتيجية المستخدمة (ExactMatch, FullyPay, SameAmount)

4. **Transactions**
   - وقت التنفيذ
   - عدد السجلات المتأثرة
   - نجاح/فشل العملية

## 🎯 الاستخدام في الإنتاج

1. **مراقبة يومية**: فحص ملف `performance-YYYYMMDD.txt` كل يوم
2. **تحليل الأداء**: البحث عن الاستعلامات والعمليات البطيئة
3. **تحسين الأداء**: استخدام البيانات لتحديد نقاط التحسين
4. **مراقبة الأخطاء**: البحث عن العمليات الفاشلة وتحليل أسبابها

## 📝 ملاحظات مهمة

- جميع الـ timestamps تستخدم `KsaTime.Now` (توقيت السعودية)
- الـ logs تُكتب بشكل تلقائي عند تنفيذ العمليات
- لا حاجة لتعديل الكود لإضافة logging - تم إضافته تلقائياً
- الـ logs منفصلة تماماً عن الـ logs العادية لتسهيل المراقبة
