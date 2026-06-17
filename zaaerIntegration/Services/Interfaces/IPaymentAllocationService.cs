using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Interfaces
{
	/// <summary>
	/// Service interface for managing payment allocation between invoices and receipts
	/// واجهة خدمة إدارة تخصيص المدفوعات بين الفواتير والسندات
	/// </summary>
	public interface IPaymentAllocationService
	{
		/// <summary>
		/// Link existing unallocated receipts to an existing invoice
		/// ربط سندات غير مخصصة بفاتورة موجودة
		/// </summary>
		Task LinkReceiptsToInvoiceAsync(int invoiceId, List<int> receiptIds, int? createdBy = null);

		/// <summary>
		/// Create a new receipt and link it to an invoice automatically
		/// إنشاء سند جديد وربطه بفاتورة تلقائياً
		/// </summary>
		Task<PaymentReceipt> AddPaymentToInvoiceAsync(int invoiceId, decimal amount, string paymentMethod, int? paymentMethodId = null, int? createdBy = null, string? notes = null);

		/// <summary>
		/// Create invoice and link it to multiple existing receipts
		/// إنشاء فاتورة وربطها بعدة سندات موجودة
		/// </summary>
		Task<Invoice> CreateInvoiceWithReceiptsAsync(Invoice invoiceData, List<int> receiptIds, int? createdBy = null);

		/// <summary>
		/// Get receipts that have unallocated amount > 0 for a reservation
		/// الحصول على السندات التي لديها مبلغ غير مخصص > 0 لحجز معين
		/// </summary>
		Task<List<PaymentReceipt>> GetUnallocatedReceiptsAsync(int? reservationId = null, int? hotelId = null, int? customerId = null);

		/// <summary>
		/// Get invoice with all linked receipts and allocation details
		/// الحصول على فاتورة مع جميع السندات المرتبطة وتفاصيل التخصيص
		/// </summary>
		Task<Invoice?> GetInvoiceWithReceiptsAsync(int invoiceId);

		/// <summary>
		/// Allocate specific amount from a receipt to an invoice
		/// تخصيص مبلغ محدد من سند لفاتورة
		/// </summary>
		Task AllocateReceiptAmountToInvoiceAsync(int receiptId, int invoiceId, decimal amount, int? createdBy = null);

		/// <summary>
		/// Remove allocation between receipt and invoice
		/// إزالة التخصيص بين سند وفاتورة
		/// </summary>
		Task RemoveAllocationAsync(int mappingId, int? createdBy = null);

		/// <summary>
		/// Update invoice payment status based on allocated amounts
		/// تحديث حالة دفع الفاتورة بناءً على المبالغ المخصصة
		/// </summary>
		Task UpdateInvoicePaymentStatusAsync(int invoiceId);
	}
}
