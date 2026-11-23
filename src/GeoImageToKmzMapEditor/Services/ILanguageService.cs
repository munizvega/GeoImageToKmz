namespace TifToKmzMapEditor.Services;

public interface ILanguageService
{
    string CurrentLanguage { get; }
    void SetLanguage(string cultureCode);
    event EventHandler? LanguageChanged;
}
