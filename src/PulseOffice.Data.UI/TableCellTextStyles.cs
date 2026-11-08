using Avalonia.Media;
using PulseOffice.Document.Embedding;

namespace PulseOffice.Data.UI;

public interface ITableCellTextStyles
{
    Typeface Typeface(TableCellLook look);

    TextDecorationCollection? Decorations(TableCellLook look);
}

public sealed class TableCellTextStyles : ITableCellTextStyles
{
    public Typeface Typeface(TableCellLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        var family = look.FontFamily is { Length: > 0 } name ? new FontFamily(name) : FontFamily.Default;
        return new Typeface(family, look.Italic ? FontStyle.Italic : FontStyle.Normal, look.Bold ? FontWeight.SemiBold : FontWeight.Normal);
    }

    public TextDecorationCollection? Decorations(TableCellLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        if (!look.Underline && !look.Strikethrough) return null;
        var decorations = new TextDecorationCollection();
        if (look.Underline) decorations.AddRange(TextDecorations.Underline);
        if (look.Strikethrough) decorations.AddRange(TextDecorations.Strikethrough);
        return decorations;
    }
}
