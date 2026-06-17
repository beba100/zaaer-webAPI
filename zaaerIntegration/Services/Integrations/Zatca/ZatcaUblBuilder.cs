using System.Security.Cryptography;
using System.Text;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using zaaerIntegration.Configuration;
using zaaerIntegration.Data;
using Zatca.EInvoice.Signing;
using Zatca.EInvoice.Validation;
using Zatca.EInvoice.Xml;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  /// <summary>
  /// Builds UBL 2.1 via Zatca.EInvoice, signs with the device private key, returns base64 payload for gateway submission.
  /// </summary>
  public sealed class ZatcaUblBuilder : IZatcaUblBuilder
  {
    private readonly ApplicationDbContext _db;
    private readonly IIntegrationSecretProtector _secretProtector;
    private readonly IHostEnvironment _env;
    private readonly ZatcaOptions _zatcaOptions;
    private readonly ILogger<ZatcaUblBuilder> _logger;

    public ZatcaUblBuilder(
      ApplicationDbContext db,
      IIntegrationSecretProtector secretProtector,
      IHostEnvironment env,
      IOptions<ZatcaOptions> zatcaOptions,
      ILogger<ZatcaUblBuilder> logger)
    {
      _db = db;
      _secretProtector = secretProtector;
      _env = env;
      _zatcaOptions = zatcaOptions.Value;
      _logger = logger;
    }

    public async Task<ZatcaUblBuildResult> BuildAndSignInvoiceAsync(
      Invoice invoice,
      ZatcaDetails seller,
      ZatcaDevice device,
      ZatcaProfileResolution profile,
      string previousHash,
      int icv,
      CancellationToken cancellationToken = default)
    {
      var customer = await ResolveCustomerPartyAsync(
        invoice.ReservationId,
        invoice.CustomerId,
        profile,
        cancellationToken);

      var invoiceData = ZatcaInvoiceUblMapper.MapInvoice(
        invoice,
        seller,
        profile,
        customer,
        previousHash,
        icv);

      return await BuildAndSignCoreAsync(invoice.InvoiceNo, invoiceData, seller, device, profile, cancellationToken);
    }

    public async Task<ZatcaUblBuildResult> BuildAndSignCreditNoteAsync(
      CreditNote creditNote,
      Invoice? originalInvoice,
      ZatcaDetails seller,
      ZatcaDevice device,
      ZatcaProfileResolution profile,
      string previousHash,
      int icv,
      CancellationToken cancellationToken = default)
    {
      var customer = await ResolveCustomerPartyAsync(
        creditNote.ReservationId,
        creditNote.CustomerId,
        profile,
        cancellationToken);

      var invoiceData = ZatcaInvoiceUblMapper.MapCreditNote(
        creditNote,
        originalInvoice,
        seller,
        profile,
        customer,
        previousHash,
        icv);

      return await BuildAndSignCoreAsync(creditNote.CreditNoteNo, invoiceData, seller, device, profile, cancellationToken);
    }

    public async Task<ZatcaUblBuildResult> BuildAndSignDebitNoteAsync(
      DebitNote debitNote,
      Invoice? originalInvoice,
      ZatcaDetails seller,
      ZatcaDevice device,
      ZatcaProfileResolution profile,
      string previousHash,
      int icv,
      CancellationToken cancellationToken = default)
    {
      var customer = await ResolveCustomerPartyAsync(
        debitNote.ReservationId,
        debitNote.CustomerId,
        profile,
        cancellationToken);

      var invoiceData = ZatcaInvoiceUblMapper.MapDebitNote(
        debitNote,
        originalInvoice,
        seller,
        profile,
        customer,
        previousHash,
        icv);

      return await BuildAndSignCoreAsync(debitNote.DebitNoteNo, invoiceData, seller, device, profile, cancellationToken);
    }

    private async Task<ZatcaUblBuildResult> BuildAndSignCoreAsync(
      string documentNo,
      Dictionary<string, object> invoiceData,
      ZatcaDetails seller,
      ZatcaDevice device,
      ZatcaProfileResolution profile,
      CancellationToken cancellationToken)
    {
      _ = cancellationToken;

      try
      {
        if (string.IsNullOrWhiteSpace(seller.TaxNumber))
        {
          return Fail("Seller tax number is missing in ZATCA settings.");
        }

        if (device.PrivateKeyEncrypted == null)
        {
          return Fail("Device private key is not stored. Re-onboard the EGS device.");
        }

        var amountValidation = InvoiceAmountValidator.ValidateMonetaryTotals(invoiceData);
        if (!amountValidation.IsValid)
        {
          return Fail(string.Join("; ", amountValidation.Errors));
        }

        if (string.Equals(profile.Profile, ZatcaApiConstants.ProfileStandard, StringComparison.OrdinalIgnoreCase))
        {
          if (invoiceData.TryGetValue("customer", out var customerObj)
              && customerObj is Dictionary<string, object> customer
              && (!customer.ContainsKey("taxId") || string.IsNullOrWhiteSpace(customer["taxId"]?.ToString())))
          {
            return Fail(
              "Standard (B2B) invoice requires buyer VAT on the corporate customer record.");
          }
        }

        var csidToken = !string.IsNullOrWhiteSpace(device.ProductionCsid)
          ? device.ProductionCsid
          : device.ComplianceCsid;
        if (string.IsNullOrWhiteSpace(csidToken))
        {
          return Fail("Device CSID is missing.");
        }

        var privateKey = _secretProtector.Unprotect(device.PrivateKeyEncrypted);
        if (string.IsNullOrWhiteSpace(privateKey))
        {
          return Fail(IntegrationSecretProtector.DecryptFailedUserMessage);
        }

        using var signingCertificate = ZatcaSigningCertificateFactory.CreateSigningCertificate(csidToken, privateKey);
        var generator = new InvoiceGenerator();
        var xml = generator.Generate(invoiceData);
        var signed = InvoiceSigner.Sign(xml, signingCertificate);

        if (_zatcaOptions.LogInvoiceXmlBeforeSubmit)
        {
          ZatcaUblXmlDiagnostics.TryWrite(
            _env,
            _logger,
            documentNo,
            profile,
            xml,
            signed.SignedXml);
        }

        return new ZatcaUblBuildResult
        {
          Success = true,
          SignedXmlBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(signed.SignedXml)),
          InvoiceHash = signed.Hash,
          QrCode = signed.QrCode
        };
      }
      catch (CryptographicException ex)
      {
        _logger.LogError(ex, "[ZATCA UBL] Signing key/certificate mismatch for {DocumentNo}", documentNo);
        return Fail(
          "Private key does not match the stored CSID certificate. " +
          "In Integrations → ZATCA repeat: (1) Register device OTP, (2) Run compliance tests, (3) Request Production CSID.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "[ZATCA UBL] Build/sign failed for {DocumentNo}", documentNo);
        return Fail(ex.Message);
      }
    }

    private async Task<Dictionary<string, object>?> ResolveCustomerPartyAsync(
      int? reservationId,
      int? customerId,
      ZatcaProfileResolution profile,
      CancellationToken cancellationToken)
    {
      if (string.Equals(profile.Profile, ZatcaApiConstants.ProfileSimplified, StringComparison.OrdinalIgnoreCase))
      {
        return null;
      }

      if (reservationId is > 0)
      {
        var reservation = await ZatcaReservationLinkage.FindReservationAsync(
          _db,
          reservationId.Value,
          cancellationToken: cancellationToken);

        if (reservation?.CorporateId is int corpRef and > 0)
        {
          var corporate = await ZatcaReservationLinkage.FindCorporateCustomerAsync(
            _db,
            reservation.HotelId,
            corpRef,
            cancellationToken);

          if (corporate != null)
          {
            return ZatcaUblDocumentBuilder.BuildCustomerParty(
              corporate.CorporateName,
              corporate.VatRegistrationNo,
              corporate.Address,
              "1111",
              corporate.City,
              corporate.City,
              corporate.PostalCode);
          }
        }
      }

      if (customerId is > 0)
      {
        var customer = await _db.Customers.AsNoTracking()
          .FirstOrDefaultAsync(c => c.CustomerId == customerId.Value, cancellationToken);
        if (customer != null)
        {
          return ZatcaUblDocumentBuilder.BuildCustomerParty(
            customer.CustomerName,
            null,
            customer.Address,
            "1111",
            null,
            null,
            null);
        }
      }

      return null;
    }

    private static ZatcaUblBuildResult Fail(string message) =>
      new() { Success = false, ErrorMessage = message };
  }
}
