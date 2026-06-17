using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using zaaerIntegration.Configuration;
using Zatca.EInvoice.Api;
using Zatca.EInvoice.Exceptions;

namespace zaaerIntegration.Services.Integrations.Zatca
{
    /// <summary>
    /// Submits signed invoices via Zatca.EInvoice <see cref="ZatcaApiClient"/> (reporting/clearance).
    /// </summary>
    public sealed class ZatcaGatewayClient : IZatcaGatewayClient
    {
        private readonly ILogger<ZatcaGatewayClient> _logger;
        private readonly string _fallbackAcceptLanguage;

        public ZatcaGatewayClient(
            ILogger<ZatcaGatewayClient> logger,
            IOptions<ZatcaOptions> options)
        {
            _logger = logger;
            _fallbackAcceptLanguage = ZatcaApiConstants.NormalizeAcceptLanguage(options.Value.AcceptLanguage);
        }

        public async Task<ZatcaSubmissionResult> SubmitAsync(
            ZatcaSubmissionRequest request,
            string csid,
            string secret,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.SignedXmlBase64))
            {
                return new ZatcaSubmissionResult
                {
                    Success = false,
                    ErrorMessage = "Signed XML is required before submission."
                };
            }

            var signedXml = DecodeSignedPayload(request.SignedXmlBase64);
            if (string.IsNullOrWhiteSpace(signedXml))
            {
                return new ZatcaSubmissionResult
                {
                    Success = false,
                    ErrorMessage = "Signed XML payload is empty or invalid."
                };
            }

            var csidToken = csid.Trim();
            var secretValue = secret.Trim();
            var acceptLanguage = ZatcaApiConstants.NormalizeAcceptLanguage(
                request.AcceptLanguage ?? _fallbackAcceptLanguage);
            var apiEnvironment = ZatcaEInvoiceEnvironmentMapper.ToApiEnvironment(request.Environment);
            var isClearance = string.Equals(
                request.SubmissionMode,
                ZatcaApiConstants.ModeClearance,
                StringComparison.OrdinalIgnoreCase);

            // Use Handlers (not HttpClient): custom HttpClient skips BaseAddress setup in Zatca.EInvoice.
            using var apiClient = new ZatcaApiClient(new ZatcaApiClientOptions
            {
                Environment = apiEnvironment,
                Timeout = TimeSpan.FromSeconds(60),
                Handlers = new List<DelegatingHandler>
                {
                    new ZatcaApiLanguageHandler(acceptLanguage)
                }
            });

            try
            {
                var hash = request.InvoiceHash ?? string.Empty;
                var uuid = request.ZatcaUuid;

                InvoiceSubmissionResult result = isClearance
                    ? await apiClient.SubmitClearanceInvoiceAsync(
                        signedXml,
                        hash,
                        uuid,
                        csidToken,
                        secretValue,
                        cancellationToken)
                    : await apiClient.SubmitReportingInvoiceAsync(
                        signedXml,
                        hash,
                        uuid,
                        csidToken,
                        secretValue,
                        cancellationToken);

                var responseBody = SerializeSubmissionResult(result);

                if (!result.IsSuccess)
                {
                    var errorMessage = BuildSubmissionErrorMessage(result);
                    _logger.LogWarning(
                        "[ZATCA] Submit failed {Mode} {DocNo} env={Env}: {Error}",
                        request.SubmissionMode,
                        request.DocumentNo,
                        request.Environment,
                        errorMessage);

                    return new ZatcaSubmissionResult
                    {
                        Success = false,
                        HttpStatusCode = 400,
                        ResponseBody = responseBody,
                        ErrorMessage = errorMessage
                    };
                }

                _logger.LogInformation(
                    "[ZATCA] Submit OK {Mode} {DocNo} env={Env} status={Status}",
                    request.SubmissionMode,
                    request.DocumentNo,
                    request.Environment,
                    result.ClearanceStatus ?? result.ReportingStatus ?? result.Status);

                return new ZatcaSubmissionResult
                {
                    Success = true,
                    HttpStatusCode = 200,
                    ResponseBody = responseBody,
                    ClearanceStatus = result.ClearanceStatus ?? result.ReportingStatus ?? result.Status
                };
            }
            catch (ZatcaApiException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[ZATCA] Submit API exception {Mode} {DocNo} env={Env}",
                    request.SubmissionMode,
                    request.DocumentNo,
                    request.Environment);

                return new ZatcaSubmissionResult
                {
                    Success = false,
                    HttpStatusCode = ex.StatusCode,
                    ResponseBody = ex.Response,
                    ErrorMessage = BuildHttpErrorMessage(ex.StatusCode, ex.Response, ex.Message)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZATCA] Submit exception for {DocNo}", request.DocumentNo);
                return new ZatcaSubmissionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static string DecodeSignedPayload(string payload)
        {
            try
            {
                var bytes = Convert.FromBase64String(payload.Trim());
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return payload;
            }
        }

        private static string SerializeSubmissionResult(InvoiceSubmissionResult result)
        {
            try
            {
                return JsonSerializer.Serialize(new
                {
                    result.Status,
                    result.ClearanceStatus,
                    result.ReportingStatus,
                    result.IsSuccess,
                    result.IsClearance,
                    result.IsReporting,
                    errors = result.Errors?.Select(e => new { e.Code, e.Message, e.Category }),
                    warnings = result.Warnings?.Select(e => new { e.Code, e.Message, e.Category })
                });
            }
            catch
            {
                return result.Status ?? string.Empty;
            }
        }

        private static string BuildSubmissionErrorMessage(InvoiceSubmissionResult result)
        {
            if (result.Errors is { Count: > 0 })
            {
                return string.Join(
                    "; ",
                    result.Errors
                        .Where(e => !string.IsNullOrWhiteSpace(e.Message))
                        .Select(e => string.IsNullOrWhiteSpace(e.Code) ? e.Message : $"[{e.Code}] {e.Message}"));
            }

            return result.Status ?? "ZATCA submission failed.";
        }

        private static string BuildHttpErrorMessage(int? statusCode, string? body, string? fallback)
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Reason", out var reason) && reason.ValueKind == JsonValueKind.String)
                    {
                        return $"ZATCA HTTP {statusCode}: {reason.GetString()}";
                    }

                    if (root.TryGetProperty("Message", out var message) && message.ValueKind == JsonValueKind.String)
                    {
                        return $"ZATCA HTTP {statusCode}: {message.GetString()}";
                    }
                }
                catch (JsonException)
                {
                    // ignore
                }
            }

            if (statusCode == 401)
            {
                return "ZATCA HTTP 401 — unauthorized. Re-request Production CSID for the same environment (Simulation), then submit again.";
            }

            return !string.IsNullOrWhiteSpace(fallback)
                ? fallback
                : $"ZATCA HTTP {statusCode}";
        }
    }
}
