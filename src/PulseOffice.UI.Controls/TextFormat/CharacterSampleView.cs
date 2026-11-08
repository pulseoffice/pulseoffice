using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.TextFormat;

public sealed record CharacterGlyph(string Text, double Size, double Advance);

public interface ICharacterSampleLayout
{
    string Transform(string sample, TextCase textCase);

    IReadOnlyList<(string Text, bool Reduced)> Pieces(string text, bool smallCaps);

    double DisplaySize(double points);

    double BaselineShift(TextBaseline baseline, double displaySize);

    double GlyphSize(TextBaseline baseline, double displaySize);

    double ReducedSize(double glyphSize);
}

public sealed class CharacterSampleLayout : ICharacterSampleLayout
{
    private const double MinimumDisplay = 9;
    private const double MaximumDisplay = 40;
    private const double ScriptScale = 0.62;
    private const double SmallCapsScale = 0.78;

    public string Transform(string sample, TextCase textCase)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var culture = CultureInfo.CurrentCulture;
        return textCase switch
        {
            TextCase.Upper => sample.ToUpper(culture),
            TextCase.Lower => sample.ToLower(culture),
            TextCase.Capitalize => culture.TextInfo.ToTitleCase(sample.ToLower(culture)),
            _ => sample,
        };
    }

    public IReadOnlyList<(string Text, bool Reduced)> Pieces(string text, bool smallCaps)
    {
        ArgumentNullException.ThrowIfNull(text);
        var culture = CultureInfo.CurrentCulture;
        return [.. text.Select(character => smallCaps && char.IsLower(character)
            ? (char.ToUpper(character, culture).ToString(), true)
            : (character.ToString(), false))];
    }

    public double DisplaySize(double points) => Math.Clamp(points, MinimumDisplay, MaximumDisplay);

    public double GlyphSize(TextBaseline baseline, double displaySize) =>
        baseline == TextBaseline.Normal ? displaySize : displaySize * ScriptScale;

    public double BaselineShift(TextBaseline baseline, double displaySize) => baseline switch
    {
        TextBaseline.Superscript => -displaySize * 0.36,
        TextBaseline.Subscript => displaySize * 0.16,
        _ => 0,
    };

    public double ReducedSize(double glyphSize) => glyphSize * SmallCapsScale;
}

public sealed class CharacterSampleView : Control
{
    private const double OutlineReach = 0.7;

    private readonly ICharacterSampleLayout _layout;
    private readonly IBrush _paper = Brushes.White;
    private readonly IBrush _shadow = new SolidColorBrush(Color.FromArgb(0x70, 0x60, 0x60, 0x60));
    private readonly IBrush _light = new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF));
    private readonly IPen _baseline = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0x6B, 0x72, 0x80)), 0.6, new DashStyle([3, 3], 0));
    private CharacterFormatState _state = new();
    private string _sample = string.Empty;

    public CharacterSampleView(ICharacterSampleLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Width = 360;
        Height = 120;
    }

    public CharacterFormatState State => _state;

    public string Sample => _sample;

    public void Show(CharacterFormatState state, string sample)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _sample = sample ?? string.Empty;
        InvalidateVisual();
    }

    public IReadOnlyList<CharacterGlyph> Glyphs()
    {
        var display = _layout.DisplaySize(_state.Size);
        var glyphSize = _layout.GlyphSize(_state.Baseline, display);
        var reduced = _layout.ReducedSize(glyphSize);
        var spacing = _state.LetterSpacing * display / Math.Max(_state.Size, 1);
        var text = _layout.Transform(_sample, _state.Case);
        return [.. _layout.Pieces(text, _state.SmallCaps).Select(piece =>
        {
            var size = piece.Reduced ? reduced : glyphSize;
            var formatted = Format(piece.Text, size, Brushes.Black);
            return new CharacterGlyph(piece.Text, size, formatted.WidthIncludingTrailingWhitespace + spacing);
        })];
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(_paper, new Rect(Bounds.Size));
        if (_sample.Length == 0) return;

        var display = _layout.DisplaySize(_state.Size);
        var glyphs = Glyphs();
        var scale = Math.Clamp(_state.Scale / 100, 0.1, 6);
        var natural = glyphs.Sum(glyph => glyph.Advance);
        var width = natural * scale;
        var fit = width > Bounds.Width - 16 && width > 0 ? (Bounds.Width - 16) / width : 1;
        var left = Math.Max(8, (Bounds.Width - width * fit) / 2);
        var baseline = Bounds.Height / 2 + display * 0.32;
        var shift = _layout.BaselineShift(_state.Baseline, display);
        var ink = Ink();

        if (_state.Baseline != TextBaseline.Normal)
            context.DrawLine(_baseline, new Point(8, baseline), new Point(Bounds.Width - 8, baseline));

        using (context.PushTransform(Matrix.CreateScale(scale * fit, fit) * Matrix.CreateTranslation(left, baseline + shift)))
        {
            if (_state.Highlight is { } highlight)
                context.FillRectangle(new SolidColorBrush(Color.FromUInt32(highlight | 0xFF000000)), new Rect(0, -display * 0.92, natural, display * 1.18));

            if (_state.Shadow) Draw(context, glyphs, new Vector(display * 0.06, display * 0.06), _shadow);
            switch (_state.Relief)
            {
                case FontRelief.Embossed:
                    Draw(context, glyphs, new Vector(display * 0.04, display * 0.04), _shadow);
                    break;
                case FontRelief.Engraved:
                    Draw(context, glyphs, new Vector(-display * 0.04, -display * 0.04), _shadow);
                    break;
            }

            if (_state.Outline)
            {
                foreach (var dx in new[] { -OutlineReach, 0, OutlineReach })
                    foreach (var dy in new[] { -OutlineReach, 0, OutlineReach })
                        if (dx != 0 || dy != 0) Draw(context, glyphs, new Vector(dx, dy), ink);
                Draw(context, glyphs, default, _paper);
            }
            else
            {
                Draw(context, glyphs, default, _state.Relief == FontRelief.None ? ink : _light);
                if (_state.Relief != FontRelief.None) Draw(context, glyphs, default, new SolidColorBrush(Color.FromArgb(0x90, 0xB0, 0xB0, 0xB0)));
            }

            Decorations(context, natural, display, ink);
        }
    }

    private void Decorations(DrawingContext context, double width, double display, IBrush ink)
    {
        var size = _layout.GlyphSize(_state.Baseline, display);
        var pen = new Pen(ink, Math.Max(0.8, size / 16));
        if (_state.Underline == UnderlineKind.Single) context.DrawLine(pen, new Point(0, size * 0.14), new Point(width, size * 0.14));
        if (_state.Underline == UnderlineKind.Double)
        {
            context.DrawLine(pen, new Point(0, size * 0.1), new Point(width, size * 0.1));
            context.DrawLine(pen, new Point(0, size * 0.22), new Point(width, size * 0.22));
        }
        if (_state.Strikethrough) context.DrawLine(pen, new Point(0, -size * 0.28), new Point(width, -size * 0.28));
        if (_state.Overline) context.DrawLine(pen, new Point(0, -size * 0.86), new Point(width, -size * 0.86));
        if (_state.Hidden)
            context.DrawLine(new Pen(Brushes.Gray, 1, new DashStyle([1, 2], 0)), new Point(0, size * 0.3), new Point(width, size * 0.3));
    }

    private void Draw(DrawingContext context, IReadOnlyList<CharacterGlyph> glyphs, Vector offset, IBrush brush)
    {
        var x = offset.X;
        foreach (var glyph in glyphs)
        {
            var formatted = Format(glyph.Text, glyph.Size, brush);
            context.DrawText(formatted, new Point(x, offset.Y - formatted.Baseline));
            x += glyph.Advance;
        }
    }

    private IBrush Ink()
    {
        var color = _state.Color is { } chosen ? Color.FromUInt32(chosen | 0xFF000000) : Colors.Black;
        return new SolidColorBrush(_state.Hidden ? Color.FromArgb(0x80, color.R, color.G, color.B) : color);
    }

    private FormattedText Format(string text, double size, IBrush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(
                _state.Font.Length > 0 ? new FontFamily(_state.Font) : FontFamily.Default,
                _state.Italic ? FontStyle.Italic : FontStyle.Normal,
                _state.Bold ? FontWeight.Bold : FontWeight.Normal),
            size, brush);
}
