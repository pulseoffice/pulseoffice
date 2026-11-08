using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Rich;

public interface IHyperlinkAppearance
{
    uint Ink(TextRun run, ITextTheme theme);

    bool Underlined(TextRun run);

    string? ColorHex(ShapeTextRun run);

    bool Underlined(ShapeTextRun run);
}

public sealed class HyperlinkAppearance : IHyperlinkAppearance
{
    private const string DefaultLinkHex = "#0563C1";

    public uint Ink(TextRun run, ITextTheme theme) =>
        Linked(run) ? theme.Resolve(ColorRole.Hyperlink) : run.Color ?? theme.Resolve(run.ColorRole ?? ColorRole.Text);

    public bool Underlined(TextRun run) => run.Underline || run.DoubleUnderline || Linked(run);

    public string? ColorHex(ShapeTextRun run) => Linked(run) ? DefaultLinkHex : run.ColorHex is { Length: > 0 } colour ? colour : null;

    public bool Underlined(ShapeTextRun run) => run.Underline || Linked(run);

    private bool Linked(TextRun run) => run.Hyperlink is { Length: > 0 } && run.Field == TextFieldKind.None;

    private bool Linked(ShapeTextRun run) => run.Link is { Length: > 0 };
}
