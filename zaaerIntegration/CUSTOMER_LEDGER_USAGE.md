# 📋 استخدام جداول Customer Ledger
## `customer_accounts` و `customer_transactions`

---

## 🎯 **الهدف**
هذا الملف يوضح **متى وأين** يتم استدعاء الجداول `customer_accounts` و `customer_transactions` في المشروع، وكيف يتم ملؤها بالداتا، وبعد كل إيه.

---

## 📊 **الجداول**

### 1. `customer_accounts` (حسابات العملاء)
- **الوصف**: جدول رئيسي لحسابات العملاء (Customer Ledger)
- **الغرض**: تخزين رصيد كل عميل لكل فندق/حجز
- **الحقول المهمة**: `account_id`, `customer_id`, `hotel_id`, `reservation_id`, `balance`, `total_credit`, `total_debit`

### 2. `customer_transactions` (معاملات العملاء)
- **الوصف**: جدول تفاصيل المعاملات (Transaction Details)
- **الغرض**: تخزين كل معاملة (receipt, refund, reservation charge)
- **الحقول المهمة**: `transaction_id`, `account_id`, `customer_id`, `payment_receipt_id`, `credit_amount`, `debit_amount`, `balance_after`

---

## 🔄 **الخدمة الرئيسية**

### `CustomerLedgerService`
**الموقع**: `Services/Implementations/CustomerLedgerService.cs`

**الوظائف**:
1. `SyncReceiptAsync()` - مزامنة Payment Receipt
2. `SyncReservationAsync()` - مزامنة Reservation
3. `CancelReceiptAsync()` - إلغاء Payment Receipt
4. `GetOrCreateAccountAsync()` - إنشاء/الحصول على حساب
5. `RecalculateAccountAsync()` - إعادة حساب الرصيد

---

## 📍 **أماكن الاستدعاء**

### 1️⃣ **ZaaerPaymentReceiptService** 
**الموقع**: `Services/Zaaer/ZaaerPaymentReceiptService.cs`

#### ✅ **بعد إنشاء Payment Receipt**
```csharp
// السطر: 273
await _customerLedgerService.SyncReceiptAsync(createdPaymentReceipt);
```
**المكان**: `CreatePaymentReceiptAsync()`
**بعد**: إنشاء `PaymentReceipt` في قاعدة البيانات

---

#### ✅ **بعد تحديث Payment Receipt (By ID)**
```csharp
// السطر: 432
await _customerLedgerService.SyncReceiptAsync(existingPaymentReceipt);
```
**المكان**: `UpdatePaymentReceiptAsync()`
**بعد**: تحديث `PaymentReceipt` في قاعدة البيانات

---

#### ✅ **بعد تحديث Payment Receipt (By Receipt No)**
```csharp
// السطر: 598
await _customerLedgerService.SyncReceiptAsync(paymentReceipt);
```
**المكان**: `UpdatePaymentReceiptByReceiptNoAsync()`
**بعد**: تحديث `PaymentReceipt` في قاعدة البيانات

---

#### ✅ **بعد تحديث Payment Receipt (By Zaaer ID)**
```csharp
// السطر: 912
await _customerLedgerService.SyncReceiptAsync(paymentReceipt);
```
**المكان**: `UpdatePaymentReceiptByZaaerIdAsync()`
**بعد**: تحديث `PaymentReceipt` في قاعدة البيانات

---

#### ❌ **بعد إلغاء Payment Receipt (By ID)**
```csharp
// السطر: 975
await _customerLedgerService.CancelReceiptAsync(paymentReceipt);
```
**المكان**: `CancelPaymentReceiptAsync()`
**بعد**: تحديث `ReceiptStatus = "cancelled"` في قاعدة البيانات

---

#### ❌ **بعد إلغاء Payment Receipt (By Receipt No)**
```csharp
// السطر: 994
await _customerLedgerService.CancelReceiptAsync(paymentReceipt);
```
**المكان**: `CancelPaymentReceiptByReceiptNoAsync()`
**بعد**: تحديث `ReceiptStatus = "cancelled"` في قاعدة البيانات

---

#### ❌ **بعد إلغاء Payment Receipt (By Zaaer ID)**
```csharp
// السطر: 1011
await _customerLedgerService.CancelReceiptAsync(paymentReceipt);
```
**المكان**: `CancelPaymentReceiptByZaaerIdAsync()`
**بعد**: تحديث `ReceiptStatus = "cancelled"` في قاعدة البيانات

---

### 2️⃣ **ZaaerReservationService**
**الموقع**: `Services/Zaaer/ZaaerReservationService.cs`

#### ✅ **بعد إنشاء Reservation**
```csharp
// السطر: 277
await _customerLedgerService.SyncReservationAsync(createdReservation);
```
**المكان**: `CreateReservationAsync()`
**بعد**: إنشاء `Reservation` في قاعدة البيانات

---

#### ✅ **بعد تحديث Reservation (By ID)**
```csharp
// السطر: 406
await _customerLedgerService.SyncReservationAsync(existingReservation);
```
**المكان**: `UpdateReservationAsync()`
**بعد**: تحديث `Reservation` في قاعدة البيانات

---

#### ✅ **بعد تحديث Reservation (By Number)**
```csharp
// السطر: 543
await _customerLedgerService.SyncReservationAsync(reservation);
```
**المكان**: `UpdateReservationByNumberAsync()`
**بعد**: تحديث `Reservation` في قاعدة البيانات

---

#### ✅ **بعد تحديث Reservation (By Zaaer ID)**
```csharp
// السطر: 679
await _customerLedgerService.SyncReservationAsync(reservation);
```
**المكان**: `UpdateReservationByZaaerIdAsync()`
**بعد**: تحديث `Reservation` في قاعدة البيانات

---

### 3️⃣ **ZaaerGenericHandlers** (Queue Handlers)
**الموقع**: `Services/PartnerQueue/Handlers/ZaaerGenericHandlers.cs`

#### ✅ **في Queue Handlers**
يتم إنشاء `CustomerLedgerService` داخل الـ handlers عند معالجة:
- `ZaaerPaymentReceiptCreateHandler`
- `ZaaerPaymentReceiptUpdateByIdHandler`
- `ZaaerPaymentReceiptUpdateByReceiptNoHandler`
- `ZaaerPaymentReceiptUpdateByZaaerIdHandler`
- `ZaaerReservationCreateHandler`
- `ZaaerReservationUpdateByIdHandler`
- `ZaaerReservationUpdateByNumberHandler`
- `ZaaerReservationUpdateByZaaerIdHandler`

---

## 🔧 **كيف يتم ملء الجداول**

### 📝 **خطوات `SyncReceiptAsync()`**

1. **التحقق من CustomerId**
   ```csharp
   if (!receipt.CustomerId.HasValue || receipt.CustomerId.Value == 0)
       return; // لا يتم إنشاء ledger entry
   ```

2. **الحصول على/إنشاء CustomerAccount**
   ```csharp
   var account = await GetOrCreateAccountAsync(...);
   ```
   - البحث عن حساب موجود (`customer_id`, `hotel_id`, `reservation_id`)
   - إذا لم يوجد، إنشاء حساب جديد في `customer_accounts`

3. **البحث عن Transaction موجود**
   ```csharp
   var existingTransactions = await _unitOfWork.CustomerTransactions
       .FindAsync(t => t.PaymentReceiptId == receipt.ReceiptId);
   ```

4. **إنشاء/تحديث CustomerTransaction**
   - إذا لم يوجد: إنشاء `CustomerTransaction` جديد في `customer_transactions`
   - إذا وجد: تحديث `CustomerTransaction` الموجود

5. **تحديث CustomerAccount**
   ```csharp
   account.LastTransactionAt = receipt.ReceiptDate;
   account.UpdatedAt = now;
   account.Status = "active";
   ```

6. **حفظ التغييرات**
   ```csharp
   await _unitOfWork.SaveChangesAsync();
   ```

7. **إعادة حساب الرصيد**
   ```csharp
   await RecalculateAccountAsync(account.AccountId);
   ```
   - حساب `TotalCredit`, `TotalDebit`, `Balance`
   - تحديث `BalanceAfter` لكل transaction

---

### 📝 **خطوات `SyncReservationAsync()`**

1. **التحقق من CustomerId**
   ```csharp
   if (reservation.CustomerId == 0)
       return; // لا يتم إنشاء ledger entry
   ```

2. **التحقق من TotalAmount**
   ```csharp
   var chargeAmount = reservation.TotalAmount ?? reservation.Subtotal ?? 0.00M;
   if (chargeAmount <= 0.00M)
       return; // لا يتم إنشاء ledger entry
   ```

3. **الحصول على Lock** (لتجنب Race Conditions)
   ```csharp
   var reservationLock = await AcquireReservationLockAsync(lockKey);
   ```

4. **الحصول على/إنشاء CustomerAccount**
   ```csharp
   var account = await GetOrCreateAccountAsync(...);
   ```

5. **البحث عن Reservation Transaction موجود**
   ```csharp
   var transaction = await FindReservationTransactionAsync(account.AccountId, reservationKeys);
   ```

6. **إنشاء/تحديث CustomerTransaction**
   - إذا لم يوجد: إنشاء `CustomerTransaction` جديد (`TransactionType = "reservation_charge"`, `DebitAmount = chargeAmount`)
   - إذا وجد: تحديث `CustomerTransaction` الموجود

7. **تنظيف Duplicate Transactions**
   ```csharp
   await CleanupDuplicateReservationTransactionsAsync(account.AccountId, reservationKeys);
   ```

8. **حفظ التغييرات**
   ```csharp
   await _unitOfWork.SaveChangesAsync();
   ```

9. **إعادة حساب الرصيد**
   ```csharp
   await RecalculateAccountAsync(account.AccountId);
   ```

---

### 📝 **خطوات `CancelReceiptAsync()`**

1. **البحث عن Transaction**
   ```csharp
   var transactions = await _unitOfWork.CustomerTransactions
       .FindAsync(t => t.PaymentReceiptId == receipt.ReceiptId);
   ```

2. **تحديث Transaction Status**
   ```csharp
   transaction.TransactionStatus = "cancelled";
   transaction.UpdatedAt = KsaTime.Now;
   ```

3. **حفظ التغييرات**
   ```csharp
   await _unitOfWork.SaveChangesAsync();
   ```

4. **إعادة حساب الرصيد**
   ```csharp
   await RecalculateAccountAsync(transaction.AccountId);
   ```
   - Transactions بـ `status = "cancelled"` لا تُحسب في الرصيد

---

### 📝 **خطوات `RecalculateAccountAsync()`**

1. **الحصول على Account**
   ```csharp
   var account = await _unitOfWork.CustomerAccounts.GetByIdAsync(accountId);
   ```

2. **الحصول على جميع Transactions**
   ```csharp
   var transactions = await _unitOfWork.CustomerTransactions
       .FindAsync(t => t.AccountId == accountId);
   ```

3. **تصفية Transactions**
   ```csharp
   var ordered = transactions
       .Where(t => t.TransactionStatus != "cancelled")
       .OrderBy(t => t.TransactionDate)
       .ThenBy(t => t.TransactionId)
       .ToList();
   ```

4. **حساب الرصيد**
   ```csharp
   decimal runningBalance = 0.00M;
   decimal totalCredit = 0.00M;
   decimal totalDebit = 0.00M;
   
   foreach (var tx in ordered)
   {
       if (ShouldExcludeFromBalance(tx)) // security_deposit transactions
           continue;
       
       totalCredit += tx.CreditAmount;
       totalDebit += tx.DebitAmount;
       runningBalance += tx.DebitAmount - tx.CreditAmount;
       tx.BalanceAfter = runningBalance;
   }
   ```

5. **تحديث Account**
   ```csharp
   account.TotalCredit = totalCredit;
   account.TotalDebit = totalDebit;
   account.Balance = runningBalance;
   account.LastTransactionAt = ordered.LastOrDefault()?.TransactionDate;
   account.UpdatedAt = KsaTime.Now;
   ```

6. **حفظ التغييرات**
   ```csharp
   await _unitOfWork.SaveChangesAsync();
   ```

---

## 📋 **ملخص سريع**

| الحدث | المكان | الدالة | بعد إيه |
|------|--------|--------|---------|
| ✅ إنشاء Payment Receipt | `ZaaerPaymentReceiptService.CreatePaymentReceiptAsync()` | `SyncReceiptAsync()` | بعد `SaveChangesAsync()` |
| ✅ تحديث Payment Receipt | `ZaaerPaymentReceiptService.UpdatePaymentReceiptAsync()` | `SyncReceiptAsync()` | بعد `SaveChangesAsync()` |
| ✅ تحديث Payment Receipt (By Receipt No) | `ZaaerPaymentReceiptService.UpdatePaymentReceiptByReceiptNoAsync()` | `SyncReceiptAsync()` | بعد `SaveChangesAsync()` |
| ✅ تحديث Payment Receipt (By Zaaer ID) | `ZaaerPaymentReceiptService.UpdatePaymentReceiptByZaaerIdAsync()` | `SyncReceiptAsync()` | بعد `SaveChangesAsync()` |
| ❌ إلغاء Payment Receipt | `ZaaerPaymentReceiptService.CancelPaymentReceiptAsync()` | `CancelReceiptAsync()` | بعد تحديث `ReceiptStatus` |
| ✅ إنشاء Reservation | `ZaaerReservationService.CreateReservationAsync()` | `SyncReservationAsync()` | بعد `SaveChangesAsync()` |
| ✅ تحديث Reservation | `ZaaerReservationService.UpdateReservationAsync()` | `SyncReservationAsync()` | بعد `SaveChangesAsync()` |
| ✅ تحديث Reservation (By Number) | `ZaaerReservationService.UpdateReservationByNumberAsync()` | `SyncReservationAsync()` | بعد `SaveChangesAsync()` |
| ✅ تحديث Reservation (By Zaaer ID) | `ZaaerReservationService.UpdateReservationByZaaerIdAsync()` | `SyncReservationAsync()` | بعد `SaveChangesAsync()` |

---

## ⚠️ **ملاحظات مهمة**

1. **لا يتم إنشاء Ledger Entry إذا**:
   - `CustomerId` = `null` أو `0`
   - `TotalAmount` = `0` (للـ Reservations)

2. **Voucher Codes**:
   - **Credit** (إيداع): `"receipt"`, `"security_deposit"`, `"deposit"`
   - **Debit** (سحب): `"refund"`, `"security_deposit_refund"`, `"expense"`

3. **Transactions Excluded from Balance** (لا تُحسب في الرصيد):
   - **Security Deposits**: `"security_deposit"`, `"security_deposit_refund"`
   - **Bank Transfers**: `"transfers_to_bank"`, `"transfer_bank_balance"`
   - يتم تسجيلها فقط في `customer_transactions` ولكن لا تُحسب في `Balance` في `customer_accounts`
   - يتم استخدام `ShouldExcludeFromBalance()` للتحقق من هذه المعاملات

4. **Cancelled Transactions**:
   - `TransactionStatus = "cancelled"`
   - لا تُحسب في الرصيد

5. **Reservation Locking**:
   - يتم استخدام `SemaphoreSlim` لتجنب Race Conditions عند معالجة Reservations

---

## 🔗 **الملفات ذات الصلة**

- `Services/Implementations/CustomerLedgerService.cs` - الخدمة الرئيسية
- `Services/Interfaces/ICustomerLedgerService.cs` - Interface
- `Services/Zaaer/ZaaerPaymentReceiptService.cs` - استدعاءات Payment Receipt
- `Services/Zaaer/ZaaerReservationService.cs` - استدعاءات Reservation
- `Models/CustomerAccount.cs` - Model
- `Models/CustomerTransaction.cs` - Model
- `Repositories/Interfaces/IUnitOfWork.cs` - Repositories
- `Data/ApplicationDbContext.cs` - DbContext

---

**آخر تحديث**: 2026-01-01

