using FinanceLedgerAPI.Models;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  /// <summary>
  /// Builds ZATCA compliance sample payloads for Zatca.EInvoice 1.0.9 (see SampleCommands / InvoiceGenerator).
  /// </summary>
  public static class ZatcaComplianceSampleBuilder
  {
    /// <summary>ZATCA first-in-chain PIH (base64 SHA256 of '0').</summary>
    public const string InitialPreviousInvoiceHash =
      "NWZlY2ViNjZmZmM4NmYzOGQ5NTI3ODZjNmQ2OTZjNzljMmRiYzIzOWRkNGU5MWI0NjcyOWQ3M2EyN2ZiNTdlOQ==";

    private const string StandardBuyerVat = "399999999900003";
    private const decimal Subtotal = 100.00m;
    private const decimal TaxAmount = 15.00m;
    private const decimal Total = 115.00m;

    public static Dictionary<string, object> Build(
      ZatcaComplianceDocumentType documentType,
      ZatcaDetails seller,
      int icv,
      string previousHash,
      string? billingReferenceUuid = null)
    {
      var (isSimplified, subType, docId) = MapDocument(documentType);
      var now = KsaTime.Now;
      var uuid = Guid.NewGuid().ToString();

      var data = new Dictionary<string, object>
      {
        ["uuid"] = uuid,
        ["id"] = docId,
        ["issueDate"] = now.ToString("yyyy-MM-dd"),
        ["issueTime"] = now.ToString("HH:mm:ss"),
        ["currencyCode"] = "SAR",
        ["taxCurrencyCode"] = "SAR",
        ["invoiceType"] = BuildInvoiceType(isSimplified, subType),
        ["additionalDocuments"] = BuildAdditionalDocuments(icv, previousHash),
        ["signature"] = BuildSignatureReference(),
        ["supplier"] = BuildSupplier(seller),
        ["paymentMeans"] = BuildPaymentMeans(documentType),
        ["delivery"] = new Dictionary<string, object>
        {
          ["actualDeliveryDate"] = now.ToString("yyyy-MM-dd")
        },
        ["taxTotal"] = BuildTaxTotal(),
        ["legalMonetaryTotal"] = BuildMonetaryTotal(),
        ["invoiceLines"] = BuildLines()
      };

      // Simplified invoices need an empty AccountingCustomerParty in UBL.
      data["customer"] = isSimplified
        ? new Dictionary<string, object>()
        : BuildStandardCustomer();

      if (documentType is ZatcaComplianceDocumentType.StandardCreditNote
          or ZatcaComplianceDocumentType.StandardDebitNote
          or ZatcaComplianceDocumentType.SimplifiedCreditNote
          or ZatcaComplianceDocumentType.SimplifiedDebitNote)
      {
        if (string.IsNullOrWhiteSpace(billingReferenceUuid))
        {
          throw new InvalidOperationException(
            "Billing reference UUID is required for credit/debit note compliance samples.");
        }

        data["billingReferences"] = new List<object>
        {
          new Dictionary<string, object> { ["id"] = billingReferenceUuid }
        };
      }

      return data;
    }

    private static (bool IsSimplified, string SubType, string DocId) MapDocument(
      ZatcaComplianceDocumentType documentType) =>
      documentType switch
      {
        ZatcaComplianceDocumentType.StandardInvoice =>
          (false, "invoice", "STD-INV-COMP-001"),
        ZatcaComplianceDocumentType.StandardCreditNote =>
          (false, "credit", "STD-CN-COMP-001"),
        ZatcaComplianceDocumentType.StandardDebitNote =>
          (false, "debit", "STD-DN-COMP-001"),
        ZatcaComplianceDocumentType.SimplifiedInvoice =>
          (true, "invoice", "SIM-INV-COMP-001"),
        ZatcaComplianceDocumentType.SimplifiedCreditNote =>
          (true, "credit", "SIM-CN-COMP-001"),
        ZatcaComplianceDocumentType.SimplifiedDebitNote =>
          (true, "debit", "SIM-DN-COMP-001"),
        _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null)
      };

    /// <summary>
    /// KSA-2 (InvoiceTypeCode @name): NNPNESBCG — 01 standard / 02 simplified, remaining flags 0.
    /// </summary>
    private static Dictionary<string, object> BuildInvoiceType(bool isSimplified, string subType)
    {
      var typeCode = subType switch
      {
        "credit" => "381",
        "debit" => "383",
        _ => "388"
      };

      var name = (isSimplified ? "02" : "01") + "0000000";

      return new Dictionary<string, object>
      {
        ["typeCode"] = typeCode,
        ["name"] = name
      };
    }

    private static Dictionary<string, object> BuildSignatureReference() =>
      new()
      {
        ["id"] = "urn:oasis:names:specification:ubl:signature:Invoice",
        ["signatureMethod"] = "urn:oasis:names:specification:ubl:dsig:enveloped:xades"
      };

    private static List<object> BuildAdditionalDocuments(int icv, string previousHash) =>
      new()
      {
        new Dictionary<string, object> { ["id"] = "ICV", ["uuid"] = icv.ToString() },
        new Dictionary<string, object>
        {
          ["id"] = "PIH",
          ["attachment"] = new Dictionary<string, object>
          {
            ["embeddedDocument"] = previousHash,
            ["mimeCode"] = "text/plain"
          }
        },
        new Dictionary<string, object> { ["id"] = "QR" }
      };

    private static Dictionary<string, object> BuildSupplier(ZatcaDetails seller)
    {
      var crn = AlphanumericOnly(
        string.IsNullOrWhiteSpace(seller.CorporateRegistrationNumber)
          ? "2050002700"
          : seller.CorporateRegistrationNumber);
      var taxId = seller.TaxNumber!.Trim();

      return new Dictionary<string, object>
      {
        ["registrationName"] = seller.CompanyName,
        ["taxId"] = taxId,
        ["partyIdentification"] = crn,
        ["partyIdentificationId"] = "CRN",
        ["taxScheme"] = new Dictionary<string, object> { ["id"] = "VAT" },
        ["address"] = new Dictionary<string, object>
        {
          ["street"] = string.IsNullOrWhiteSpace(seller.StreetName) ? "King Fahd Road" : seller.StreetName.Trim(),
          ["buildingNumber"] = NormalizeBuildingNumber(seller.BuildingNumber),
          ["citySubdivisionName"] = string.IsNullOrWhiteSpace(seller.CitySubdivisionName)
            ? (seller.City ?? "Riyadh")
            : seller.CitySubdivisionName.Trim(),
          ["city"] = string.IsNullOrWhiteSpace(seller.City) ? "Riyadh" : seller.City.Trim(),
          ["postalZone"] = NormalizePostalZone(seller.PostalZone),
          ["country"] = "SA"
        }
      };
    }

    private static Dictionary<string, object> BuildStandardCustomer() =>
      new()
      {
        ["registrationName"] = "Fatoora Samples",
        ["taxId"] = StandardBuyerVat,
        ["taxScheme"] = new Dictionary<string, object> { ["id"] = "VAT" },
        ["address"] = new Dictionary<string, object>
        {
          ["street"] = "Salah Al-Din",
          ["buildingNumber"] = "1111",
          ["citySubdivisionName"] = "Al-Murooj",
          ["city"] = "Riyadh",
          ["postalZone"] = "12222",
          ["country"] = "SA"
        }
      };

    private static Dictionary<string, object> BuildPaymentMeans(ZatcaComplianceDocumentType documentType)
    {
      var means = new Dictionary<string, object> { ["code"] = "10" };

      // BR-KSA-17 / KSA-10: reason for credit/debit note → cbc:InstructionNote on PaymentMeans.
      var instructionNote = documentType switch
      {
        ZatcaComplianceDocumentType.StandardCreditNote or ZatcaComplianceDocumentType.SimplifiedCreditNote =>
          "Refund for returned goods",
        ZatcaComplianceDocumentType.StandardDebitNote or ZatcaComplianceDocumentType.SimplifiedDebitNote =>
          "Additional charges for services rendered",
        _ => null
      };

      if (!string.IsNullOrWhiteSpace(instructionNote))
      {
        means["instructionNote"] = instructionNote;
      }

      return means;
    }

    private static Dictionary<string, object> BuildTaxTotal() =>
      new()
      {
        ["taxAmount"] = TaxAmount,
        ["subTotals"] = new List<object>
        {
          new Dictionary<string, object>
          {
            ["taxableAmount"] = Subtotal,
            ["taxAmount"] = TaxAmount,
            ["taxCategory"] = BuildVatCategory()
          }
        }
      };

    private static Dictionary<string, object> BuildMonetaryTotal() =>
      new()
      {
        ["lineExtensionAmount"] = Subtotal,
        ["taxExclusiveAmount"] = Subtotal,
        ["taxInclusiveAmount"] = Total,
        ["prepaidAmount"] = 0.00m,
        ["payableAmount"] = Total,
        ["allowanceTotalAmount"] = 0.00m
      };

    private static List<object> BuildLines() =>
      new()
      {
        new Dictionary<string, object>
        {
          ["id"] = "1",
          ["unitCode"] = "PCE",
          ["quantity"] = 1.0m,
          ["lineExtensionAmount"] = Subtotal,
          ["item"] = new Dictionary<string, object>
          {
            ["name"] = "Compliance sample line",
            ["classifiedTaxCategory"] = new List<object> { BuildVatCategory() }
          },
          ["price"] = new Dictionary<string, object>
          {
            ["amount"] = Subtotal,
            ["baseQuantity"] = 1.0m
          },
          ["taxTotal"] = new Dictionary<string, object>
          {
            ["taxAmount"] = TaxAmount,
            ["roundingAmount"] = Total
          }
        }
      };

    private static Dictionary<string, object> BuildVatCategory() =>
      new()
      {
        ["id"] = "S",
        ["percent"] = 15m,
        ["taxScheme"] = new Dictionary<string, object> { ["id"] = "VAT" }
      };

    private static string NormalizeBuildingNumber(string? value)
    {
      var digits = DigitsOnly(value);
      if (digits.Length >= 4)
      {
        return digits[^4..];
      }

      return digits.Length > 0 ? digits.PadLeft(4, '0') : "1234";
    }

    private static string NormalizePostalZone(string? value)
    {
      var digits = DigitsOnly(value);
      if (digits.Length >= 5)
      {
        return digits[..5];
      }

      return digits.Length > 0 ? digits.PadLeft(5, '0') : "12345";
    }

    private static string DigitsOnly(string? value) =>
      new string((value ?? "").Where(char.IsDigit).ToArray());

    private static string AlphanumericOnly(string value) =>
      new string(value.Where(char.IsLetterOrDigit).ToArray());
  }
}
