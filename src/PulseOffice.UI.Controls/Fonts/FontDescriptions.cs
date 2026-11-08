using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.Licensing;
using PulseOffice.Typography.OpenType;
using PulseOffice.Localization;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fonts;

public sealed class FontDescriptions(ITextCatalog texts)
{
    public ITextCatalog Texts => texts;

    public string Category(FontCategory category) => texts[$"font.category.{Key(category.ToString())}"];

    public string Script(FontScript script) => texts[$"font.script.{Key(script.ToString())}"];

    public string Origin(FontOrigin origin) => texts[$"font.source.{Key(origin.ToString())}"];

    public string Freedom(FontLicense license) => texts[$"font.license.{Key(license.Freedom.ToString())}"];

    public string FreedomKey(FontLicense license) => license.Freedom switch
    {
        LicenseFreedom.Free => Palette.Keys.Success,
        LicenseFreedom.Restricted => Palette.Keys.Warning,
        _ => Palette.Keys.TextSecondary,
    };

    public string Embedding(EmbeddingPermission permission)
    {
        var text = texts[$"font.embedding.{Key(permission.Level.ToString())}"];
        if (permission.BitmapOnly) text += " · " + texts["font.embedding.bitmapOnly"];
        if (permission.NoSubsetting) text += " · " + texts["font.embedding.noSubsetting"];
        return text;
    }

    public string EmbeddingKey(EmbeddingPermission permission) => permission.AllowsEmbedding
        ? permission.Level == EmbeddingLevel.PreviewAndPrint ? Palette.Keys.Warning : Palette.Keys.TextSecondary
        : Palette.Keys.Danger;

    public string Styles(FontFamilyInfo family) =>
        family.Faces.Count == 0
            ? texts["font.browser.noStyles"]
            : string.Join(", ", family.Faces
                .Select(face => (face.Weight, face.Italic))
                .Distinct()
                .OrderBy(face => face.Italic).ThenBy(face => face.Weight)
                .Select(face => face.Italic ? $"{face.Weight} {texts["font.style.italic"]}" : face.Weight.ToString(System.Globalization.CultureInfo.CurrentCulture)));

    private string Key(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
