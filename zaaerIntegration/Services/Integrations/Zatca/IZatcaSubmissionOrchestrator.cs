namespace zaaerIntegration.Services.Integrations.Zatca
{
    public sealed class ZatcaBatchProcessResult
    {
        public int InvoicesProcessed { get; set; }
        public int InvoicesSucceeded { get; set; }
        public int InvoicesFailed { get; set; }
        public int CreditNotesProcessed { get; set; }
        public int CreditNotesSucceeded { get; set; }
        public int CreditNotesFailed { get; set; }
        public int DebitNotesProcessed { get; set; }
        public int DebitNotesSucceeded { get; set; }
        public int DebitNotesFailed { get; set; }

        public void Add(ZatcaBatchProcessResult other)
        {
            InvoicesProcessed += other.InvoicesProcessed;
            InvoicesSucceeded += other.InvoicesSucceeded;
            InvoicesFailed += other.InvoicesFailed;
            CreditNotesProcessed += other.CreditNotesProcessed;
            CreditNotesSucceeded += other.CreditNotesSucceeded;
            CreditNotesFailed += other.CreditNotesFailed;
            DebitNotesProcessed += other.DebitNotesProcessed;
            DebitNotesSucceeded += other.DebitNotesSucceeded;
            DebitNotesFailed += other.DebitNotesFailed;
        }
    }

    public sealed class ZatcaSingleDocumentResult
    {
        public bool Success { get; init; }
        public string? ZatcaStatus { get; init; }
        public string? Message { get; init; }

        public static ZatcaSingleDocumentResult Ok(string? status) =>
            new() { Success = true, ZatcaStatus = status };

        public static ZatcaSingleDocumentResult Fail(string message) =>
            new() { Success = false, Message = message };
    }

    public interface IZatcaSubmissionOrchestrator
    {
        Task<ZatcaBatchProcessResult> ProcessPendingBatchAsync(
            int maxRetries,
            int batchSize,
            CancellationToken cancellationToken = default);

        Task<ZatcaSingleDocumentResult> ProcessInvoiceByIdAsync(
            int invoiceId,
            CancellationToken cancellationToken = default);

        Task<ZatcaSingleDocumentResult> ProcessCreditNoteByIdAsync(
            int creditNoteId,
            CancellationToken cancellationToken = default);

        Task<ZatcaSingleDocumentResult> ProcessDebitNoteByIdAsync(
            int debitNoteId,
            CancellationToken cancellationToken = default);
    }
}
