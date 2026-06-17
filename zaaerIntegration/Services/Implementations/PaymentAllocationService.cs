using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
	/// <summary>
	/// Service for managing payment allocation between invoices and receipts
	/// خدمة إدارة تخصيص المدفوعات بين الفواتير والسندات
	/// </summary>
	public class PaymentAllocationService : IPaymentAllocationService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ApplicationDbContext _context;
		private readonly INumberingService _numberingService;
		private readonly ILogger<PaymentAllocationService>? _logger;

		public PaymentAllocationService(
			IUnitOfWork unitOfWork,
			ApplicationDbContext context,
			INumberingService numberingService,
			ILogger<PaymentAllocationService>? logger = null)
		{
			_unitOfWork = unitOfWork;
			_context = context;
			_numberingService = numberingService;
			_logger = logger;
		}

		/// <summary>
		/// Link existing unallocated receipts to an existing invoice
		/// </summary>
		public async Task LinkReceiptsToInvoiceAsync(int invoiceId, List<int> receiptIds, int? createdBy = null)
		{
			var auditIds = new List<long>();
			await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Get invoice
				var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
				if (invoice == null)
					throw new InvalidOperationException($"Invoice with ID {invoiceId} not found.");

				// Get receipts
				var receipts = new List<PaymentReceipt>();
				foreach (var receiptId in receiptIds)
				{
					var receipt = await _unitOfWork.PaymentReceipts.GetByIdAsync(receiptId);
					if (receipt == null)
						throw new InvalidOperationException($"Receipt with ID {receiptId} not found.");

					// Check if receipt has unallocated amount
					var unallocated = receipt.UnallocatedAmount ?? receipt.AmountPaid;
					if (unallocated <= 0)
						throw new InvalidOperationException($"Receipt {receipt.ReceiptNo} has no unallocated amount.");

					receipts.Add(receipt);
				}

				// Calculate total unallocated amount
				var totalUnallocated = receipts.Sum(r => r.UnallocatedAmount ?? r.AmountPaid);
				var invoiceRemaining = invoice.AmountRemaining ?? (invoice.TotalAmount ?? 0) - invoice.AmountPaid;

				// Allocate receipts to invoice
				foreach (var receipt in receipts)
				{
					var unallocated = receipt.UnallocatedAmount ?? receipt.AmountPaid;
					var amountToAllocate = Math.Min(unallocated, invoiceRemaining);

					if (amountToAllocate > 0)
					{
						// Create mapping
						var mapping = new InvoiceReceiptMapping
						{
							InvoiceId = invoiceId,
							ReceiptId = receipt.ReceiptId,
							AllocatedAmount = amountToAllocate,
							MappingDate = KsaTime.Now,
							CreatedBy = createdBy,
							CreatedAt = KsaTime.Now
						};

						await _unitOfWork.InvoiceReceiptMappings.AddAsync(mapping);

						// Update receipt allocation
						receipt.AllocatedAmount += amountToAllocate;
						receipt.UnallocatedAmount = receipt.AmountPaid - receipt.AllocatedAmount;
						receipt.IsFullyAllocated = receipt.UnallocatedAmount <= 0;

						// Update invoice payment
						invoice.AmountPaid += amountToAllocate;
						invoiceRemaining -= amountToAllocate;

						// For backward compatibility, set invoice_id if not set
						if (!receipt.InvoiceId.HasValue)
						{
							receipt.InvoiceId = invoiceId;
						}
					}
				}

				// Update invoice payment status
				await UpdateInvoicePaymentStatusAsync(invoiceId);

				await _unitOfWork.SaveChangesAsync();
				await _unitOfWork.CommitTransactionAsync();

				_logger?.LogInformation("Linked {Count} receipts to invoice {InvoiceId}", receipts.Count, invoiceId);
			}
			catch
			{
				await _unitOfWork.RollbackTransactionAsync();
				throw;
			}
		}

		/// <summary>
		/// Create a new receipt and link it to an invoice automatically
		/// </summary>
		public async Task<PaymentReceipt> AddPaymentToInvoiceAsync(
			int invoiceId,
			decimal amount,
			string paymentMethod,
			int? paymentMethodId = null,
			int? createdBy = null,
			string? notes = null)
		{
			var auditIds = new List<long>();
			await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Get invoice
				var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
				if (invoice == null)
					throw new InvalidOperationException($"Invoice with ID {invoiceId} not found.");

				var identity = await _numberingService.GetNextBusinessIdentityAsync(
					"payment_receipt",
					invoice.HotelId,
					createdBy?.ToString(),
					$"payment_receipt:{invoice.HotelId}:{invoiceId}:{Guid.NewGuid():N}");
				auditIds.Add(identity.AuditId);

				// Create receipt
				var receipt = new PaymentReceipt
				{
					ReceiptNo = identity.DocumentNo,
					ZaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId),
					HotelId = invoice.HotelId,
					ReservationId = invoice.ReservationId,
					UnitId = invoice.UnitId,
					InvoiceId = invoiceId, // For backward compatibility
					CustomerId = invoice.CustomerId,
					ReceiptDate = KsaTime.Now,
					AmountPaid = amount,
					PaymentMethod = paymentMethod,
					PaymentMethodId = paymentMethodId,
					Notes = notes,
					ReceiptStatus = "active",
					ReceiptType = "receipt",
					CreatedBy = createdBy,
					CreatedAt = KsaTime.Now,
					AllocatedAmount = amount,
					UnallocatedAmount = 0,
					IsFullyAllocated = true
				};

				await _unitOfWork.PaymentReceipts.AddAsync(receipt);
				await _unitOfWork.SaveChangesAsync(); // Save to get receipt ID

				// Create mapping
				var mapping = new InvoiceReceiptMapping
				{
					InvoiceId = invoiceId,
					ReceiptId = receipt.ReceiptId,
					AllocatedAmount = amount,
					MappingDate = KsaTime.Now,
					CreatedBy = createdBy,
					CreatedAt = KsaTime.Now
				};

				await _unitOfWork.InvoiceReceiptMappings.AddAsync(mapping);

				// Update invoice
				invoice.AmountPaid += amount;
				await UpdateInvoicePaymentStatusAsync(invoiceId);

				await _unitOfWork.SaveChangesAsync();
				await _unitOfWork.CommitTransactionAsync();
				foreach (var auditId in auditIds)
					await _numberingService.MarkCommittedAsync(auditId);

				_logger?.LogInformation("Created receipt {ReceiptNo} and linked to invoice {InvoiceId}", receipt.ReceiptNo, invoiceId);

				return receipt;
			}
			catch
			{
				foreach (var auditId in auditIds)
					await _numberingService.MarkVoidedAsync(auditId, "Payment allocation receipt creation failed.");
				await _unitOfWork.RollbackTransactionAsync();
				throw;
			}
		}

		/// <summary>
		/// Create invoice and link it to multiple existing receipts
		/// </summary>
		public async Task<Invoice> CreateInvoiceWithReceiptsAsync(
			Invoice invoiceData,
			List<int> receiptIds,
			int? createdBy = null)
		{
			await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Create invoice
				invoiceData.CreatedAt = KsaTime.Now;
				invoiceData.CreatedBy = createdBy;
				await _unitOfWork.Invoices.AddAsync(invoiceData);
				await _unitOfWork.SaveChangesAsync(); // Save to get invoice ID

				// Link receipts
				if (receiptIds.Any())
				{
					await LinkReceiptsToInvoiceAsync(invoiceData.InvoiceId, receiptIds, createdBy);
				}

				await _unitOfWork.CommitTransactionAsync();

				_logger?.LogInformation("Created invoice {InvoiceNo} with {Count} receipts", invoiceData.InvoiceNo, receiptIds.Count);

				return invoiceData;
			}
			catch
			{
				await _unitOfWork.RollbackTransactionAsync();
				throw;
			}
		}

		/// <summary>
		/// Get receipts that have unallocated amount > 0
		/// Optimized query with proper filtering at database level
		/// </summary>
		public async Task<List<PaymentReceipt>> GetUnallocatedReceiptsAsync(int? reservationId = null, int? hotelId = null, int? customerId = null)
		{
			var query = _context.PaymentReceipts
				.Where(r => r.ReceiptStatus == "active" && 
				           (r.UnallocatedAmount ?? r.AmountPaid) > 0);

			if (reservationId.HasValue)
				query = query.Where(r => r.ReservationId == reservationId.Value);

			if (hotelId.HasValue)
				query = query.Where(r => r.HotelId == hotelId.Value);

			if (customerId.HasValue && customerId.Value > 0)
				query = query.Where(r => r.CustomerId == customerId.Value);

			// Order by receipt date (oldest first) for consistent linking
			var receipts = await query
				.OrderBy(r => r.ReceiptDate)
				.ToListAsync();

			return receipts;
		}

		/// <summary>
		/// Get invoice with all linked receipts and allocation details
		/// </summary>
		public async Task<Invoice?> GetInvoiceWithReceiptsAsync(int invoiceId)
		{
			var invoice = await _context.Invoices
				.Include(i => i.InvoiceReceiptMappings)
					.ThenInclude(m => m.PaymentReceipt)
				.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

			return invoice;
		}

		/// <summary>
		/// Allocate specific amount from a receipt to an invoice
		/// </summary>
		public async Task AllocateReceiptAmountToInvoiceAsync(
			int receiptId,
			int invoiceId,
			decimal amount,
			int? createdBy = null)
		{
			await _unitOfWork.BeginTransactionAsync();
			try
			{
				// Get receipt
				var receipt = await _unitOfWork.PaymentReceipts.GetByIdAsync(receiptId);
				if (receipt == null)
					throw new InvalidOperationException($"Receipt with ID {receiptId} not found.");

				// Get invoice
				var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
				if (invoice == null)
					throw new InvalidOperationException($"Invoice with ID {invoiceId} not found.");

				// Check available amount
				var unallocated = receipt.UnallocatedAmount ?? receipt.AmountPaid;
				if (amount > unallocated)
					throw new InvalidOperationException($"Cannot allocate {amount}. Available unallocated amount: {unallocated}");

				// Check if mapping already exists
				var existingMapping = await _context.InvoiceReceiptMappings
					.FirstOrDefaultAsync(m => m.InvoiceId == invoiceId && m.ReceiptId == receiptId);

				if (existingMapping != null)
				{
					// Update existing mapping
					existingMapping.AllocatedAmount += amount;
					existingMapping.MappingDate = KsaTime.Now;
				}
				else
				{
					// Create new mapping
					var mapping = new InvoiceReceiptMapping
					{
						InvoiceId = invoiceId,
						ReceiptId = receiptId,
						AllocatedAmount = amount,
						MappingDate = KsaTime.Now,
						CreatedBy = createdBy,
						CreatedAt = KsaTime.Now
					};
					await _unitOfWork.InvoiceReceiptMappings.AddAsync(mapping);
				}

				// Update receipt
				receipt.AllocatedAmount += amount;
				receipt.UnallocatedAmount = receipt.AmountPaid - receipt.AllocatedAmount;
				receipt.IsFullyAllocated = receipt.UnallocatedAmount <= 0;

				// Update invoice
				invoice.AmountPaid += amount;
				await UpdateInvoicePaymentStatusAsync(invoiceId);

				await _unitOfWork.SaveChangesAsync();
				await _unitOfWork.CommitTransactionAsync();

				_logger?.LogInformation("Allocated {Amount} from receipt {ReceiptId} to invoice {InvoiceId}", amount, receiptId, invoiceId);
			}
			catch
			{
				await _unitOfWork.RollbackTransactionAsync();
				throw;
			}
		}

		/// <summary>
		/// Remove allocation between receipt and invoice
		/// </summary>
		public async Task RemoveAllocationAsync(int mappingId, int? createdBy = null)
		{
			await _unitOfWork.BeginTransactionAsync();
			try
			{
				var mapping = await _unitOfWork.InvoiceReceiptMappings.GetByIdAsync(mappingId);
				if (mapping == null)
					throw new InvalidOperationException($"Mapping with ID {mappingId} not found.");

				var receipt = await _unitOfWork.PaymentReceipts.GetByIdAsync(mapping.ReceiptId);
				var invoice = await _unitOfWork.Invoices.GetByIdAsync(mapping.InvoiceId);

				if (receipt != null)
				{
					receipt.AllocatedAmount -= mapping.AllocatedAmount;
					receipt.UnallocatedAmount = receipt.AmountPaid - receipt.AllocatedAmount;
					receipt.IsFullyAllocated = receipt.UnallocatedAmount <= 0;
				}

				if (invoice != null)
				{
					invoice.AmountPaid -= mapping.AllocatedAmount;
					await UpdateInvoicePaymentStatusAsync(invoice.InvoiceId);
				}

				await _unitOfWork.InvoiceReceiptMappings.DeleteAsync(mapping);
				await _unitOfWork.SaveChangesAsync();
				await _unitOfWork.CommitTransactionAsync();

				_logger?.LogInformation("Removed allocation mapping {MappingId}", mappingId);
			}
			catch
			{
				await _unitOfWork.RollbackTransactionAsync();
				throw;
			}
		}

		/// <summary>
		/// Update invoice payment status based on allocated amounts
		/// </summary>
		public async Task UpdateInvoicePaymentStatusAsync(int invoiceId)
		{
			var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
			if (invoice == null) return;

			var totalAmount = invoice.TotalAmount ?? 0;
			var amountPaid = invoice.AmountPaid;

			if (amountPaid <= 0)
			{
				invoice.PaymentStatus = "unpaid";
			}
			else if (amountPaid >= totalAmount)
			{
				invoice.PaymentStatus = "paid";
			}
			else
			{
				invoice.PaymentStatus = "partially_paid";
			}

			invoice.AmountRemaining = totalAmount - amountPaid;
		}

		/// <summary>
		/// Generate unique receipt number
		/// </summary>
		private async Task<string> GenerateReceiptNumberAsync(int hotelId)
		{
			var lastReceipt = await _context.PaymentReceipts
				.Where(r => r.HotelId == hotelId && r.ReceiptNo.StartsWith("REC"))
				.OrderByDescending(r => r.ReceiptId)
				.FirstOrDefaultAsync();

			if (lastReceipt == null)
				return "REC0001";

			// Extract number from receipt number (e.g., REC0001 -> 1)
			var numberPart = lastReceipt.ReceiptNo.Replace("REC", "");
			if (int.TryParse(numberPart, out int lastNumber))
			{
				return $"REC{(lastNumber + 1):D4}";
			}

			return $"REC{KsaTime.Now:yyyyMMddHHmmss}";
		}
	}
}
