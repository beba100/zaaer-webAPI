namespace zaaerIntegration.Services.Integrations.Zatca
{
    /// <summary>
    /// Injects Fatoora <c>Accept-Language</c> on every ZATCA HTTP call.
    /// </summary>
    internal sealed class ZatcaApiLanguageHandler : DelegatingHandler
    {
        private readonly string _acceptLanguage;

        public ZatcaApiLanguageHandler(string acceptLanguage)
        {
            _acceptLanguage = acceptLanguage;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", _acceptLanguage);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
