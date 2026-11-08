using PulseOffice.Documents.Styles;
using PulseOffice.Localization;

namespace PulseOffice.Documents.UI.StyleCatalog;

public interface IFolioStyleNames
{
    string Title(DocumentStyle style);

    string Title(string name);

    bool IsBuiltIn(StyleFamily family, string name);
}

public sealed class FolioStyleNames(ITextCatalog texts, IDefaultStyleSheetFactory defaults) : IFolioStyleNames
{
    private readonly Lazy<StyleSheet> _defaults = new(defaults.Create);

    public string Title(DocumentStyle style) => style.DisplayName ?? Title(style.Name);

    public string Title(string name)
    {
        if (name.StartsWith("Heading ", StringComparison.Ordinal) && int.TryParse(name.AsSpan(8), out var level))
            return texts.Format("folio.styleName.headingLevel", level);
        var space = name.LastIndexOf(' ');
        if (space > 0 && int.TryParse(name.AsSpan(space + 1), out var number))
        {
            var levelKey = "folio.styleName." + string.Concat(name[..space].Where(char.IsLetterOrDigit)).ToLowerInvariant() + ".level";
            if (texts.Contains(levelKey)) return texts.Format(levelKey, number);
        }
        var letters = string.Concat(name.Where(char.IsLetterOrDigit));
        var shouting = letters.Zip(letters.Skip(1)).Any(pair => char.IsUpper(pair.First) && char.IsUpper(pair.Second));
        var key = "folio.styleName." + letters.ToLowerInvariant() + (shouting ? "upper" : string.Empty);
        return texts.Contains(key) ? texts[key] : name;
    }

    public bool IsBuiltIn(StyleFamily family, string name)
    {
        var sheet = _defaults.Value;
        return family switch
        {
            StyleFamily.Paragraph => sheet.Paragraph.Contains(name),
            StyleFamily.Character => sheet.Character.Contains(name),
            StyleFamily.List => sheet.List.Contains(name),
            StyleFamily.Page => sheet.Page.Contains(name),
            StyleFamily.Frame => sheet.Frame.Contains(name),
            _ => sheet.Table.Contains(name),
        };
    }
}
