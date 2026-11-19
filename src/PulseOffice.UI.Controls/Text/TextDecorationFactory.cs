using Avalonia.Media;

namespace PulseOffice.UI.Controls.Text;

public interface ITextDecorationFactory
{
    TextDecorationCollection? Create(bool underline, bool strikethrough, bool overline = false);

    TextDecorationCollection Underline();

    TextDecorationCollection Strikethrough();
}

public sealed class TextDecorationFactory : ITextDecorationFactory
{
    public TextDecorationCollection? Create(bool underline, bool strikethrough, bool overline = false)
    {
        if (!underline && !strikethrough && !overline) return null;
        var decorations = new TextDecorationCollection();
        if (underline) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
        if (strikethrough) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
        if (overline) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Overline });
        return decorations;
    }

    public TextDecorationCollection Underline() => Create(true, false)!;

    public TextDecorationCollection Strikethrough() => Create(false, true)!;
}
