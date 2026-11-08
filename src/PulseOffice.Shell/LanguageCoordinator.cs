using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.Settings;

namespace PulseOffice.Shell;

public sealed class LanguageCoordinator : IDisposable
{
    private readonly ISettingsService _settings;
    private readonly ITextCatalog _texts;
    private readonly ILogger<LanguageCoordinator> _logger;

    public LanguageCoordinator(
        ISettingsService settings, ITextCatalog texts, ILogger<LanguageCoordinator> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Apply();
        _settings.Changed += OnSettingChanged;
    }

    private void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        if (e.Key == ShellSettingKeys.Language) Apply();
    }

    private void Apply()
    {
        var chosen = Parse(_settings.Get<string>(ShellSettingKeys.Language));
        _texts.Use(chosen);

        _logger.LogInformation(
            "Interface language: {Language} (setting: {Setting}, system culture: {Culture})",
            _texts.Language, chosen, System.Globalization.CultureInfo.CurrentUICulture.Name);
    }

    private static UiLanguage Parse(string? code) => code switch
    {
        "it" => UiLanguage.Italian,
        "en" => UiLanguage.English,
        "de" => UiLanguage.German,
        "es" => UiLanguage.Spanish,
        "fr" => UiLanguage.French,
        _ => UiLanguage.System,
    };

    public void Dispose() => _settings.Changed -= OnSettingChanged;
}
