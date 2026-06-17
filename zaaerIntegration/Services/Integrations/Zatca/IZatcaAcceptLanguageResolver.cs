namespace zaaerIntegration.Services.Integrations.Zatca
{
    /// <summary>
    /// Resolves ZATCA Fatoora <c>Accept-Language</c> (EN/AR) from the current UI culture when available.
    /// </summary>
    public interface IZatcaAcceptLanguageResolver
    {
        string ResolveAcceptLanguage();
    }
}
