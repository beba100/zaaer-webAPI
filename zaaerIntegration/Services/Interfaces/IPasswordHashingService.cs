namespace zaaerIntegration.Services.Interfaces
{
    public interface IPasswordHashingService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string storedHash);
        bool NeedsRehash(string storedHash);
    }
}
