using System.Windows;
using WpfApplication = System.Windows.Application;

namespace FocusGuard.Services;

public sealed class LocalizationService : ILocalizationService
{
    public const string English = "en";
    public const string Russian = "ru";
    public const string ChineseSimplified = "zh-Hans";

    private const string DictionaryPrefix = "Styles/Strings.";
    private string _currentLanguage = English;

    public string CurrentLanguage => _currentLanguage;

    public event EventHandler? LanguageChanged;

    public void SetLanguage(string language)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        if (_currentLanguage == normalizedLanguage && HasLanguageDictionary(normalizedLanguage))
        {
            return;
        }

        var appResources = WpfApplication.Current?.Resources;
        if (appResources is null)
        {
            _currentLanguage = normalizedLanguage;
            return;
        }

        RemoveExistingLanguageDictionary(appResources);
        appResources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"{DictionaryPrefix}{normalizedLanguage}.xaml", UriKind.Relative)
        });

        _currentLanguage = normalizedLanguage;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Translate(string key)
    {
        return WpfApplication.Current?.TryFindResource(key)?.ToString() ?? key;
    }

    private static string NormalizeLanguage(string language)
    {
        return language switch
        {
            Russian => Russian,
            ChineseSimplified or "zh" or "zh-CN" => ChineseSimplified,
            _ => English
        };
    }

    private static bool HasLanguageDictionary(string language)
    {
        return WpfApplication.Current?.Resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source?.OriginalString.Contains($"{DictionaryPrefix}{language}.xaml", StringComparison.OrdinalIgnoreCase) == true) == true;
    }

    private static void RemoveExistingLanguageDictionary(ResourceDictionary appResources)
    {
        for (var index = appResources.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            var source = appResources.MergedDictionaries[index].Source?.OriginalString;
            if (source?.Contains(DictionaryPrefix, StringComparison.OrdinalIgnoreCase) == true)
            {
                appResources.MergedDictionaries.RemoveAt(index);
            }
        }
    }
}
