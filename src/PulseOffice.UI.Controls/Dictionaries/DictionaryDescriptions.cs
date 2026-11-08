using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Text.Spelling.Dictionaries;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Dictionaries;

public sealed class DictionaryDescriptions(ITextCatalog texts)
{
    public string Part(DictionaryPart part) => texts[$"dictionaries.part.{Key(part.ToString())}"];

    public string PartDescription(DictionaryPart part) => texts[$"dictionaries.part.{Key(part.ToString())}.description"];

    public string PartIcon(DictionaryPart part) => part switch
    {
        DictionaryPart.Spelling => "ui.check",
        DictionaryPart.Hyphenation => "ui.dictionary.hyphen",
        _ => "ui.dictionary.thesaurus",
    };

    public string Status(DictionaryStatus status) => texts[$"dictionaries.status.{Key(status.ToString())}"];

    public string StatusColour(DictionaryStatus status) => status switch
    {
        DictionaryStatus.Installed => Palette.Keys.Success,
        DictionaryStatus.UpdateAvailable => Palette.Keys.Warning,
        DictionaryStatus.OnSystem => Palette.Keys.Info,
        DictionaryStatus.Available => Palette.Keys.TextSecondary,
        _ => Palette.Keys.TextDisabled,
    };

    public string Freedom(DictionaryLicense license) => texts[$"dictionaries.license.{Key(license.Freedom.ToString())}"];

    public string FreedomColour(DictionaryLicense license) => license.Freedom switch
    {
        DictionaryFreedom.Free => Palette.Keys.Success,
        DictionaryFreedom.Copyleft => Palette.Keys.Info,
        _ => Palette.Keys.TextSecondary,
    };

    public string LanguageName(string code)
    {
        try
        {
            var name = CultureInfo.GetCultureInfo(code).NativeName;
            return name.Length == 0 || name.Equals(code, StringComparison.OrdinalIgnoreCase) ? code : Capitalised(name);
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

    public string Size(long bytes) => bytes < 1024 * 1024
        ? texts.Format("dictionaries.size.kb", (bytes / 1024d).ToString("N0", CultureInfo.CurrentCulture))
        : texts.Format("dictionaries.size.mb", (bytes / 1024d / 1024d).ToString("N1", CultureInfo.CurrentCulture));

    private string Capitalised(string name) =>
        name.Length == 0 ? name : char.ToUpper(name[0], CultureInfo.CurrentCulture) + name[1..];

    private string Key(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
