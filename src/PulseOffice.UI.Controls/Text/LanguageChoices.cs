using System.Globalization;
using PulseOffice.Localization;

namespace PulseOffice.UI.Controls.Text;

public sealed record LanguageChoice(string? Tag, string Title);

public interface ILanguageChoices
{
    IReadOnlyList<LanguageChoice> For(string? current);
}

public sealed class LanguageChoices(ITextCatalog texts) : ILanguageChoices
{
    public const string NoLanguage = "zxx";

    private readonly IReadOnlyList<string> _common =
    [
        "it-IT", "en-US", "en-GB", "de-DE", "de-CH", "fr-FR", "es-ES", "pt-PT", "pt-BR", "nl-NL", "ca-ES", "sv-SE", "da-DK",
        "nb-NO", "fi-FI", "pl-PL", "cs-CZ", "sk-SK", "sl-SI", "hr-HR", "hu-HU", "ro-RO", "el-GR", "ru-RU", "uk-UA", "tr-TR",
        "ar-SA", "he-IL", "ja-JP", "zh-CN", "zh-TW", "ko-KR", "la",
    ];

    public IReadOnlyList<LanguageChoice> For(string? current)
    {
        var tags = _common.ToList();
        if (current is { Length: > 0 } && current != NoLanguage && !tags.Contains(current, StringComparer.OrdinalIgnoreCase)) tags.Add(current);
        var named = tags.Select(tag => new LanguageChoice(tag, Title(tag))).OrderBy(choice => choice.Title, StringComparer.CurrentCulture);
        return [new LanguageChoice(null, texts["language.default"]), new LanguageChoice(NoLanguage, texts["language.none"]), .. named];
    }

    private string Title(string tag)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(tag);
            return culture.DisplayName == culture.Name ? tag : $"{culture.DisplayName}";
        }
        catch (CultureNotFoundException)
        {
            return tag;
        }
    }
}
