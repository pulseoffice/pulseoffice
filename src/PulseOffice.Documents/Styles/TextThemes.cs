using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Themes;
using PulseOffice.Documents.Editing;

namespace PulseOffice.Documents.Styles;

public interface ITextThemeApplier
{
    int Apply(TextDocument document, UndoStack history, ThemePalette palette);
}

public sealed class TextThemeApplier(IStyleEditing styles) : ITextThemeApplier
{
    public int Apply(TextDocument document, UndoStack history, ThemePalette palette)
    {
        var heading = Color(palette.HeadingColor);
        var accent = Color(palette.Accent1);
        var changed = 0;
        using (history.BeginTransaction("theme"))
        {
            var sheet = document.Styles;
            var before = sheet.DefaultCharacter;
            var after = before with { Font = palette.FontFamily };
            if (after != before)
            {
                sheet.DefaultCharacter = after;
                history.Push(new DelegateAction("theme-font", () => { sheet.DefaultCharacter = after; document.Raise(); }, () => { sheet.DefaultCharacter = before; document.Raise(); }));
                changed++;
            }
            foreach (var style in sheet.Paragraph.All.ToList())
            {
                uint? color = style.Name == DefaultStyleNames.Title || style.Name == DefaultStyleNames.Subtitle ? accent
                    : style.Name.StartsWith(DefaultStyleNames.Heading, StringComparison.Ordinal) ? heading
                    : null;
                if (color is null) continue;
                if (styles.Change(document, history, sheet.Paragraph, style.Name, target => target.Character = target.Character with { Color = color, Font = palette.FontFamily }, "theme")) changed++;
            }
            if (sheet.Character.Find(DefaultStyleNames.InternetLink) is not null
                && styles.Change(document, history, sheet.Character, DefaultStyleNames.InternetLink, target => target.Character = target.Character with { Color = accent }, "theme")) changed++;
            document.Raise();
        }
        return changed;
    }

    private uint Color(string hex) =>
        ArgbColor.FromRgbHex(hex)!.Value.Value;
}
