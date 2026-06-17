using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Integrations
{
    public sealed class NtmpGatewayClient : INtmpGatewayClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public NtmpGatewayClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public Task<NtmpGatewayResponse> CreateOrUpdateBookingAsync(
            NtmpDetails settings,
            string password,
            NtmpCreateOrUpdateBookingRequest request,
            CancellationToken cancellationToken = default) =>
            PostAsync(
                settings,
                password,
                NtmpApiConstants.CreateOrUpdateBookingUrl(settings.ApiEnvironment),
                request,
                NtmpErrorCodeMapper.ApiCreateOrUpdate,
                cancellationToken);

        public Task<NtmpGatewayResponse> CancelBookingAsync(
            NtmpDetails settings,
            string password,
            NtmpCancelBookingRequest request,
            CancellationToken cancellationToken = default) =>
            PostAsync(
                settings,
                password,
                NtmpApiConstants.CancelBookingUrl(settings.ApiEnvironment),
                request,
                NtmpErrorCodeMapper.ApiCancelBooking,
                cancellationToken);

        public Task<NtmpGatewayResponse> BookingExpenseAsync(
            NtmpDetails settings,
            string password,
            NtmpBookingExpenseRequest request,
            CancellationToken cancellationToken = default) =>
            PostAsync(
                settings,
                password,
                NtmpApiConstants.BookingExpenseUrl(settings.ApiEnvironment),
                request,
                NtmpErrorCodeMapper.ApiBookingExpense,
                cancellationToken);

        public Task<NtmpGatewayResponse> OccupancyUpdateAsync(
            NtmpDetails settings,
            string password,
            NtmpOccupancyUpdateRequest request,
            CancellationToken cancellationToken = default) =>
            PostAsync(
                settings,
                password,
                NtmpApiConstants.OccupancyUpdateUrl(settings.ApiEnvironment),
                request,
                NtmpErrorCodeMapper.ApiOccupancyUpdate,
                cancellationToken);

        public Task<NtmpGatewayResponse> GetTransactionIdByBookingNoAsync(
            NtmpDetails settings,
            string password,
            NtmpGetTransactionIdRequest request,
            CancellationToken cancellationToken = default) =>
            PostAsync(
                settings,
                password,
                NtmpApiConstants.GetTransactionIdUrl(settings.ApiEnvironment),
                request,
                NtmpErrorCodeMapper.ApiCreateOrUpdate,
                cancellationToken);

        private async Task<NtmpGatewayResponse> PostAsync(
            NtmpDetails settings,
            string password,
            string url,
            object request,
            string apiName,
            CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var client = _httpClientFactory.CreateClient("NtmpGateway");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(settings.GatewayApiKey))
            {
                httpRequest.Headers.TryAddWithoutValidation("x-Gateway-APIKey", settings.GatewayApiKey);
            }

            if (!string.IsNullOrWhiteSpace(settings.UserName))
            {
                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{password}"));
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            }

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(httpRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                return new NtmpGatewayResponse
                {
                    Success = false,
                    HttpStatusCode = 0,
                    RawRequest = json,
                    ErrorMessage = ex.Message
                };
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = ParseResponse(body, (int)response.StatusCode, apiName);
            parsed.RawRequest = json;
            parsed.RawResponse = body;
            parsed.HttpStatusCode = (int)response.StatusCode;
            return parsed;
        }

        internal static NtmpGatewayResponse ParseResponse(string body, int httpStatusCode, string apiName = NtmpErrorCodeMapper.ApiOccupancyUpdate)
        {
            var result = new NtmpGatewayResponse { HttpStatusCode = httpStatusCode, RawResponse = body };
            if (string.IsNullOrWhiteSpace(body))
            {
                result.Success = httpStatusCode is >= 200 and < 300;
                result.ErrorMessage = result.Success ? null : "Empty response from NTMP gateway.";
                return result;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("correlationId", out var corr))
                {
                    result.CorrelationId = corr.GetString();
                }

                if (root.TryGetProperty("transactionId", out var tx))
                {
                    result.TransactionId = tx.ValueKind == JsonValueKind.String
                        ? tx.GetString()
                        : tx.ToString();
                }

                if (root.TryGetProperty("errorCode", out var codes) && codes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in codes.EnumerateArray())
                    {
                        var code = c.GetString();
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            result.ErrorCodes.Add(code);
                        }
                    }
                }

                var hasOnlyZero = result.ErrorCodes.Count == 0
                    || (result.ErrorCodes.Count == 1 && result.ErrorCodes[0] == "0");
                result.Success = httpStatusCode is >= 200 and < 300 && hasOnlyZero;
                if (!result.Success)
                {
                    result.ErrorMessage = result.ErrorCodes.Count > 0
                        ? NtmpErrorCodeMapper.DescribeMany(result.ErrorCodes, apiName)
                        : "NTMP gateway returned an error.";
                }
            }
            catch
            {
                result.Success = httpStatusCode is >= 200 and < 300;
                result.ErrorMessage = result.Success ? null : body;
            }

            return result;
        }
    }
}
