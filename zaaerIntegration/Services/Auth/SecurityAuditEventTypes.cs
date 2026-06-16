namespace zaaerIntegration.Services.Auth
{
    public static class SecurityAuditEventTypes
    {
        public const string Login = "Login";
        public const string LoginFailed = "LoginFailed";
        public const string Logout = "Logout";
        public const string Refresh = "Refresh";
        public const string SessionRevoked = "SessionRevoked";
        public const string ForceLogout = "ForceLogout";
        public const string UserLocked = "UserLocked";
        public const string UserUnlocked = "UserUnlocked";
        public const string PasswordChanged = "PasswordChanged";
    }
}
