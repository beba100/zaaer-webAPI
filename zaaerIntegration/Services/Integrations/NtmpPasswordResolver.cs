using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Integrations
{
    public sealed class NtmpPasswordResolution
    {
        public string? Password { get; init; }
        public string? ErrorMessage { get; init; }
        public bool IsConfigured => !string.IsNullOrEmpty(Password);
    }

    public interface INtmpPasswordResolver
    {
        NtmpPasswordResolution Resolve(NtmpDetails settings);
    }

    public sealed class NtmpPasswordResolver : INtmpPasswordResolver
    {
        private readonly IIntegrationSecretProtector _secretProtector;

        public NtmpPasswordResolver(IIntegrationSecretProtector secretProtector)
        {
            _secretProtector = secretProtector;
        }

        public NtmpPasswordResolution Resolve(NtmpDetails settings)
        {
            if (settings.PasswordEncrypted == null || settings.PasswordEncrypted.Length == 0)
            {
                return new NtmpPasswordResolution
                {
                    ErrorMessage =
                        "NTMP password is not saved in Aleairy PMS. Open Tourism settings, enter the same password as Zaaer NTMP (username + password), then Save."
                };
            }

            var password = _secretProtector.Unprotect(settings.PasswordEncrypted);
            if (string.IsNullOrEmpty(password))
            {
                return new NtmpPasswordResolution
                {
                    ErrorMessage =
                        "NTMP password cannot be decrypted. Re-enter the password in Tourism settings and Save (server encryption keys may have reset after deploy)."
                };
            }

            return new NtmpPasswordResolution { Password = password };
        }
    }
}
