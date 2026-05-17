namespace FocusGuard.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    event EventHandler? LanguageChanged;
    void SetLanguage(string language);
    string Translate(string key);
}
