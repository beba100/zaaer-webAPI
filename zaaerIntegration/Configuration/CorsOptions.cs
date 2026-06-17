namespace zaaerIntegration.Configuration
{
    public sealed class CorsOptions
    {
        public const string SectionName = "Cors";

        public string[] AllowedOrigins { get; set; } = [];

        public bool AllowAnyOriginInDevelopment { get; set; } = true;
    }
}
