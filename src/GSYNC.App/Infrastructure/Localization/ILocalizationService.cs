namespace GSYNC.App.Infrastructure.Localization;

public interface ILocalizationService
{
    string CurrentLanguageTag { get; }

    IReadOnlyList<LanguageOption> SupportedLanguages { get; }

    event EventHandler? LanguageChanged;

    string GetString(string key);

    bool SetLanguage(string languageTag);
}

public sealed record LanguageOption(string Tag, string DisplayName)
{
    public override string ToString() => DisplayName;
}
