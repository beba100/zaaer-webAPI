using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using zaaerIntegration.Configuration;

namespace zaaerIntegration.Services.Integrations.Zatca
{
    public sealed class ZatcaAcceptLanguageResolver : IZatcaAcceptLanguageResolver
    {
        public const string UiCultureHeaderName = "X-Ui-Culture";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _fallbackAcceptLanguage;

        public ZatcaAcceptLanguageResolver(
            IHttpContextAccessor httpContextAccessor,
            IOptions<ZatcaOptions> options)
        {
            _httpContextAccessor = httpContextAccessor;
            _fallbackAcceptLanguage = ZatcaApiConstants.NormalizeAcceptLanguage(options.Value.AcceptLanguage);
        }

        public string ResolveAcceptLanguage()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                if (context.Request.Headers.TryGetValue(UiCultureHeaderName, out var uiCulture))
                {
                    return ZatcaApiConstants.NormalizeAcceptLanguage(uiCulture.ToString());
                }

                if (context.Request.Headers.TryGetValue("Accept-Language", out var acceptLanguage))
                {
                    var first = acceptLanguage.ToString().Split(',')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(first))
                    {
                        return ZatcaApiConstants.NormalizeAcceptLanguage(first);
                    }
                }
            }

            return _fallbackAcceptLanguage;
        }
    }
}
