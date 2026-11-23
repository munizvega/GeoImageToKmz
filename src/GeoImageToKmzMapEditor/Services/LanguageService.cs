using System.Globalization;
using System.Text.Json;
using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.Services;

public sealed class LanguageService : ILanguageService
{
    private const string _configFileName = "language.editor.json";
    private string _currentLanguage = "es";

    public string CurrentLanguage => _currentLanguage;

    public event EventHandler? LanguageChanged;

    public LanguageService()
    {
        LoadConfig();
        ApplyLanguage(_currentLanguage, notify: false);
    }

    public void SetLanguage(string cultureCode)
    {
        string clean = cultureCode.ToLowerInvariant().StartsWith("en") ? "en" : "es";
        if (_currentLanguage != clean)
        {
            _currentLanguage = clean;
            SaveConfig();
            ApplyLanguage(_currentLanguage, notify: true);
        }
    }

    private void ApplyLanguage(string cultureCode, bool notify)
    {
        CultureInfo culture = new(cultureCode);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Strings.Culture = culture;

        if (notify)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void LoadConfig()
    {
        try
        {
            string configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Language", out JsonElement elem))
                {
                    string? lang = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(lang))
                    {
                        _currentLanguage = lang.ToLowerInvariant().StartsWith("en") ? "en" : "es";
                    }
                }
            }
            else
            {
                string currentUiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                _currentLanguage = currentUiLang == "en" ? "en" : "es";
            }
        }
        catch
        {
            _currentLanguage = "es";
        }
    }

    private void SaveConfig()
    {
        try
        {
            string configPath = GetConfigFilePath();
            string? dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var configObj = new
            {
                Language = _currentLanguage
            };

            string json = JsonSerializer.Serialize(configObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch
        {
            // Ignore saving errors
        }
    }

    private static string GetConfigFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TifToKmzMapEditor", _configFileName);
    }
}
