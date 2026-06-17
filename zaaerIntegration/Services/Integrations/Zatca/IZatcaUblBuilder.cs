using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Integrations.Zatca
{
    public sealed class ZatcaUblBuildResult
    {
        public bool Success { get; init; }
        public string? SignedXmlBase64 { get; init; }
        public string? InvoiceHash { get; init; }
        /// <summary>TLV QR from signing (base64), stored on invoice when gateway does not return one.</summary>
        public string? QrCode { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public interface IZatcaUblBuilder
    {
        Task<ZatcaUblBuildResult> BuildAndSignInvoiceAsync(
            Invoice invoice,
            ZatcaDetails seller,
            ZatcaDevice device,
            ZatcaProfileResolution profile,
            string previousHash,
            int icv,
            CancellationToken cancellationToken = default);

        Task<ZatcaUblBuildResult> BuildAndSignCreditNoteAsync(
            CreditNote creditNote,
            Invoice? originalInvoice,
            ZatcaDetails seller,
            ZatcaDevice device,
            ZatcaProfileResolution profile,
            string previousHash,
            int icv,
            CancellationToken cancellationToken = default);

        Task<ZatcaUblBuildResult> BuildAndSignDebitNoteAsync(
            DebitNote debitNote,
            Invoice? originalInvoice,
            ZatcaDetails seller,
            ZatcaDevice device,
            ZatcaProfileResolution profile,
            string previousHash,
            int icv,
            CancellationToken cancellationToken = default);
    }
}
