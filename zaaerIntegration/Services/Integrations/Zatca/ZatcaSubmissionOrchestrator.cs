using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Integrations;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations.Zatca
{
    public sealed class ZatcaSubmissionOrchestrator : IZatcaSubmissionOrchestrator
    {
        private readonly ApplicationDbContext _db;
        private readonly IZatcaIntegrationSchemaEnsurer _schemaEnsurer;
        private readonly IZatcaProfileResolver _profileResolver;
        private readonly IZatcaUblBuilder _ublBuilder;
        private readonly IZatcaGatewayClient _gateway;
        private readonly IZatcaAcceptLanguageResolver _acceptLanguageResolver;
        private readonly IIntegrationSecretProtector _secretProtector;
        private readonly ILogger<ZatcaSubmissionOrchestrator> _logger;

        public ZatcaSubmissionOrchestrator(
            ApplicationDbContext db,
            IZatcaIntegrationSchemaEnsurer schemaEnsurer,
            IZatcaProfileResolver profileResolver,
            IZatcaUblBuilder ublBuilder,
            IZatcaGatewayClient gateway,
            IZatcaAcceptLanguageResolver acceptLanguageResolver,
            IIntegrationSecretProtector secretProtector,
            ILogger<ZatcaSubmissionOrchestrator> logger)
        {
            _db = db;
            _schemaEnsurer = schemaEnsurer;
            _profileResolver = profileResolver;
            _ublBuilder = ublBuilder;
            _gateway = gateway;
            _acceptLanguageResolver = acceptLanguageResolver;
            _secretProtector = secretProtector;
            _logger = logger;
        }

        public async Task<ZatcaBatchProcessResult> ProcessPendingBatchAsync(
            int maxRetries,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);

            var result = new ZatcaBatchProcessResult();
            var settings = await IntegrationPlatformGuard.LoadActiveZatcaAsync(_db, cancellationToken);
            if (settings == null || string.IsNullOrWhiteSpace(settings.TaxNumber))
            {
                _logger.LogDebug("[ZATCA] Inactive or missing zatca_details — skipping batch.");
                return result;
            }

            var environment = ZatcaDetailsEnvironmentSync.ResolveEffective(
                settings.ApiEnvironment,
                settings.Environment);

            var device = await ResolveDeviceAsync(settings, environment, cancellationToken);
            if (device == null)
            {
                _logger.LogWarning("[ZATCA] No active device for hotel {HotelId} env {Env}", settings.HotelId, environment);
                return result;
            }

            var invoices = await _db.Invoices
                .Where(i => i.HotelId == settings.HotelId)
                .Where(i => i.ZatcaStatus == ZatcaApiConstants.StatusPending
                            || i.ZatcaStatus == ZatcaApiConstants.StatusFailed)
                .Where(i => i.ZatcaRetryCount < maxRetries)
                .OrderBy(i => i.InvoiceId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var invoice in invoices)
            {
                result.InvoicesProcessed++;
                var ok = await ProcessInvoiceAsync(invoice, settings, device, cancellationToken);
                if (ok)
                {
                    result.InvoicesSucceeded++;
                }
                else
                {
                    result.InvoicesFailed++;
                }
            }

            var creditNotes = await _db.CreditNotes
                .Where(c => c.HotelId == settings.HotelId)
                .Where(c => c.ZatcaStatus == ZatcaApiConstants.StatusPending
                            || c.ZatcaStatus == ZatcaApiConstants.StatusFailed)
                .Where(c => c.ZatcaRetryCount < maxRetries)
                .OrderBy(c => c.CreditNoteId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var cn in creditNotes)
            {
                result.CreditNotesProcessed++;
                var ok = await ProcessCreditNoteAsync(cn, settings, device, cancellationToken);
                if (ok)
                {
                    result.CreditNotesSucceeded++;
                }
                else
                {
                    result.CreditNotesFailed++;
                }
            }

            var debitNotes = await _db.DebitNotes
                .Where(d => d.HotelId == settings.HotelId)
                .Where(d => d.ZatcaStatus == ZatcaApiConstants.StatusPending
                            || d.ZatcaStatus == ZatcaApiConstants.StatusFailed)
                .Where(d => d.ZatcaRetryCount < maxRetries)
                .OrderBy(d => d.DebitNoteId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var dn in debitNotes)
            {
                result.DebitNotesProcessed++;
                var ok = await ProcessDebitNoteAsync(dn, settings, device, cancellationToken);
                if (ok)
                {
                    result.DebitNotesSucceeded++;
                }
                else
                {
                    result.DebitNotesFailed++;
                }
            }

            return result;
        }

        public async Task<ZatcaSingleDocumentResult> ProcessInvoiceByIdAsync(
            int invoiceId,
            CancellationToken cancellationToken = default) =>
            await ProcessSingleDocumentAsync(
                async () => await _db.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken),
                (doc, settings, device) => ProcessInvoiceAsync(doc, settings, device, cancellationToken),
                doc => doc.ZatcaStatus,
                doc => doc.ZatcaLastError,
                "Invoice not found.",
                cancellationToken);

        public async Task<ZatcaSingleDocumentResult> ProcessCreditNoteByIdAsync(
            int creditNoteId,
            CancellationToken cancellationToken = default)
        {
            var creditNote = await _db.CreditNotes.FirstOrDefaultAsync(
                c => c.CreditNoteId == creditNoteId, cancellationToken);
            if (creditNote == null)
            {
                return ZatcaSingleDocumentResult.Fail("Credit note not found.");
            }

            var parentCheck = await ValidateParentInvoiceSubmittedAsync(creditNote.InvoiceId, cancellationToken);
            if (parentCheck != null)
            {
                return parentCheck;
            }

            return await ProcessSingleDocumentAsync(
                () => Task.FromResult<CreditNote?>(creditNote),
                (doc, settings, device) => ProcessCreditNoteAsync(doc, settings, device, cancellationToken),
                doc => doc.ZatcaStatus,
                doc => doc.ZatcaLastError,
                "Credit note not found.",
                cancellationToken);
        }

        public async Task<ZatcaSingleDocumentResult> ProcessDebitNoteByIdAsync(
            int debitNoteId,
            CancellationToken cancellationToken = default)
        {
            var debitNote = await _db.DebitNotes.FirstOrDefaultAsync(
                d => d.DebitNoteId == debitNoteId, cancellationToken);
            if (debitNote == null)
            {
                return ZatcaSingleDocumentResult.Fail("Debit note not found.");
            }

            var parentCheck = await ValidateParentInvoiceSubmittedAsync(debitNote.InvoiceId, cancellationToken);
            if (parentCheck != null)
            {
                return parentCheck;
            }

            return await ProcessSingleDocumentAsync(
                () => Task.FromResult<DebitNote?>(debitNote),
                (doc, settings, device) => ProcessDebitNoteAsync(doc, settings, device, cancellationToken),
                doc => doc.ZatcaStatus,
                doc => doc.ZatcaLastError,
                "Debit note not found.",
                cancellationToken);
        }

        private async Task<ZatcaSingleDocumentResult?> ValidateParentInvoiceSubmittedAsync(
            int invoiceRef,
            CancellationToken cancellationToken)
        {
            var invoice = await FindOriginalInvoiceAsync(invoiceRef, cancellationToken);
            if (invoice == null)
            {
                return ZatcaSingleDocumentResult.Fail("Parent invoice not found.");
            }

            if (string.IsNullOrWhiteSpace(invoice.ZatcaUuid))
            {
                return ZatcaSingleDocumentResult.Fail("Parent invoice has no ZATCA UUID — submit the invoice first.");
            }

            var status = invoice.ZatcaStatus ?? ZatcaApiConstants.StatusPending;
            if (!string.Equals(status, ZatcaApiConstants.StatusReported, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, ZatcaApiConstants.StatusCleared, StringComparison.OrdinalIgnoreCase))
            {
                return ZatcaSingleDocumentResult.Fail(
                    "Parent invoice must be reported or cleared in ZATCA before sending this document.");
            }

            return null;
        }

        private async Task<ZatcaSingleDocumentResult> ProcessSingleDocumentAsync<T>(
            Func<Task<T?>> loadDocument,
            Func<T, ZatcaDetails, ZatcaDevice, Task<bool>> process,
            Func<T, string?> getStatus,
            Func<T, string?> getLastError,
            string notFoundMessage,
            CancellationToken cancellationToken)
            where T : class
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);

            var document = await loadDocument();
            if (document == null)
            {
                return ZatcaSingleDocumentResult.Fail(notFoundMessage);
            }

            var status = getStatus(document);
            if (!IsManualSendableStatus(status))
            {
                return ZatcaSingleDocumentResult.Fail(
                    $"Document status is '{status}' — only pending or failed documents can be sent.");
            }

            var aligned = await ZatcaDetailsEnvironmentSync.LoadAlignedAsync(_db, cancellationToken);
            if (aligned == null)
            {
                return ZatcaSingleDocumentResult.Fail("ZATCA is not configured for this hotel.");
            }

            if (!aligned.IsActive)
            {
                return ZatcaSingleDocumentResult.Fail(IntegrationPlatformGuard.ZatcaInactiveMessage);
            }

            var settings = aligned;
            if (string.IsNullOrWhiteSpace(settings.TaxNumber))
            {
                return ZatcaSingleDocumentResult.Fail("ZATCA seller tax number is required in settings.");
            }

            var environment = ZatcaDetailsEnvironmentSync.ResolveEffective(
                settings.ApiEnvironment,
                settings.Environment);
            var device = await ResolveDeviceAsync(settings, environment, cancellationToken);
            if (device == null)
            {
                return ZatcaSingleDocumentResult.Fail(
                    $"ZATCA device is not configured for environment '{environment}'. " +
                    "Register the device (OTP) and request Production CSID for this environment.");
            }

            if (IntegrationSecretMigration.TryMigrateZatcaDevicePrivateKey(device, _secretProtector))
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            var deviceError = ValidateDeviceForSubmission(device, environment);
            if (deviceError != null)
            {
                return ZatcaSingleDocumentResult.Fail(deviceError);
            }

            var ok = await process(document, settings, device);
            await _db.SaveChangesAsync(cancellationToken);

            var finalStatus = getStatus(document);
            return ok
                ? ZatcaSingleDocumentResult.Ok(finalStatus)
                : ZatcaSingleDocumentResult.Fail(getLastError(document) ?? "ZATCA submission failed.");
        }

        private static bool IsManualSendableStatus(string? status) =>
            string.Equals(status, ZatcaApiConstants.StatusPending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ZatcaApiConstants.StatusFailed, StringComparison.OrdinalIgnoreCase);

        private async Task<ZatcaDevice?> ResolveDeviceAsync(
            ZatcaDetails settings,
            string environment,
            CancellationToken cancellationToken)
        {
            var deviceUuid = settings.DeviceUuid;
            if (string.IsNullOrWhiteSpace(deviceUuid))
            {
                return null;
            }

            return await _db.ZatcaDevices
                .FirstOrDefaultAsync(
                    d => d.HotelId == settings.HotelId
                         && d.Environment == environment
                         && d.DeviceUuid == deviceUuid,
                    cancellationToken);
        }

        private static string? ValidateDeviceForSubmission(ZatcaDevice device, string environment)
        {
            if (!string.Equals(device.Environment, environment, StringComparison.OrdinalIgnoreCase))
            {
                return $"ZATCA device row environment '{device.Environment}' does not match settings '{environment}'. " +
                       "Save api_environment in ZATCA settings, then re-onboard and request Production CSID for that environment.";
            }

            return null;
        }

        private async Task<bool> ProcessInvoiceAsync(
            Invoice invoice,
            ZatcaDetails settings,
            ZatcaDevice device,
            CancellationToken cancellationToken)
        {
            try
            {
                var profile = await _profileResolver.ResolveForInvoiceAsync(invoice, cancellationToken);
                ApplyProfile(invoice, profile);
                _logger.LogInformation(
                    "[ZATCA] Invoice {InvoiceNo} reservationId={ReservationId} profile={Profile} mode={Mode} reservationType={ReservationType}",
                    invoice.InvoiceNo,
                    invoice.ReservationId,
                    profile.Profile,
                    profile.SubmissionMode,
                    profile.ReservationType ?? "(none)");

                if (string.IsNullOrWhiteSpace(invoice.ZatcaUuid))
                {
                    invoice.ZatcaUuid = Guid.NewGuid().ToString();
                }

                var environment = ZatcaDetailsEnvironmentSync.ResolveEffective(
                    settings.ApiEnvironment,
                    settings.Environment);
                var credentialError = ValidateSubmissionCredentials(device, environment);
                if (credentialError != null)
                {
                    await FailAsync(
                        invoice,
                        device,
                        ZatcaApiConstants.DocumentKindInvoice,
                        invoice.InvoiceId,
                        invoice.InvoiceNo,
                        null,
                        credentialError,
                        null,
                        cancellationToken);
                    return false;
                }

                var (csid, secret) = ResolveSubmissionCredentials(device, environment);
                if (csid == null)
                {
                    await FailAsync(
                        invoice,
                        device,
                        ZatcaApiConstants.DocumentKindInvoice,
                        invoice.InvoiceId,
                        invoice.InvoiceNo,
                        null,
                        "ZATCA device is not onboarded (missing CSID).",
                        null,
                        cancellationToken);
                    return false;
                }

                var icv = device.LastIcv + 1;
                var previousHash = ZatcaUblDocumentBuilder.NormalizePreviousHash(device.LastInvoiceHash);

                var build = await _ublBuilder.BuildAndSignInvoiceAsync(
                    invoice, settings, device, profile, previousHash, icv, cancellationToken);

                if (!build.Success)
                {
                    await FailAsync(
                        invoice,
                        device,
                        ZatcaApiConstants.DocumentKindInvoice,
                        invoice.InvoiceId,
                        invoice.InvoiceNo,
                        null,
                        build.ErrorMessage,
                        null,
                        cancellationToken);
                    return false;
                }

                var submit = await _gateway.SubmitAsync(
                    new ZatcaSubmissionRequest
                    {
                        Environment = ZatcaApiConstants.NormalizeEnvironment(ZatcaDetailsEnvironmentSync.ResolveEffective(settings.ApiEnvironment, settings.Environment)),
                        Profile = profile.Profile,
                        SubmissionMode = profile.SubmissionMode,
                        DocumentKind = ZatcaApiConstants.DocumentKindInvoice,
                        DocumentNo = invoice.InvoiceNo,
                        ZatcaUuid = invoice.ZatcaUuid!,
                        SignedXmlBase64 = build.SignedXmlBase64!,
                        InvoiceHash = build.InvoiceHash,
                        AcceptLanguage = _acceptLanguageResolver.ResolveAcceptLanguage()
                    },
                    csid,
                    secret!,
                    cancellationToken);

                return await CompleteAsync(
                    invoice,
                    device,
                    ZatcaApiConstants.DocumentKindInvoice,
                    invoice.InvoiceId,
                    invoice.InvoiceNo,
                    invoice.ZatcaUuid!,
                    icv,
                    build.InvoiceHash ?? "",
                    build.QrCode,
                    profile,
                    submit,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZATCA] Invoice {InvoiceNo} failed", invoice.InvoiceNo);
                invoice.ZatcaRetryCount++;
                invoice.ZatcaLastError = ex.Message;
                invoice.ZatcaStatus = ZatcaApiConstants.StatusFailed;
                await _db.SaveChangesAsync(cancellationToken);
                return false;
            }
        }

        private async Task<bool> ProcessCreditNoteAsync(
            CreditNote creditNote,
            ZatcaDetails settings,
            ZatcaDevice device,
            CancellationToken cancellationToken)
        {
            try
            {
                var profile = await _profileResolver.ResolveForReservationIdAsync(
                    creditNote.ReservationId,
                    creditNote.HotelId,
                    cancellationToken);
                ApplyProfile(creditNote, profile);

                if (string.IsNullOrWhiteSpace(creditNote.ZatcaUuid))
                {
                    creditNote.ZatcaUuid = Guid.NewGuid().ToString();
                }

                var original = await FindOriginalInvoiceAsync(creditNote.InvoiceId, cancellationToken);

                var environment = ZatcaDetailsEnvironmentSync.ResolveEffective(
                    settings.ApiEnvironment,
                    settings.Environment);
                var credentialError = ValidateSubmissionCredentials(device, environment);
                if (credentialError != null)
                {
                    await FailCreditNoteAsync(creditNote, credentialError, cancellationToken: cancellationToken);
                    return false;
                }

                var (csid, secret) = ResolveSubmissionCredentials(device, environment);
                if (csid == null)
                {
                    await FailCreditNoteAsync(
                        creditNote,
                        "ZATCA device is not onboarded (missing CSID).",
                        cancellationToken: cancellationToken);
                    return false;
                }

                var icv = device.LastIcv + 1;
                var previousHash = ZatcaUblDocumentBuilder.NormalizePreviousHash(device.LastInvoiceHash);

                var build = await _ublBuilder.BuildAndSignCreditNoteAsync(
                    creditNote, original, settings, device, profile, previousHash, icv, cancellationToken);

                if (!build.Success)
                {
                    await FailCreditNoteAsync(creditNote, build.ErrorMessage, cancellationToken: cancellationToken);
                    return false;
                }

                var submit = await _gateway.SubmitAsync(
                    new ZatcaSubmissionRequest
                    {
                        Environment = ZatcaApiConstants.NormalizeEnvironment(ZatcaDetailsEnvironmentSync.ResolveEffective(settings.ApiEnvironment, settings.Environment)),
                        Profile = profile.Profile,
                        SubmissionMode = profile.SubmissionMode,
                        DocumentKind = ZatcaApiConstants.DocumentKindCreditNote,
                        DocumentNo = creditNote.CreditNoteNo,
                        ZatcaUuid = creditNote.ZatcaUuid!,
                        SignedXmlBase64 = build.SignedXmlBase64!,
                        InvoiceHash = build.InvoiceHash,
                        AcceptLanguage = _acceptLanguageResolver.ResolveAcceptLanguage()
                    },
                    csid,
                    secret!,
                    cancellationToken);

                return await CompleteCreditNoteAsync(
                    creditNote,
                    device,
                    icv,
                    build.InvoiceHash ?? "",
                    build.QrCode,
                    profile,
                    submit,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZATCA] Credit note {No} failed", creditNote.CreditNoteNo);
                creditNote.ZatcaRetryCount++;
                creditNote.ZatcaLastError = ex.Message;
                creditNote.ZatcaStatus = ZatcaApiConstants.StatusFailed;
                await LogIntegrationAsync(
                    creditNote.HotelId,
                    creditNote.ReservationId,
                    ZatcaApiConstants.SubmitIntegrationEventType(ZatcaApiConstants.DocumentKindCreditNote),
                    "Error",
                    IntegrationResponseLogHelper.TruncateErrorMessage(ex.Message),
                    null,
                    null,
                    cancellationToken,
                    creditNote.ZaaerId);
                await _db.SaveChangesAsync(cancellationToken);
                return false;
            }
        }

        private async Task<bool> ProcessDebitNoteAsync(
            DebitNote debitNote,
            ZatcaDetails settings,
            ZatcaDevice device,
            CancellationToken cancellationToken)
        {
            try
            {
                var profile = await _profileResolver.ResolveForReservationIdAsync(
                    debitNote.ReservationId,
                    debitNote.HotelId,
                    cancellationToken);
                ApplyProfile(debitNote, profile);

                if (string.IsNullOrWhiteSpace(debitNote.ZatcaUuid))
                {
                    debitNote.ZatcaUuid = Guid.NewGuid().ToString();
                }

                var original = await FindOriginalInvoiceAsync(debitNote.InvoiceId, cancellationToken);

                var environment = ZatcaDetailsEnvironmentSync.ResolveEffective(
                    settings.ApiEnvironment,
                    settings.Environment);
                var credentialError = ValidateSubmissionCredentials(device, environment);
                if (credentialError != null)
                {
                    await FailDebitNoteAsync(debitNote, credentialError, cancellationToken: cancellationToken);
                    return false;
                }

                var (csid, secret) = ResolveSubmissionCredentials(device, environment);
                if (csid == null)
                {
                    await FailDebitNoteAsync(
                        debitNote,
                        "ZATCA device is not onboarded (missing CSID).",
                        cancellationToken: cancellationToken);
                    return false;
                }

                var icv = device.LastIcv + 1;
                var previousHash = device.LastInvoiceHash ?? "";

                var build = await _ublBuilder.BuildAndSignDebitNoteAsync(
                    debitNote, original, settings, device, profile, previousHash, icv, cancellationToken);

                if (!build.Success)
                {
                    await FailDebitNoteAsync(debitNote, build.ErrorMessage, cancellationToken: cancellationToken);
                    return false;
                }

                var submit = await _gateway.SubmitAsync(
                    new ZatcaSubmissionRequest
                    {
                        Environment = ZatcaApiConstants.NormalizeEnvironment(ZatcaDetailsEnvironmentSync.ResolveEffective(settings.ApiEnvironment, settings.Environment)),
                        Profile = profile.Profile,
                        SubmissionMode = profile.SubmissionMode,
                        DocumentKind = ZatcaApiConstants.DocumentKindDebitNote,
                        DocumentNo = debitNote.DebitNoteNo,
                        ZatcaUuid = debitNote.ZatcaUuid!,
                        SignedXmlBase64 = build.SignedXmlBase64!,
                        InvoiceHash = build.InvoiceHash,
                        AcceptLanguage = _acceptLanguageResolver.ResolveAcceptLanguage()
                    },
                    csid,
                    secret!,
                    cancellationToken);

                return await CompleteDebitNoteAsync(
                    debitNote,
                    device,
                    icv,
                    build.InvoiceHash ?? "",
                    build.QrCode,
                    profile,
                    submit,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZATCA] Debit note {No} failed", debitNote.DebitNoteNo);
                debitNote.ZatcaRetryCount++;
                debitNote.ZatcaLastError = ex.Message;
                debitNote.ZatcaStatus = ZatcaApiConstants.StatusFailed;
                await LogIntegrationAsync(
                    debitNote.HotelId,
                    debitNote.ReservationId,
                    ZatcaApiConstants.SubmitIntegrationEventType(ZatcaApiConstants.DocumentKindDebitNote),
                    "Error",
                    IntegrationResponseLogHelper.TruncateErrorMessage(ex.Message),
                    null,
                    null,
                    cancellationToken,
                    debitNote.ZaaerId);
                await _db.SaveChangesAsync(cancellationToken);
                return false;
            }
        }

        private async Task<Invoice?> FindOriginalInvoiceAsync(int invoiceRef, CancellationToken cancellationToken)
        {
            var byZaaer = await _db.Invoices
                .FirstOrDefaultAsync(i => i.ZaaerId == invoiceRef, cancellationToken);
            if (byZaaer != null)
            {
                return byZaaer;
            }

            return await _db.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceRef, cancellationToken);
        }

        /// <summary>
        /// Reporting/clearance APIs require Production CSID. Compliance CSID is only for /compliance/invoices checks.
        /// </summary>
        private static (string? Csid, string? Secret) ResolveSubmissionCredentials(ZatcaDevice device, string? environment)
        {
            if (!string.IsNullOrWhiteSpace(device.ProductionCsid) && !string.IsNullOrWhiteSpace(device.ProductionSecret))
            {
                return (device.ProductionCsid.Trim(), device.ProductionSecret.Trim());
            }

            var env = ZatcaApiConstants.NormalizeEnvironment(environment);
            if (env == "sandbox"
                && !string.IsNullOrWhiteSpace(device.ComplianceCsid)
                && !string.IsNullOrWhiteSpace(device.ComplianceSecret))
            {
                return (device.ComplianceCsid.Trim(), device.ComplianceSecret.Trim());
            }

            return (null, null);
        }

        private static string? ValidateSubmissionCredentials(ZatcaDevice device, string? environment)
        {
            var env = ZatcaApiConstants.NormalizeEnvironment(environment);
            var hasProduction = !string.IsNullOrWhiteSpace(device.ProductionCsid)
                                && !string.IsNullOrWhiteSpace(device.ProductionSecret);

            if (env is "simulation" or "production" && !hasProduction)
            {
                return "Production CSID is required to submit invoices. In Integrations → ZATCA, run the six compliance tests, then request Production CSID.";
            }

            if (env == "sandbox"
                && !hasProduction
                && (string.IsNullOrWhiteSpace(device.ComplianceCsid) || string.IsNullOrWhiteSpace(device.ComplianceSecret)))
            {
                return "ZATCA device is not onboarded (missing CSID).";
            }

            return null;
        }

        private static void ApplyProfile(Invoice invoice, ZatcaProfileResolution profile)
        {
            invoice.ZatcaProfile = profile.Profile;
            invoice.ZatcaSubmissionMode = profile.SubmissionMode;
        }

        private static void ApplyProfile(CreditNote cn, ZatcaProfileResolution profile)
        {
            cn.ZatcaProfile = profile.Profile;
            cn.ZatcaSubmissionMode = profile.SubmissionMode;
        }

        private static void ApplyProfile(DebitNote dn, ZatcaProfileResolution profile)
        {
            dn.ZatcaProfile = profile.Profile;
            dn.ZatcaSubmissionMode = profile.SubmissionMode;
        }

        private async Task<bool> CompleteAsync(
            Invoice invoice,
            ZatcaDevice device,
            string documentKind,
            int documentId,
            string documentNo,
            string zatcaUuid,
            int icv,
            string invoiceHash,
            string? qrCode,
            ZatcaProfileResolution profile,
            ZatcaSubmissionResult submit,
            CancellationToken cancellationToken)
        {
            if (!submit.Success)
            {
                await FailAsync(
                    invoice,
                    device,
                    documentKind,
                    documentId,
                    documentNo,
                    submit.HttpStatusCode,
                    submit.ErrorMessage,
                    submit.ResponseBody,
                    cancellationToken);
                return false;
            }

            invoice.ZatcaStatus = string.Equals(profile.SubmissionMode, ZatcaApiConstants.ModeClearance, StringComparison.OrdinalIgnoreCase)
                ? ZatcaApiConstants.StatusCleared
                : ZatcaApiConstants.StatusReported;
            invoice.IsSentZatca = true;
            invoice.ZatcaIcv = icv;
            invoice.ZatcaHash = invoiceHash;
            invoice.ZatcaQr = submit.QrBase64 ?? qrCode;
            invoice.ZatcaResponse = submit.ResponseBody;
            invoice.ZatcaSentAt = KsaTime.Now;
            invoice.ZatcaLastError = null;

            device.LastIcv = icv;
            device.LastInvoiceHash = invoiceHash;
            device.UpdatedAt = KsaTime.Now;

            _db.ZatcaInvoiceHashHistory.Add(new ZatcaInvoiceHashHistory
            {
                DeviceId = device.DeviceId,
                HotelId = invoice.HotelId,
                DocumentKind = documentKind,
                DocumentId = documentId,
                DocumentNo = documentNo,
                Icv = icv,
                InvoiceHash = invoiceHash,
                ZatcaUuid = zatcaUuid,
                CreatedAt = KsaTime.Now
            });

            await LogIntegrationAsync(
                invoice.HotelId,
                invoice.ReservationId,
                ZatcaApiConstants.SubmitIntegrationEventType(documentKind),
                "Success",
                null,
                submit.HttpStatusCode,
                submit.ResponseBody,
                cancellationToken,
                invoice.ZaaerId);

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task FailAsync(
            Invoice invoice,
            ZatcaDevice device,
            string documentKind,
            int documentId,
            string documentNo,
            int? httpStatus,
            string? error,
            string? responseBody,
            CancellationToken cancellationToken)
        {
            invoice.ZatcaRetryCount++;
            invoice.ZatcaStatus = ZatcaApiConstants.StatusFailed;
            invoice.ZatcaLastError = error;

            await LogIntegrationAsync(
                invoice.HotelId,
                invoice.ReservationId,
                ZatcaApiConstants.SubmitIntegrationEventType(documentKind),
                "Error",
                error,
                httpStatus,
                responseBody,
                cancellationToken,
                invoice.ZaaerId);

            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> CompleteCreditNoteAsync(
            CreditNote cn,
            ZatcaDevice device,
            int icv,
            string invoiceHash,
            string? qrCode,
            ZatcaProfileResolution profile,
            ZatcaSubmissionResult submit,
            CancellationToken cancellationToken)
        {
            if (!submit.Success)
            {
                await FailCreditNoteAsync(
                    cn,
                    submit.ErrorMessage,
                    submit.HttpStatusCode,
                    submit.ResponseBody,
                    cancellationToken);
                return false;
            }

            cn.ZatcaStatus = string.Equals(profile.SubmissionMode, ZatcaApiConstants.ModeClearance, StringComparison.OrdinalIgnoreCase)
                ? ZatcaApiConstants.StatusCleared
                : ZatcaApiConstants.StatusReported;
            cn.IsSentZatca = true;
            cn.ZatcaIcv = icv;
            cn.ZatcaHash = invoiceHash;
            cn.ZatcaQr = submit.QrBase64 ?? qrCode;
            cn.ZatcaResponse = submit.ResponseBody;
            cn.ZatcaSentAt = KsaTime.Now;
            cn.ZatcaLastError = null;

            device.LastIcv = icv;
            device.LastInvoiceHash = invoiceHash;
            device.UpdatedAt = KsaTime.Now;

            _db.ZatcaInvoiceHashHistory.Add(new ZatcaInvoiceHashHistory
            {
                DeviceId = device.DeviceId,
                HotelId = cn.HotelId,
                DocumentKind = ZatcaApiConstants.DocumentKindCreditNote,
                DocumentId = cn.CreditNoteId,
                DocumentNo = cn.CreditNoteNo,
                Icv = icv,
                InvoiceHash = invoiceHash,
                ZatcaUuid = cn.ZatcaUuid!,
                CreatedAt = KsaTime.Now
            });

            await LogIntegrationAsync(
                cn.HotelId,
                cn.ReservationId,
                ZatcaApiConstants.SubmitIntegrationEventType(ZatcaApiConstants.DocumentKindCreditNote),
                "Success",
                null,
                submit.HttpStatusCode,
                submit.ResponseBody,
                cancellationToken,
                cn.ZaaerId);

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task FailCreditNoteAsync(
            CreditNote cn,
            string? error,
            int? httpStatus = null,
            string? responseBody = null,
            CancellationToken cancellationToken = default)
        {
            cn.ZatcaRetryCount++;
            cn.ZatcaStatus = ZatcaApiConstants.StatusFailed;
            cn.ZatcaLastError = error;

            await LogIntegrationAsync(
                cn.HotelId,
                cn.ReservationId,
                ZatcaApiConstants.SubmitIntegrationEventType(ZatcaApiConstants.DocumentKindCreditNote),
                "Error",
                IntegrationResponseLogHelper.TruncateErrorMessage(error),
                httpStatus,
                responseBody,
                cancellationToken,
                cn.ZaaerId);

            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> CompleteDebitNoteAsync(
            DebitNote dn,
            ZatcaDevice device,
            int icv,
            string invoiceHash,
            string? qrCode,
            ZatcaProfileResolution profile,
            ZatcaSubmissionResult submit,
            CancellationToken cancellationToken)
        {
            if (!submit.Success)
            {
                await FailDebitNoteAsync(
                    dn,
                    submit.ErrorMessage,
                    submit.HttpStatusCode,
                    submit.ResponseBody,
                    cancellationToken);
                return false;
            }

            dn.ZatcaStatus = string.Equals(profile.SubmissionMode, ZatcaApiConstants.ModeClearance, StringComparison.OrdinalIgnoreCase)
                ? ZatcaApiConstants.StatusCleared
                : ZatcaApiConstants.StatusReported;
            dn.IsSentZatca = true;
            dn.ZatcaIcv = icv;
            dn.ZatcaHash = invoiceHash;
            dn.ZatcaQr = submit.QrBase64 ?? qrCode;
            dn.ZatcaResponse = submit.ResponseBody;
            dn.ZatcaSentAt = KsaTime.Now;
            dn.ZatcaLastError = null;

            device.LastIcv = icv;
            device.LastInvoiceHash = invoiceHash;
            device.UpdatedAt = KsaTime.Now;

            _db.ZatcaInvoiceHashHistory.Add(new ZatcaInvoiceHashHistory
            {
                DeviceId = device.DeviceId,
                HotelId = dn.HotelId,
                DocumentKind = ZatcaApiConstants.DocumentKindDebitNote,
                DocumentId = dn.DebitNoteId,
                DocumentNo = dn.DebitNoteNo,
                Icv = icv,
                InvoiceHash = invoiceHash,
                ZatcaUuid = dn.ZatcaUuid!,
                CreatedAt = KsaTime.Now
            });

            await LogIntegrationAsync(
                dn.HotelId,
                dn.ReservationId,
                ZatcaApiConstants.SubmitIntegrationEventType(ZatcaApiConstants.DocumentKindDebitNote),
                "Success",
                null,
                submit.HttpStatusCode,
                submit.ResponseBody,
                cancellationToken,
                dn.ZaaerId);

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task FailDebitNoteAsync(
            DebitNote dn,
            string? error,
            int? httpStatus = null,
            string? responseBody = null,
            CancellationToken cancellationToken = default)
        {
            dn.ZatcaRetryCount++;
            dn.ZatcaStatus = ZatcaApiConstants.StatusFailed;
            dn.ZatcaLastError = error;

            await LogIntegrationAsync(
                dn.HotelId,
                dn.ReservationId,
                ZatcaApiConstants.SubmitIntegrationEventType(ZatcaApiConstants.DocumentKindDebitNote),
                "Error",
                IntegrationResponseLogHelper.TruncateErrorMessage(error),
                httpStatus,
                responseBody,
                cancellationToken,
                dn.ZaaerId);

            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task LogIntegrationAsync(
            int hotelId,
            int? reservationId,
            string eventType,
            string status,
            string? error,
            int? httpStatus,
            string? responsePayload,
            CancellationToken cancellationToken,
            int? zaaerId = null)
        {
            string? resNo = null;
            if (reservationId is > 0)
            {
                var reservation = await ZatcaReservationLinkage.FindReservationAsync(
                    _db,
                    reservationId.Value,
                    hotelId,
                    cancellationToken);
                resNo = reservation?.ReservationNo;
            }

            _db.IntegrationResponses.Add(new IntegrationResponse
            {
                HotelId = hotelId,
                ResNo = resNo,
                Service = ZatcaApiConstants.ServiceName,
                EventType = eventType,
                Status = status,
                ErrorMessage = IntegrationResponseLogHelper.TruncateErrorMessage(error),
                HttpStatusCode = httpStatus,
                ResponsePayload = responsePayload,
                ZaaerId = zaaerId,
                CreatedAt = KsaTime.Now
            });
        }
    }
}
