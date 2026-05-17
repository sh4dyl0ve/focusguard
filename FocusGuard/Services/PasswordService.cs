using System.Security.Cryptography;
using FocusGuard.Config;

namespace FocusGuard.Services;

public sealed class PasswordService : IPasswordService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DefaultIterations = 120_000;

    public bool HasPassword(AppSettings settings)
    {
        MigrateLegacyPassword(settings);
        return !string.IsNullOrWhiteSpace(settings.DisablePasswordHash)
            && !string.IsNullOrWhiteSpace(settings.DisablePasswordSalt);
    }

    public bool Verify(AppSettings settings, string password)
    {
        MigrateLegacyPassword(settings);
        if (!HasPassword(settings))
        {
            return true;
        }

        try
        {
            var salt = Convert.FromBase64String(settings.DisablePasswordSalt);
            var expectedHash = Convert.FromBase64String(settings.DisablePasswordHash);
            var actualHash = HashPassword(password, salt, GetIterations(settings));
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    public void SetPassword(AppSettings settings, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            settings.DisablePassword = string.Empty;
            settings.DisablePasswordHash = string.Empty;
            settings.DisablePasswordSalt = string.Empty;
            settings.DisablePasswordIterations = DefaultIterations;
            return;
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = HashPassword(password, salt, DefaultIterations);
        settings.DisablePassword = string.Empty;
        settings.DisablePasswordSalt = Convert.ToBase64String(salt);
        settings.DisablePasswordHash = Convert.ToBase64String(hash);
        settings.DisablePasswordIterations = DefaultIterations;
    }

    public void MigrateLegacyPassword(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DisablePassword)
            || !string.IsNullOrWhiteSpace(settings.DisablePasswordHash))
        {
            return;
        }

        SetPassword(settings, settings.DisablePassword);
    }

    private static int GetIterations(AppSettings settings)
    {
        return settings.DisablePasswordIterations > 0 ? settings.DisablePasswordIterations : DefaultIterations;
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);
    }
}
