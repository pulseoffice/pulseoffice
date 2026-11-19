using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.UI.Controls.Text;

namespace PulseOffice.Data.UI;

public interface ITableCellTextStyles
{
    Typeface Typeface(TableCellLook look);

    TextDecorationCollection? Decorations(TableCellLook look);
}

public sealed class TableCellTextStyles(ITextDecorationFactory decorations) : ITableCellTextStyles
{
    public TableCellTextStyles()
        : this(new TextDecorationFactory())
    {
    }

    public Typeface Typeface(TableCellLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        var family = look.FontFamily is { Length: > 0 } name ? new FontFamily(name) : FontFamily.Default;
        return new Typeface(family, look.Italic ? FontStyle.Italic : FontStyle.Normal, look.Bold ? FontWeight.SemiBold : FontWeight.Normal);
    }

    public TextDecorationCollection? Decorations(TableCellLook look)
    {
        ArgumentNullException.ThrowIfNull(look);
        return decorations.Create(look.Underline, look.Strikethrough);
    }
}
