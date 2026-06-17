using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  /// <summary>
  /// Shared UBL dictionary fragments for Zatca.EInvoice <see cref="Zatca.EInvoice.Xml.InvoiceGenerator"/>.
  /// </summary>
  internal static class ZatcaUblDocumentBuilder
  {
    public static Dictionary<string, object> BuildInvoiceType(bool isSimplified, string subType)
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

    public static Dictionary<string, object> BuildSignatureReference() =>
      new()
      {
        ["id"] = "urn:oasis:names:specification:ubl:signature:Invoice",
        ["signatureMethod"] = "urn:oasis:names:specification:ubl:dsig:enveloped:xades"
      };

    public static List<object> BuildAdditionalDocuments(int icv, string previousHash) =>
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

    public static Dictionary<string, object> BuildSupplier(ZatcaDetails seller)
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
          ["street"] = string.IsNullOrWhiteSpace(seller.StreetName)
            ? (string.IsNullOrWhiteSpace(seller.Address) ? "King Fahd Road" : seller.Address.Trim())
            : seller.StreetName.Trim(),
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

    public static Dictionary<string, object> BuildCustomerParty(
      string registrationName,
      string? taxId,
      string? street,
      string? buildingNumber,
      string? citySubdivision,
      string? city,
      string? postalZone)
    {
      var party = new Dictionary<string, object>
      {
        ["registrationName"] = registrationName,
        ["taxScheme"] = new Dictionary<string, object> { ["id"] = "VAT" },
        ["address"] = new Dictionary<string, object>
        {
          ["street"] = string.IsNullOrWhiteSpace(street) ? "—" : street.Trim(),
          ["buildingNumber"] = NormalizeBuildingNumber(buildingNumber),
          ["citySubdivisionName"] = string.IsNullOrWhiteSpace(citySubdivision) ? "—" : citySubdivision.Trim(),
          ["city"] = string.IsNullOrWhiteSpace(city) ? "Riyadh" : city.Trim(),
          ["postalZone"] = NormalizePostalZone(postalZone),
          ["country"] = "SA"
        }
      };

      if (!string.IsNullOrWhiteSpace(taxId))
      {
        party["taxId"] = taxId.Trim();
      }

      return party;
    }

    public static Dictionary<string, object> BuildPaymentMeans(string code, string? instructionNote)
    {
      var means = new Dictionary<string, object> { ["code"] = code };
      if (!string.IsNullOrWhiteSpace(instructionNote))
      {
        means["instructionNote"] = instructionNote.Trim();
      }

      return means;
    }

    public readonly record struct HotelInvoiceAmounts(
      decimal Subtotal,
      decimal LodgingAmount,
      decimal VatAmount,
      decimal Total,
      decimal VatRate,
      decimal LodgingRate);

    public static Dictionary<string, object> BuildTaxTotal(decimal subtotal, decimal taxAmount, decimal vatPercent) =>
      new()
      {
        ["taxAmount"] = taxAmount,
        ["subTotals"] = new List<object>
        {
          new Dictionary<string, object>
          {
            ["taxableAmount"] = subtotal,
            ["taxAmount"] = taxAmount,
            ["taxCategory"] = BuildVatCategory(vatPercent)
          }
        }
      };

    public static Dictionary<string, object> BuildMonetaryTotal(
      decimal subtotal,
      decimal taxAmount,
      decimal total) =>
      new()
      {
        ["lineExtensionAmount"] = subtotal,
        ["taxExclusiveAmount"] = subtotal,
        ["taxInclusiveAmount"] = total,
        ["prepaidAmount"] = 0.00m,
        ["payableAmount"] = total,
        ["allowanceTotalAmount"] = 0.00m
      };

    public static List<object> BuildSingleLine(
      string lineName,
      decimal subtotal,
      decimal taxAmount,
      decimal total,
      decimal vatPercent,
      decimal quantity = 1m) =>
      new()
      {
        new Dictionary<string, object>
        {
          ["id"] = "1",
          ["unitCode"] = "PCE",
          ["quantity"] = quantity,
          ["lineExtensionAmount"] = subtotal,
          ["item"] = new Dictionary<string, object>
          {
            ["name"] = lineName,
            ["classifiedTaxCategory"] = new List<object> { BuildVatCategory(vatPercent) }
          },
          ["price"] = new Dictionary<string, object>
          {
            ["amount"] = quantity > 0 ? Math.Round(subtotal / quantity, 2) : subtotal,
            ["baseQuantity"] = quantity
          },
          ["taxTotal"] = new Dictionary<string, object>
          {
            ["taxAmount"] = taxAmount,
            ["roundingAmount"] = total
          }
        }
      };

    public static Dictionary<string, object> BuildVatCategory(decimal percent = 15m) =>
      new()
      {
        ["id"] = "S",
        ["percent"] = percent,
        ["taxScheme"] = new Dictionary<string, object> { ["id"] = "VAT" }
      };

    public static void ApplyHotelTaxFragments(
      Dictionary<string, object> data,
      HotelInvoiceAmounts amounts,
      string lineName,
      decimal quantity = 1m)
    {
      // Lodging/EWA is included in the invoice line net (BT-131), not as a separate
      // document AllowanceCharge. Zatca.EInvoice InvoiceGenerator does not emit
      // cbc:ChargeTotalAmount (BT-108), which causes BR-CO-12 when document charges exist.
      var lineNet = amounts.LodgingAmount > 0m
        ? Math.Round(amounts.Subtotal + amounts.LodgingAmount, 2, MidpointRounding.AwayFromZero)
        : amounts.Subtotal;
      var lineWithVat = Math.Round(lineNet + amounts.VatAmount, 2, MidpointRounding.AwayFromZero);

      data["taxTotal"] = BuildTaxTotal(lineNet, amounts.VatAmount, amounts.VatRate);
      data["legalMonetaryTotal"] = BuildMonetaryTotal(lineNet, amounts.VatAmount, amounts.Total);
      data["invoiceLines"] = BuildSingleLine(
        lineName,
        lineNet,
        amounts.VatAmount,
        lineWithVat,
        amounts.VatRate,
        quantity);
    }

    public static string NormalizePreviousHash(string? previousHash) =>
      string.IsNullOrWhiteSpace(previousHash)
        ? ZatcaComplianceSampleBuilder.InitialPreviousInvoiceHash
        : previousHash.Trim();

    public static (decimal Subtotal, decimal VatAmount, decimal Total, decimal VatRate) ResolveAmounts(
      decimal? subtotal,
      decimal? vatAmount,
      decimal? totalAmount,
      decimal? vatRate,
      decimal fallbackTotal = 0m)
    {
      var total = totalAmount ?? fallbackTotal;
      var vat = vatAmount ?? 0m;
      var sub = subtotal ?? (total - vat);
      if (sub < 0)
      {
        sub = 0m;
      }

      if (total <= 0 && sub > 0)
      {
        total = sub + vat;
      }

      if (vat <= 0 && sub > 0 && total > sub)
      {
        vat = total - sub;
      }

      var rate = vatRate ?? (sub > 0 ? Math.Round(vat / sub * 100m, 2) : 15m);
      if (rate <= 0)
      {
        rate = 15m;
      }

      return (Math.Round(sub, 2), Math.Round(vat, 2), Math.Round(total, 2), rate);
    }

    public static HotelInvoiceAmounts ResolveHotelAmounts(
      decimal? subtotal,
      decimal? lodgingAmount,
      decimal? vatAmount,
      decimal? totalAmount,
      decimal? vatRate,
      decimal? lodgingRate,
      decimal fallbackTotal = 0m)
    {
      var (sub, vat, total, rate) = ResolveAmounts(subtotal, vatAmount, totalAmount, vatRate, fallbackTotal);

      var lodging = lodgingAmount ?? 0m;
      if (lodging <= 0m && total > 0m)
      {
        var derived = Math.Round(total - sub - vat, 2, MidpointRounding.AwayFromZero);
        if (derived > 0m)
        {
          lodging = derived;
        }
      }

      if (total <= 0m)
      {
        total = Math.Round(sub + lodging + vat, 2, MidpointRounding.AwayFromZero);
      }

      var lr = lodgingRate ?? 0m;
      if (lr <= 0m && sub > 0m && lodging > 0m)
      {
        lr = Math.Round(lodging / sub * 100m, 2, MidpointRounding.AwayFromZero);
      }

      return new HotelInvoiceAmounts(
        Math.Round(sub, 2, MidpointRounding.AwayFromZero),
        Math.Round(lodging, 2, MidpointRounding.AwayFromZero),
        Math.Round(vat, 2, MidpointRounding.AwayFromZero),
        Math.Round(total, 2, MidpointRounding.AwayFromZero),
        rate,
        lr);
    }

    public static string NormalizeBuildingNumber(string? value)
    {
      var digits = DigitsOnly(value);
      if (digits.Length >= 4)
      {
        return digits[^4..];
      }

      return digits.Length > 0 ? digits.PadLeft(4, '0') : "1234";
    }

    public static string NormalizePostalZone(string? value)
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
