using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using zaaerIntegration.Models;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
	/// <summary>
	/// Customer ledger service implementation.
	/// تنفيذ خدمة دفتر الأستاذ لحسابات العملاء.
	/// </summary>
	public class CustomerLedgerService : ICustomerLedgerService
	{
		private static readonly ConcurrentDictionary<string, SemaphoreSlim> ReservationLocks = new();

		private static readonly HashSet<string> CreditVoucherCodes = new(StringComparer.OrdinalIgnoreCase)
		{
			"receipt",
			"security_deposit",
			"deposit"
		};

		private static readonly HashSet<string> DebitVoucherCodes = new(StringComparer.OrdinalIgnoreCase)
		{
			"refund",
			"security_deposit_refund",
			"expense"
		};

		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<CustomerLedgerService> _logger;

		public CustomerLedgerService(IUnitOfWork unitOfWork, ILogger<CustomerLedgerService> logger)
		{
			_unitOfWork = unitOfWork;
			_logger = logger;
		}

		public async Task SyncReceiptAsync(PaymentReceipt receipt, CancellationToken cancellationToken = default)
		{
			if (receipt == null)
			{
				throw new ArgumentNullException(nameof(receipt));
			}

			// Ignore ledger when no customer is associated (e.g. pure expense, walk-in orders)
			if (!receipt.CustomerId.HasValue || receipt.CustomerId.Value == 0)
			{
				_logger.LogDebug("Skipping ledger sync for receipt {ReceiptId} because customer_id is null or 0", receipt.ReceiptId);
				return;
			}

			// Ignore ledger when no reservation is associated
			// أي سند لا يوجد له reservation_id أو كانت قيمته null لا يُضاف إلى الجداول
			if (!receipt.ReservationId.HasValue || receipt.ReservationId.Value == 0)
			{
				_logger.LogDebug("Skipping ledger sync for receipt {ReceiptId} because reservation_id is null or 0", receipt.ReceiptId);
				return;
			}

			var matchedReservation = await ResolveReservationAsync(receipt.ReservationId, cancellationToken).ConfigureAwait(false);
			var reservationKeys = ResolveReservationKeys(matchedReservation?.ReservationId, matchedReservation?.ZaaerId, receipt.ReservationId);

			var account = await GetOrCreateAccountAsync(
				receipt.CustomerId.Value,
				receipt.HotelId,
				reservationKeys,
				matchedReservation?.ZaaerId,
				cancellationToken).ConfigureAwait(false);

			var existingTransactions = await _unitOfWork.CustomerTransactions
				.FindAsync(t => t.PaymentReceiptId == receipt.ReceiptId)
				.ConfigureAwait(false);

			var transaction = existingTransactions.FirstOrDefault();
			var (creditAmount, debitAmount) = MapAmounts(receipt);
			var normalizedStatus = NormalizeStatus(receipt.ReceiptStatus);
			var now = KsaTime.Now;

			if (transaction == null)
			{
				transaction = new CustomerTransaction
				{
					AccountId = account.AccountId,
					CustomerId = receipt.CustomerId.Value, // Already checked for null above
					ReservationId = reservationKeys.LedgerReservationId,
					HotelId = receipt.HotelId,
					PaymentReceiptId = receipt.ReceiptId,
					ReceiptNo = receipt.ReceiptNo,
					VoucherCode = receipt.VoucherCode,
					ReceiptType = receipt.ReceiptType,
					ZaaerReceiptId = receipt.ZaaerId,
					TransactionDate = receipt.ReceiptDate,
					TransactionType = ResolveTransactionType(receipt),
					TransactionSource = "PaymentReceipt",
					TransactionStatus = normalizedStatus,
					CreditAmount = creditAmount,
					DebitAmount = debitAmount,
					BalanceAfter = 0, // recalculated later
					PaymentMethod = receipt.PaymentMethod,
					RelatedInvoiceId = receipt.InvoiceId,
					Description = BuildDescription(receipt),
					CreatedBy = receipt.CreatedBy,
					CreatedAt = now
				};

				await _unitOfWork.CustomerTransactions.AddAsync(transaction).ConfigureAwait(false);
			}
			else
			{
				transaction.CustomerId = receipt.CustomerId.Value; // Already checked for null above
				transaction.ReservationId = reservationKeys.LedgerReservationId;
				transaction.HotelId = receipt.HotelId;
				transaction.ReceiptNo = receipt.ReceiptNo;
				transaction.VoucherCode = receipt.VoucherCode;
				transaction.ReceiptType = receipt.ReceiptType;
				transaction.ZaaerReceiptId = receipt.ZaaerId;
				transaction.TransactionDate = receipt.ReceiptDate;
				transaction.TransactionType = ResolveTransactionType(receipt);
				transaction.TransactionStatus = normalizedStatus;
				transaction.CreditAmount = creditAmount;
				transaction.DebitAmount = debitAmount;
				transaction.PaymentMethod = receipt.PaymentMethod;
				transaction.RelatedInvoiceId = receipt.InvoiceId;
				transaction.Description = BuildDescription(receipt);
				transaction.UpdatedAt = now;
			}

			account.LastTransactionAt = receipt.ReceiptDate;
			account.UpdatedAt = now;
			account.Status = "active";

			await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
			await RecalculateAccountAsync(account.AccountId, cancellationToken).ConfigureAwait(false);
			
			// Update reservation amount_paid and balance_amount from customer_accounts
			// تحديث amount_paid و balance_amount في reservations من customer_accounts
			if (receipt.ReservationId.HasValue && receipt.ReservationId.Value > 0)
			{
				await UpdateReservationFromAccountAsync(account, receipt.ReservationId.Value, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Update reservation amount_paid and balance_amount from customer_accounts
		/// تحديث amount_paid و balance_amount في reservations من customer_accounts
		/// 
		/// Performance Optimized: Uses FindSingleAsync for efficient database queries
		/// Optimistic Concurrency: Checks values before update to prevent race conditions
		/// </summary>
		private async Task UpdateReservationFromAccountAsync(CustomerAccount account, int reservationId, CancellationToken cancellationToken)
		{
			try
			{
				// Performance Optimization: Try by zaaer_id first (most common case)
				// تحسين الأداء: البحث أولاً بـ zaaer_id (الحالة الأكثر شيوعاً)
				var reservation = await _unitOfWork.Reservations
					.FindSingleAsync(r => r.ZaaerId == reservationId)
					.ConfigureAwait(false);

				// Fallback to reservation_id if not found by zaaer_id
				// البحث بـ reservation_id إذا لم يتم العثور عليه بـ zaaer_id
				if (reservation == null)
				{
					reservation = await _unitOfWork.Reservations
						.FindSingleAsync(r => r.ReservationId == reservationId)
						.ConfigureAwait(false);
				}

				if (reservation == null)
				{
					_logger.LogDebug("Reservation with ID {ReservationId} not found. Skipping reservation update from account.", reservationId);
					return;
				}

				// Calculate new values
				// حساب القيم الجديدة
				var newAmountPaid = account.TotalCredit;
				var totalAmount = reservation.TotalAmount ?? reservation.Subtotal ?? 0.00M;
				var newBalanceAmount = totalAmount - account.TotalCredit;

				// Optimistic Concurrency: Check if values actually changed
				// Concurrency المتفائلة: التحقق من تغيير القيم فعلياً
				var amountPaidChanged = reservation.AmountPaid == null || 
					Math.Abs(reservation.AmountPaid.Value - newAmountPaid) > 0.01M;
				var balanceAmountChanged = reservation.BalanceAmount == null || 
					Math.Abs(reservation.BalanceAmount.Value - newBalanceAmount) > 0.01M;

				// Only update if there are actual changes (optimistic concurrency check)
				// تحديث فقط إذا كانت هناك تغييرات فعلية (فحص concurrency المتفائلة)
				if (!amountPaidChanged && !balanceAmountChanged)
				{
					_logger.LogDebug("Reservation {ReservationId} values unchanged. Skipping update.", reservation.ReservationId);
					return;
				}

				// Update amount_paid from total_credit (sum of all receipts/credits)
				// تحديث amount_paid من total_credit (مجموع جميع الإيصالات/الإيداعات)
				reservation.AmountPaid = newAmountPaid;

				// Calculate balance_amount = TotalAmount - AmountPaid
				// حساب balance_amount = TotalAmount - AmountPaid
				reservation.BalanceAmount = newBalanceAmount;

				// Save changes (EF Core will handle optimistic concurrency if row_version exists)
				// حفظ التغييرات (EF Core سيتعامل مع optimistic concurrency إذا كان row_version موجوداً)
				await _unitOfWork.Reservations.UpdateAsync(reservation).ConfigureAwait(false);
				await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

				_logger.LogDebug("Updated reservation {ReservationId}: AmountPaid={AmountPaid:N2}, BalanceAmount={BalanceAmount:N2} from account {AccountId}",
					reservation.ReservationId, reservation.AmountPaid, reservation.BalanceAmount, account.AccountId);
			}
			catch (DbUpdateConcurrencyException ex)
			{
				// Handle optimistic concurrency conflict (if row_version is added later)
				// معالجة تعارض optimistic concurrency (إذا تم إضافة row_version لاحقاً)
				_logger.LogWarning(ex, "Concurrency conflict while updating reservation {ReservationId} from account {AccountId}. Reservation was modified by another process.",
					reservationId, account.AccountId);
			}
			catch (Exception ex)
			{
				// Log error but don't throw - reservation update is best-effort
				// تسجيل الخطأ ولكن عدم إلقاء استثناء - تحديث الحجز هو best-effort
				_logger.LogWarning(ex, "Failed to update reservation {ReservationId} from account {AccountId}. Error: {ErrorMessage}",
					reservationId, account.AccountId, ex.Message);
			}
		}

		public async Task CancelReceiptAsync(PaymentReceipt receipt, CancellationToken cancellationToken = default)
		{
			if (receipt == null)
			{
				throw new ArgumentNullException(nameof(receipt));
			}

			var transactions = await _unitOfWork.CustomerTransactions
				.FindAsync(t => t.PaymentReceiptId == receipt.ReceiptId)
				.ConfigureAwait(false);

			var transaction = transactions.FirstOrDefault();
			if (transaction == null)
			{
				_logger.LogDebug("No ledger transaction found to cancel for receipt {ReceiptId}", receipt.ReceiptId);
				return;
			}

			transaction.TransactionStatus = "cancelled";
			transaction.UpdatedAt = KsaTime.Now;
			await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

			await RecalculateAccountAsync(transaction.AccountId, cancellationToken).ConfigureAwait(false);
			
			// Update reservation amount_paid and balance_amount from customer_accounts after cancellation
			// تحديث amount_paid و balance_amount في reservations من customer_accounts بعد الإلغاء
			if (receipt.ReservationId.HasValue && receipt.ReservationId.Value > 0)
			{
				var account = await _unitOfWork.CustomerAccounts.GetByIdAsync(transaction.AccountId).ConfigureAwait(false);
				if (account != null)
				{
					await UpdateReservationFromAccountAsync(account, receipt.ReservationId.Value, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		public async Task SyncReservationAsync(Reservation reservation, CancellationToken cancellationToken = default)
		{
			if (reservation == null)
			{
				throw new ArgumentNullException(nameof(reservation));
			}

			// Ignore ledger when no customer is associated
			// أي حجز لا يوجد له customer_id أو كانت قيمته 0 لا يُضاف إلى الجداول
			if (!reservation.CustomerId.HasValue || reservation.CustomerId.Value == 0)
			{
				_logger.LogDebug("Skipping reservation ledger sync because customer_id is missing. ReservationId={ReservationId}", reservation.ReservationId);
				return;
			}

			// Ignore ledger when no reservation_id is associated
			// أي حجز لا يوجد له reservation_id أو كانت قيمته 0 لا يُضاف إلى الجداول
			if (reservation.ReservationId == 0)
			{
				_logger.LogDebug("Skipping reservation ledger sync because reservation_id is 0. ReservationId={ReservationId}", reservation.ReservationId);
				return;
			}

			var chargeAmount = reservation.TotalAmount ?? reservation.Subtotal ?? 0.00M;
			if (chargeAmount <= 0.00M)
			{
				_logger.LogDebug("Reservation {ReservationId} total amount is 0. Skipping reservation charge transaction.", reservation.ReservationId);
				return;
			}

			var transactionDate = reservation.ReservationDate == default ? KsaTime.Now : reservation.ReservationDate;
			var reservationKeys = ResolveReservationKeys(reservation.ReservationId, reservation.ZaaerId, null);
			var lockKey = BuildReservationLockKey(reservation.CustomerId!.Value, reservation.HotelId, reservationKeys);
			var reservationLock = await AcquireReservationLockAsync(lockKey, cancellationToken).ConfigureAwait(false);

			try
			{
			var account = await GetOrCreateAccountAsync(
				reservation.CustomerId!.Value,
				reservation.HotelId,
				reservationKeys,
				reservation.ZaaerId,
				cancellationToken).ConfigureAwait(false);

				var transaction = await FindReservationTransactionAsync(account.AccountId, reservationKeys).ConfigureAwait(false);
			var now = KsaTime.Now;
			var description = string.IsNullOrWhiteSpace(reservation.ReservationNo)
				? "Reservation charge"
				: $"Charge for {reservation.ReservationNo}";

			if (transaction == null)
			{
				transaction = new CustomerTransaction
				{
					AccountId = account.AccountId,
					CustomerId = reservation.CustomerId!.Value,
					ReservationId = reservationKeys.LedgerReservationId,
					HotelId = reservation.HotelId,
					TransactionDate = transactionDate,
					TransactionType = "reservation_charge",
					TransactionSource = "Reservation",
					TransactionStatus = "posted",
					DebitAmount = chargeAmount,
					CreditAmount = 0.00M,
					Description = description,
					CreatedBy = reservation.CreatedBy,
					CreatedAt = now
				};

				await _unitOfWork.CustomerTransactions.AddAsync(transaction).ConfigureAwait(false);
			}
			else
			{
				transaction.TransactionDate = transactionDate;
				transaction.DebitAmount = chargeAmount;
				transaction.Description = description;
				transaction.ReservationId = reservationKeys.LedgerReservationId;
				transaction.UpdatedAt = now;
			}

			account.LastTransactionAt = transactionDate;
			account.UpdatedAt = now;

			await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

				if (await CleanupDuplicateReservationTransactionsAsync(account.AccountId, reservationKeys).ConfigureAwait(false))
				{
					_logger.LogWarning("Detected duplicate reservation ledger transactions for AccountId={AccountId}, ReservationKey={ReservationKey}. Extra entries removed.", account.AccountId, lockKey);
				}

			await RecalculateAccountAsync(account.AccountId, cancellationToken).ConfigureAwait(false);
			
			// Update reservation amount_paid and balance_amount from customer_accounts
			// تحديث amount_paid و balance_amount في reservations من customer_accounts
			if (reservationKeys.LedgerReservationId.HasValue && reservationKeys.LedgerReservationId.Value > 0)
			{
				await UpdateReservationFromAccountAsync(account, reservationKeys.LedgerReservationId.Value, cancellationToken).ConfigureAwait(false);
			}
			}
			finally
			{
				reservationLock.Dispose();
			}
		}

		private async Task<CustomerAccount> GetOrCreateAccountAsync(
			int customerId,
			int hotelId,
			ReservationKeyInfo reservationKeys,
			int? zaaerId,
			CancellationToken cancellationToken)
		{
			var accounts = await _unitOfWork.CustomerAccounts
				.FindAsync(a => a.CustomerId == customerId && a.HotelId == hotelId)
				.ConfigureAwait(false);

			CustomerAccount? account;
			if (!reservationKeys.LedgerReservationId.HasValue)
			{
				account = accounts.FirstOrDefault(a => !a.ReservationId.HasValue);
			}
			else
			{
				account = accounts.FirstOrDefault(a =>
					a.ReservationId.HasValue && reservationKeys.Contains(a.ReservationId.Value));
			}

			if (account != null)
			{
				var updated = false;
				if (reservationKeys.LedgerReservationId.HasValue && account.ReservationId != reservationKeys.LedgerReservationId)
				{
					account.ReservationId = reservationKeys.LedgerReservationId;
					updated = true;
				}

				if (zaaerId.HasValue && account.ZaaerId != zaaerId)
				{
					account.ZaaerId = zaaerId;
					updated = true;
				}

				if (updated)
				{
					account.UpdatedAt = KsaTime.Now;
					await _unitOfWork.CustomerAccounts.UpdateAsync(account).ConfigureAwait(false);
					await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
				}

				return account;
			}

			account = new CustomerAccount
			{
				CustomerId = customerId,
				ReservationId = reservationKeys.LedgerReservationId,
				HotelId = hotelId,
				CurrencyCode = "SAR",
				CreatedAt = KsaTime.Now,
				UpdatedAt = KsaTime.Now,
				Status = "active",
				ZaaerId = zaaerId
			};

			await _unitOfWork.CustomerAccounts.AddAsync(account).ConfigureAwait(false);
			await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

			return account;
		}

		private static (decimal credit, decimal debit) MapAmounts(PaymentReceipt receipt)
		{
			var amount = Math.Abs(receipt.AmountPaid);
			if (IsDebit(receipt))
			{
				return (0.00M, amount);
			}

			return (amount, 0.00M);
		}

		private static bool IsDebit(PaymentReceipt receipt)
		{
			if (!string.IsNullOrWhiteSpace(receipt.VoucherCode) && DebitVoucherCodes.Contains(receipt.VoucherCode))
			{
				return true;
			}

			if (!string.IsNullOrWhiteSpace(receipt.ReceiptType) && DebitVoucherCodes.Contains(receipt.ReceiptType))
			{
				return true;
			}

			return string.Equals(receipt.ReceiptType, "refund", StringComparison.OrdinalIgnoreCase);
		}

		private static string ResolveTransactionType(PaymentReceipt receipt)
		{
			return IsDebit(receipt) ? "refund" : "receipt";
		}

		private static string BuildDescription(PaymentReceipt receipt)
		{
			if (!string.IsNullOrWhiteSpace(receipt.VoucherCode))
			{
				return receipt.VoucherCode;
			}

			return receipt.ReceiptType ?? "receipt";
		}

		private static string NormalizeStatus(string? status)
		{
			return string.IsNullOrWhiteSpace(status)
				? "active"
				: status.Trim().ToLowerInvariant();
		}

		private async Task<Reservation?> ResolveReservationAsync(int? reservationId, CancellationToken cancellationToken)
		{
			if (!reservationId.HasValue || reservationId.Value <= 0)
			{
				return null;
			}

			var reservations = await _unitOfWork.Reservations
				.FindAsync(r => r.ReservationId == reservationId.Value || r.ZaaerId == reservationId.Value)
				.ConfigureAwait(false);

			return reservations
				.OrderByDescending(r => r.ZaaerId == reservationId.Value)
				.ThenByDescending(r => r.ReservationId == reservationId.Value)
				.FirstOrDefault();
		}

		private static ReservationKeyInfo ResolveReservationKeys(int? localReservationId, int? zaaerReservationId, int? providedReservationId)
		{
			var equivalentIds = new HashSet<int>();

			void AddIfValid(int? value)
			{
				if (value.HasValue && value.Value > 0)
				{
					equivalentIds.Add(value.Value);
				}
			}

			AddIfValid(localReservationId);
			AddIfValid(zaaerReservationId);
			AddIfValid(providedReservationId);

			int? ledgerId = null;
			if (zaaerReservationId.HasValue && zaaerReservationId.Value > 0)
			{
				ledgerId = zaaerReservationId.Value;
			}
			else if (providedReservationId.HasValue && providedReservationId.Value > 0)
			{
				ledgerId = providedReservationId.Value;
			}
			else if (localReservationId.HasValue && localReservationId.Value > 0)
			{
				ledgerId = localReservationId.Value;
			}

			if (ledgerId.HasValue)
			{
				equivalentIds.Add(ledgerId.Value);
			}

			return new ReservationKeyInfo(ledgerId, equivalentIds);
		}

		private sealed class ReservationKeyInfo
		{
			public ReservationKeyInfo(int? ledgerReservationId, HashSet<int> equivalentReservationIds)
			{
				LedgerReservationId = ledgerReservationId;
				EquivalentReservationIds = equivalentReservationIds;
			}

			public int? LedgerReservationId { get; }
			public HashSet<int> EquivalentReservationIds { get; }

			public bool Contains(int reservationId) => EquivalentReservationIds.Contains(reservationId);

			public bool Matches(int? reservationId)
			{
				if (!reservationId.HasValue)
				{
					return !LedgerReservationId.HasValue;
				}

				return EquivalentReservationIds.Contains(reservationId.Value);
			}
		}

		private static bool ShouldExcludeFromBalance(CustomerTransaction transaction)
		{
			if (string.IsNullOrWhiteSpace(transaction.VoucherCode))
			{
				return false;
			}

			// Exclude security deposits and bank transfers from balance calculation
			// استثناء التأمينات والتحويلات البنكية من حساب الرصيد
			return string.Equals(transaction.VoucherCode, "security_deposit", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(transaction.VoucherCode, "security_deposit_refund", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(transaction.VoucherCode, "transfers_to_bank", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(transaction.VoucherCode, "transfer_bank_balance", StringComparison.OrdinalIgnoreCase);
		}

		private async Task RecalculateAccountAsync(int accountId, CancellationToken cancellationToken)
		{
			var account = await _unitOfWork.CustomerAccounts.GetByIdAsync(accountId).ConfigureAwait(false);
			if (account == null)
			{
				_logger.LogWarning("Attempted to recalculate non-existing account {AccountId}", accountId);
				return;
			}

			var transactions = await _unitOfWork.CustomerTransactions
				.FindAsync(t => t.AccountId == accountId)
				.ConfigureAwait(false);

			var ordered = transactions
				.Where(t => !string.Equals(t.TransactionStatus, "cancelled", StringComparison.OrdinalIgnoreCase))
				.OrderBy(t => t.TransactionDate)
				.ThenBy(t => t.TransactionId)
				.ToList();

			decimal runningBalance = 0.00M;
			decimal totalCredit = 0.00M;
			decimal totalDebit = 0.00M;

		foreach (var tx in ordered)
		{
			if (ShouldExcludeFromBalance(tx))
			{
				tx.BalanceAfter = runningBalance;
				continue;
			}

			totalCredit += tx.CreditAmount;
			totalDebit += tx.DebitAmount;
			runningBalance += tx.DebitAmount - tx.CreditAmount;
			tx.BalanceAfter = runningBalance;
		}

			account.TotalCredit = totalCredit;
			account.TotalDebit = totalDebit;
			account.Balance = runningBalance;
			account.LastTransactionAt = ordered.LastOrDefault()?.TransactionDate;
			account.UpdatedAt = KsaTime.Now;

			await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
		}

		private static string BuildReservationLockKey(int customerId, int hotelId, ReservationKeyInfo reservationKeys)
		{
			var reservationComponent = reservationKeys.LedgerReservationId?.ToString() ?? "account";
			return $"{customerId}:{hotelId}:{reservationComponent}";
		}

		private static async Task<IDisposable> AcquireReservationLockAsync(string key, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var semaphore = ReservationLocks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
			await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
			return new ReservationLockReleaser(key, semaphore);
		}

		private async Task<CustomerTransaction?> FindReservationTransactionAsync(int accountId, ReservationKeyInfo reservationKeys)
		{
			var reservationIds = reservationKeys.EquivalentReservationIds.ToArray();
			if (reservationIds.Length == 0)
			{
				return await _unitOfWork.CustomerTransactions.FindSingleAsync(t =>
					t.AccountId == accountId &&
					t.TransactionSource == "Reservation" &&
					t.TransactionType == "reservation_charge" &&
					!t.ReservationId.HasValue).ConfigureAwait(false);
			}

			return await _unitOfWork.CustomerTransactions.FindSingleAsync(t =>
				t.AccountId == accountId &&
				t.TransactionSource == "Reservation" &&
				t.TransactionType == "reservation_charge" &&
				t.ReservationId.HasValue &&
				reservationIds.Contains(t.ReservationId.Value)).ConfigureAwait(false);
		}

		private async Task<bool> CleanupDuplicateReservationTransactionsAsync(int accountId, ReservationKeyInfo reservationKeys)
		{
			var reservationIds = reservationKeys.EquivalentReservationIds.ToArray();
			IEnumerable<CustomerTransaction> transactions;

			if (reservationIds.Length == 0)
			{
				transactions = await _unitOfWork.CustomerTransactions
					.FindAsync(t =>
						t.AccountId == accountId &&
						t.TransactionSource == "Reservation" &&
						t.TransactionType == "reservation_charge" &&
						!t.ReservationId.HasValue)
					.ConfigureAwait(false);
			}
			else
			{
				transactions = await _unitOfWork.CustomerTransactions
					.FindAsync(t =>
						t.AccountId == accountId &&
						t.TransactionSource == "Reservation" &&
						t.TransactionType == "reservation_charge" &&
						t.ReservationId.HasValue &&
						reservationIds.Contains(t.ReservationId.Value))
					.ConfigureAwait(false);
			}

			var duplicates = transactions
				.OrderBy(t => t.CreatedAt)
				.ThenBy(t => t.TransactionId)
				.ToList();

			if (duplicates.Count <= 1)
			{
				return false;
			}

			var keeper = duplicates.First();
			var toRemove = duplicates.Skip(1).ToList();

			foreach (var duplicate in toRemove)
			{
				await _unitOfWork.CustomerTransactions.DeleteAsync(duplicate).ConfigureAwait(false);
			}

			// Ensure keeper is aligned with latest reservation id
			if (!keeper.ReservationId.HasValue && reservationKeys.LedgerReservationId.HasValue)
			{
				keeper.ReservationId = reservationKeys.LedgerReservationId;
			}

			await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
			return true;
		}

		private sealed class ReservationLockReleaser : IDisposable
		{
			private readonly string _key;
			private readonly SemaphoreSlim _semaphore;
			private bool _disposed;

			public ReservationLockReleaser(string key, SemaphoreSlim semaphore)
			{
				_key = key;
				_semaphore = semaphore;
			}

			public void Dispose()
			{
				if (_disposed)
				{
					return;
				}

				_semaphore.Release();
				if (_semaphore.CurrentCount == 1)
				{
					ReservationLocks.TryRemove(_key, out _);
				}

				_disposed = true;
			}
		}
	}
}

