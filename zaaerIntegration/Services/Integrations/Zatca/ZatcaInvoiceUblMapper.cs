using FinanceLedgerAPI.Models;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  internal static class ZatcaInvoiceUblMapper
  {
    public static Dictionary<string, object> MapInvoice(
      Invoice invoice,
      ZatcaDetails seller,
      ZatcaProfileResolution profile,
      Dictionary<string, object>? customerParty,
      string previousHash,
      int icv)
    {
      var isSimplified = string.Equals(
        profile.Profile,
        ZatcaApiConstants.ProfileSimplified,
        StringComparison.OrdinalIgnoreCase);

      var amounts = ZatcaUblDocumentBuilder.ResolveHotelAmounts(
        invoice.Subtotal,
        invoice.LodgingTaxAmount,
        invoice.VatAmount,
        invoice.TotalAmount,
        invoice.VatRate,
        invoice.LodgingTaxRate,
        invoice.TotalAmount ?? 0m);

      var issueAt = KsaTime.ToSaudiTime(invoice.InvoiceDate);
      var uuid = string.IsNullOrWhiteSpace(invoice.ZatcaUuid)
        ? Guid.NewGuid().ToString()
        : invoice.ZatcaUuid!;

      var data = new Dictionary<string, object>
      {
        ["uuid"] = uuid,
        ["id"] = invoice.InvoiceNo,
        ["issueDate"] = issueAt.ToString("yyyy-MM-dd"),
        ["issueTime"] = issueAt.ToString("HH:mm:ss"),
        ["currencyCode"] = "SAR",
        ["taxCurrencyCode"] = "SAR",
        ["invoiceType"] = ZatcaUblDocumentBuilder.BuildInvoiceType(isSimplified, "invoice"),
        ["additionalDocuments"] = ZatcaUblDocumentBuilder.BuildAdditionalDocuments(
          icv,
          ZatcaUblDocumentBuilder.NormalizePreviousHash(previousHash)),
        ["signature"] = ZatcaUblDocumentBuilder.BuildSignatureReference(),
        ["supplier"] = ZatcaUblDocumentBuilder.BuildSupplier(seller),
        ["paymentMeans"] = ZatcaUblDocumentBuilder.BuildPaymentMeans("10", null),
        ["delivery"] = new Dictionary<string, object>
        {
          ["actualDeliveryDate"] = issueAt.ToString("yyyy-MM-dd")
        }
      };

      ZatcaUblDocumentBuilder.ApplyHotelTaxFragments(data, amounts, BuildLineName(invoice));

      data["customer"] = isSimplified
        ? new Dictionary<string, object>()
        : customerParty ?? ZatcaUblDocumentBuilder.BuildCustomerParty(
          "Guest",
          null,
          null,
          null,
          null,
          null,
          null);

      return data;
    }

    public static Dictionary<string, object> MapCreditNote(
      CreditNote creditNote,
      Invoice? originalInvoice,
      ZatcaDetails seller,
      ZatcaProfileResolution profile,
      Dictionary<string, object>? customerParty,
      string previousHash,
      int icv)
    {
      var isSimplified = string.Equals(
        profile.Profile,
        ZatcaApiConstants.ProfileSimplified,
        StringComparison.OrdinalIgnoreCase);

      var amounts = ZatcaUblDocumentBuilder.ResolveHotelAmounts(
        creditNote.Subtotal,
        creditNote.LodgingTaxAmount,
        creditNote.VatAmount,
        creditNote.CreditAmount,
        creditNote.VatRate,
        creditNote.LodgingTaxRate,
        creditNote.CreditAmount);

      var issueAt = KsaTime.ToSaudiTime(creditNote.CreditNoteDate);
      var uuid = string.IsNullOrWhiteSpace(creditNote.ZatcaUuid)
        ? Guid.NewGuid().ToString()
        : creditNote.ZatcaUuid!;

      var billingUuid = originalInvoice?.ZatcaUuid;
      if (string.IsNullOrWhiteSpace(billingUuid))
      {
        throw new InvalidOperationException(
          $"Original invoice ZATCA UUID is required for credit note {creditNote.CreditNoteNo}. Submit the invoice to ZATCA first.");
      }

      var data = new Dictionary<string, object>
      {
        ["uuid"] = uuid,
        ["id"] = creditNote.CreditNoteNo,
        ["issueDate"] = issueAt.ToString("yyyy-MM-dd"),
        ["issueTime"] = issueAt.ToString("HH:mm:ss"),
        ["currencyCode"] = "SAR",
        ["taxCurrencyCode"] = "SAR",
        ["invoiceType"] = ZatcaUblDocumentBuilder.BuildInvoiceType(isSimplified, "credit"),
        ["additionalDocuments"] = ZatcaUblDocumentBuilder.BuildAdditionalDocuments(
          icv,
          ZatcaUblDocumentBuilder.NormalizePreviousHash(previousHash)),
        ["signature"] = ZatcaUblDocumentBuilder.BuildSignatureReference(),
        ["supplier"] = ZatcaUblDocumentBuilder.BuildSupplier(seller),
        ["paymentMeans"] = ZatcaUblDocumentBuilder.BuildPaymentMeans(
          "10",
          string.IsNullOrWhiteSpace(creditNote.Reason) ? "Credit note" : creditNote.Reason),
        ["delivery"] = new Dictionary<string, object>
        {
          ["actualDeliveryDate"] = issueAt.ToString("yyyy-MM-dd")
        },
        ["billingReferences"] = new List<object>
        {
          new Dictionary<string, object> { ["id"] = billingUuid }
        }
      };

      ZatcaUblDocumentBuilder.ApplyHotelTaxFragments(data, amounts, BuildCreditLineName(creditNote));

      data["customer"] = isSimplified
        ? new Dictionary<string, object>()
        : customerParty ?? ZatcaUblDocumentBuilder.BuildCustomerParty(
          "Guest",
          null,
          null,
          null,
          null,
          null,
          null);

      return data;
    }

    public static Dictionary<string, object> MapDebitNote(
      DebitNote debitNote,
      Invoice? originalInvoice,
      ZatcaDetails seller,
      ZatcaProfileResolution profile,
      Dictionary<string, object>? customerParty,
      string previousHash,
      int icv)
    {
      var isSimplified = string.Equals(
        profile.Profile,
        ZatcaApiConstants.ProfileSimplified,
        StringComparison.OrdinalIgnoreCase);

      var amounts = ZatcaUblDocumentBuilder.ResolveHotelAmounts(
        debitNote.Subtotal,
        debitNote.LodgingTaxAmount,
        debitNote.VatAmount,
        debitNote.DebitAmount,
        debitNote.VatRate,
        debitNote.LodgingTaxRate,
        debitNote.DebitAmount);

      var issueAt = KsaTime.ToSaudiTime(debitNote.DebitNoteDate);
      var uuid = string.IsNullOrWhiteSpace(debitNote.ZatcaUuid)
        ? Guid.NewGuid().ToString()
        : debitNote.ZatcaUuid!;

      var billingUuid = originalInvoice?.ZatcaUuid;
      if (string.IsNullOrWhiteSpace(billingUuid))
      {
        throw new InvalidOperationException(
          $"Original invoice ZATCA UUID is required for debit note {debitNote.DebitNoteNo}. Submit the invoice to ZATCA first.");
      }

      var data = new Dictionary<string, object>
      {
        ["uuid"] = uuid,
        ["id"] = debitNote.DebitNoteNo,
        ["issueDate"] = issueAt.ToString("yyyy-MM-dd"),
        ["issueTime"] = issueAt.ToString("HH:mm:ss"),
        ["currencyCode"] = "SAR",
        ["taxCurrencyCode"] = "SAR",
        ["invoiceType"] = ZatcaUblDocumentBuilder.BuildInvoiceType(isSimplified, "debit"),
        ["additionalDocuments"] = ZatcaUblDocumentBuilder.BuildAdditionalDocuments(
          icv,
          ZatcaUblDocumentBuilder.NormalizePreviousHash(previousHash)),
        ["signature"] = ZatcaUblDocumentBuilder.BuildSignatureReference(),
        ["supplier"] = ZatcaUblDocumentBuilder.BuildSupplier(seller),
        ["paymentMeans"] = ZatcaUblDocumentBuilder.BuildPaymentMeans(
          "10",
          string.IsNullOrWhiteSpace(debitNote.Reason) ? "Debit note" : debitNote.Reason),
        ["delivery"] = new Dictionary<string, object>
        {
          ["actualDeliveryDate"] = issueAt.ToString("yyyy-MM-dd")
        },
        ["billingReferences"] = new List<object>
        {
          new Dictionary<string, object> { ["id"] = billingUuid }
        }
      };

      ZatcaUblDocumentBuilder.ApplyHotelTaxFragments(data, amounts, BuildDebitLineName(debitNote));

      data["customer"] = isSimplified
        ? new Dictionary<string, object>()
        : customerParty ?? ZatcaUblDocumentBuilder.BuildCustomerParty(
          "Guest",
          null,
          null,
          null,
          null,
          null,
          null);

      return data;
    }

    private static string BuildLineName(Invoice invoice) =>
      string.IsNullOrWhiteSpace(invoice.Notes)
        ? $"Invoice {invoice.InvoiceNo}"
        : invoice.Notes.Trim();

    private static string BuildCreditLineName(CreditNote creditNote) =>
      string.IsNullOrWhiteSpace(creditNote.Reason)
        ? $"Credit note {creditNote.CreditNoteNo}"
        : creditNote.Reason.Trim();

    private static string BuildDebitLineName(DebitNote debitNote) =>
      string.IsNullOrWhiteSpace(debitNote.Reason)
        ? $"Debit note {debitNote.DebitNoteNo}"
        : debitNote.Reason.Trim();
  }
}
