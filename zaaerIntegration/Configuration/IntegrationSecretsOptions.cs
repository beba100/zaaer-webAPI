namespace zaaerIntegration.Configuration
{
    /// <summary>
    /// Durable encryption for integration secrets (ZATCA private keys, NTMP passwords).
    /// When <see cref="MasterKey"/> is set, secrets survive app redeploy without relying on Data Protection key ring files.
    /// </summary>
    public sealed class IntegrationSecretsOptions
    {
        public const string SectionName = "IntegrationSecrets";

        /// <summary>
        /// Base64-encoded 256-bit AES key. Set via configuration or environment variable
        /// <c>IntegrationSecrets__MasterKey</c> on production hosts.
        /// </summary>
        public string? MasterKey { get; set; }
    }
}
