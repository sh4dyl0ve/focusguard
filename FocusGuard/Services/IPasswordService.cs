using FocusGuard.Config;

namespace FocusGuard.Services;

public interface IPasswordService
{
    bool HasPassword(AppSettings settings);
    bool Verify(AppSettings settings, string password);
    void SetPassword(AppSettings settings, string password);
    void MigrateLegacyPassword(AppSettings settings);
}
