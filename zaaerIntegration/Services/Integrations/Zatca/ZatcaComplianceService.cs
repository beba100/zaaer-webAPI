using System.Security.Cryptography.X509Certificates;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Models;
using Microsoft.Extensions.Logging;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Integrations;
using zaaerIntegration.Utilities;
using Zatca.EInvoice.Api;
using Zatca.EInvoice.Certificates;
using Zatca.EInvoice.Exceptions;
using Zatca.EInvoice.Signing;
using Zatca.EInvoice.Validation;
using Zatca.EInvoice.Xml;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  /// <summary>
  /// Six mandatory ZATCA compliance document types before Production CSID.
  /// </summary>
  public enum ZatcaComplianceDocumentType
  {
    StandardInvoice,
    StandardCreditNote,
    StandardDebitNote,
    SimplifiedInvoice,
    SimplifiedCreditNote,
    SimplifiedDebitNote
  }

  public sealed class ZatcaComplianceRunResult
  {
    public string Environment { get; init; } = "simulation";
    public IReadOnlyList<ZatcaComplianceItemResult> Items { get; init; } = Array.Empty<ZatcaComplianceItemResult>();
    public bool AllPassed => Items.Count > 0 && Items.All(i => i.Success);
    public string? Message { get; init; }
  }

  public sealed class ZatcaComplianceItemResult
  {
    public ZatcaComplianceDocumentType DocumentType { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? HttpStatusCode { get; init; }
  }

  public interface IZatcaComplianceService
  {
    Task<ZatcaComplianceRunResult> RunAllSixAsync(
      int hotelId,
      string? environment = null,
      CancellationToken cancellationToken = default);
  }

  public sealed class ZatcaComplianceService : IZatcaComplianceService
  {
    private static readonly ZatcaComplianceDocumentType[] RequiredTypes =
    {
      ZatcaComplianceDocumentType.StandardInvoice,
      ZatcaComplianceDocumentType.StandardCreditNote,
      ZatcaComplianceDocumentType.StandardDebitNote,
      ZatcaComplianceDocumentType.SimplifiedInvoice,
      ZatcaComplianceDocumentType.SimplifiedCreditNote,
      ZatcaComplianceDocumentType.SimplifiedDebitNote
    };

    private readonly ApplicationDbContext _context;
    private readonly IZatcaIntegrationSchemaEnsurer _schemaEnsurer;
    private readonly IIntegrationSecretProtector _secretProtector;
    private readonly ILogger<ZatcaComplianceService> _logger;

    public ZatcaComplianceService(
      ApplicationDbContext context,
      IZatcaIntegrationSchemaEnsurer schemaEnsurer,
      IIntegrationSecretProtector secretProtector,
      ILogger<ZatcaComplianceService> logger)
    {
      _context = context;
      _schemaEnsurer = schemaEnsurer;
      _secretProtector = secretProtector;
      _logger = logger;
    }

    public async Task<ZatcaComplianceRunResult> RunAllSixAsync(
      int hotelId,
      string? environment = null,
      CancellationToken cancellationToken = default)
    {
      await _schemaEnsurer.EnsureAsync(cancellationToken);

      var details = await ZatcaDetailsEnvironmentSync.LoadAlignedForHotelAsync(
        _context,
        hotelId,
        cancellationToken);
      if (details == null)
      {
        return FailAll("Save ZATCA seller settings before running compliance tests.");
      }

      if (!details.IsActive)
      {
        return FailAll(IntegrationPlatformGuard.ZatcaInactiveMessage);
      }

      if (string.IsNullOrWhiteSpace(details.TaxNumber))
      {
        return FailAll("Seller tax number is required in ZATCA settings.");
      }

      var env = ZatcaDetailsEnvironmentSync.ResolveEffective(
        environment ?? details.ApiEnvironment,
        details.Environment);

      if (string.IsNullOrWhiteSpace(details.DeviceUuid))
      {
        return FailAll(
          env == "simulation"
            ? "Simulation device is not registered yet. OTP onboarding is deferred — register the device when OTP is available, then run compliance again."
            : "Register the EGS device (Compliance CSID) before running compliance tests.");
      }

      var device = await _context.ZatcaDevices.FirstOrDefaultAsync(
        d => d.HotelId == hotelId && d.Environment == env && d.DeviceUuid == details.DeviceUuid,
        cancellationToken);

      if (device == null
          || string.IsNullOrWhiteSpace(device.ComplianceCsid)
          || string.IsNullOrWhiteSpace(device.ComplianceSecret)
          || device.PrivateKeyEncrypted == null)
      {
        return FailAll(
          env == "simulation"
            ? "No Simulation Compliance CSID for this device. Complete Simulation OTP onboarding when ready (or test on Sandbox first)."
            : $"No Compliance CSID for environment '{env}'. Run device onboarding (OTP) first.");
      }

      var privateKey = _secretProtector.Unprotect(device.PrivateKeyEncrypted);
      if (string.IsNullOrWhiteSpace(privateKey))
      {
        return FailAll(IntegrationSecretProtector.DecryptFailedUserMessage);
      }

      var certificate = new CertificateInfo(
        device.ComplianceCsid,
        privateKey,
        device.ComplianceSecret);

      X509Certificate2 signingCertificate;
      try
      {
        signingCertificate = ZatcaSigningCertificateFactory.CreateSigningCertificate(
          device.ComplianceCsid,
          privateKey);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "[ZATCA Compliance] Signing certificate load failed for hotel {HotelId}", hotelId);
        return FailAll($"Could not load signing certificate with private key: {ex.Message}");
      }

      using (signingCertificate)
      {
      var apiClient = new ZatcaApiClient(ZatcaEInvoiceEnvironmentMapper.ToApiEnvironment(env));
      apiClient.SetWarningHandling(true);

      var generator = new InvoiceGenerator();
      var items = new List<ZatcaComplianceItemResult>();
      var previousHash = device.LastInvoiceHash ?? ZatcaComplianceSampleBuilder.InitialPreviousInvoiceHash;
      var icv = device.LastIcv > 0 ? device.LastIcv : 0;
      string? standardInvoiceUuid = null;
      string? simplifiedInvoiceUuid = null;

      foreach (var docType in RequiredTypes)
      {
        cancellationToken.ThrowIfCancellationRequested();
        icv++;
        var billingRefUuid = docType switch
        {
          ZatcaComplianceDocumentType.StandardCreditNote or ZatcaComplianceDocumentType.StandardDebitNote =>
            standardInvoiceUuid,
          ZatcaComplianceDocumentType.SimplifiedCreditNote or ZatcaComplianceDocumentType.SimplifiedDebitNote =>
            simplifiedInvoiceUuid,
          _ => null
        };

        try
        {
          if (billingRefUuid == null && docType is ZatcaComplianceDocumentType.StandardCreditNote
              or ZatcaComplianceDocumentType.StandardDebitNote)
          {
            items.Add(new ZatcaComplianceItemResult
            {
              DocumentType = docType,
              Success = false,
              ErrorMessage =
                "Standard invoice compliance must pass first (billing reference UUID is required)."
            });
            break;
          }

          if (billingRefUuid == null && docType is ZatcaComplianceDocumentType.SimplifiedCreditNote
              or ZatcaComplianceDocumentType.SimplifiedDebitNote)
          {
            items.Add(new ZatcaComplianceItemResult
            {
              DocumentType = docType,
              Success = false,
              ErrorMessage =
                "Simplified invoice compliance must pass first (billing reference UUID is required)."
            });
            break;
          }

          var invoiceData = ZatcaComplianceSampleBuilder.Build(
            docType,
            details,
            icv,
            previousHash,
            billingRefUuid);

          var amountValidation = InvoiceAmountValidator.ValidateMonetaryTotals(invoiceData);
          if (!amountValidation.IsValid)
          {
            var localErr = string.Join("; ", amountValidation.Errors);
            items.Add(new ZatcaComplianceItemResult
            {
              DocumentType = docType,
              Success = false,
              ErrorMessage = $"Local validation: {localErr}"
            });
            break;
          }

          var xml = generator.Generate(invoiceData);
          var signed = InvoiceSigner.Sign(xml, signingCertificate);
          var uuid = invoiceData["uuid"].ToString()!;

          var result = await apiClient.ValidateInvoiceComplianceAsync(
            signed.SignedXml,
            signed.Hash,
            uuid,
            certificate.RawCertificate,
            certificate.Secret,
            cancellationToken);

          if (result.IsSuccess)
          {
            previousHash = signed.Hash;
            if (docType == ZatcaComplianceDocumentType.StandardInvoice)
            {
              standardInvoiceUuid = uuid;
            }
            else if (docType == ZatcaComplianceDocumentType.SimplifiedInvoice)
            {
              simplifiedInvoiceUuid = uuid;
            }

            items.Add(new ZatcaComplianceItemResult
            {
              DocumentType = docType,
              Success = true
            });
            continue;
          }

          var err = FormatApiErrors(result.Errors);
          items.Add(new ZatcaComplianceItemResult
          {
            DocumentType = docType,
            Success = false,
            ErrorMessage = err
          });
          break;
        }
        catch (ZatcaApiException ex)
        {
          var detail = ZatcaComplianceApiErrorParser.Format(ex);
          _logger.LogWarning(
            "[ZATCA Compliance] API error hotel {HotelId} env {Env} doc {Doc}: {Detail}",
            hotelId,
            env,
            docType,
            detail);

          try
          {
            _context.IntegrationResponses.Add(new IntegrationResponse
            {
              HotelId = hotelId,
              Service = ZatcaApiConstants.ServiceName,
              EventType = $"Compliance_{docType}",
              Status = "Error",
              ErrorMessage = IntegrationResponseLogHelper.TruncateErrorMessage(detail),
              ResponsePayload = ex.Response,
              HttpStatusCode = (int?)ex.StatusCode,
              CreatedAt = KsaTime.Now
            });
            await _context.SaveChangesAsync(cancellationToken);
          }
          catch (Exception logEx)
          {
            _logger.LogWarning(
              logEx,
              "[ZATCA Compliance] Could not persist integration response log for hotel {HotelId}",
              hotelId);
          }

          items.Add(new ZatcaComplianceItemResult
          {
            DocumentType = docType,
            Success = false,
            ErrorMessage = detail,
            HttpStatusCode = (int?)ex.StatusCode
          });
          break;
        }
        catch (Exception ex)
        {
          _logger.LogError(
            ex,
            "[ZATCA Compliance] Failed hotel {HotelId} env {Env} doc {Doc}",
            hotelId,
            env,
            docType);
          items.Add(new ZatcaComplianceItemResult
          {
            DocumentType = docType,
            Success = false,
            ErrorMessage = ex.Message
          });
          break;
        }
      }

      var allPassed = items.Count == RequiredTypes.Length && items.All(i => i.Success);
      if (allPassed)
      {
        device.LastIcv = icv;
        device.LastInvoiceHash = previousHash;
        device.DeviceStatus = "compliance_tests_passed";
        device.UpdatedAt = KsaTime.Now;
        await _context.SaveChangesAsync(cancellationToken);

        _context.IntegrationResponses.Add(new IntegrationResponse
        {
          HotelId = hotelId,
          Service = ZatcaApiConstants.ServiceName,
          EventType = "Compliance_SixTypes",
          Status = "Success",
          ResponsePayload = $"All six compliance samples passed on {env}.",
          CreatedAt = KsaTime.Now
        });
        await _context.SaveChangesAsync(cancellationToken);
      }

      return new ZatcaComplianceRunResult
      {
        Environment = env,
        Items = items,
        Message = allPassed
          ? "All six compliance document types passed. You can request Production CSID."
          : items.Count == 0
            ? "Compliance run did not start."
            : "One or more compliance samples failed. Fix errors and run again."
      };
      }
    }

    private static string FormatApiErrors(
      IReadOnlyList<ValidationMessage>? errors)
    {
      if (errors == null || errors.Count == 0)
      {
        return "ZATCA validation failed.";
      }

      return string.Join("; ", errors.Select(e =>
        string.IsNullOrWhiteSpace(e.Code) ? e.Message : $"[{e.Code}] {e.Message}"));
    }

    private static ZatcaComplianceRunResult FailAll(string message) =>
      new()
      {
        Items = RequiredTypes.Select(t => new ZatcaComplianceItemResult
        {
          DocumentType = t,
          Success = false,
          ErrorMessage = message
        }).ToList(),
        Message = message
      };
  }
}
