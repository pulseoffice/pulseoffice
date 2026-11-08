using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Localization;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.CellFormat;

public sealed class CellAlignmentPage : UserControl
{
    private readonly CellAlignmentPreview _preview;
    private CellStyle _style;

    public CellAlignmentPage(CellStyle style, string sampleText, FormatCellsServices services, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(texts);
        _style = style ?? throw new ArgumentNullException(nameof(style));
        _preview = new CellAlignmentPreview(sampleText) { Width = 200, Height = 150 };
        AutomationProperties.SetName(_preview, texts["format.preview"]);

        Horizontal = new IconChoiceGroup(
        [
            new IconChoice("general", "ledger.align.general", texts["align.general"]),
            new IconChoice("left", "ledger.align.left", texts["align.left"]),
            new IconChoice("centre", "ledger.align.center", texts["align.centre"]),
            new IconChoice("right", "ledger.align.right", texts["align.right"]),
            new IconChoice("justify", "ledger.align.justify", texts["align.justify"]),
            new IconChoice("fill", "ledger.align.fill", texts["align.fill"]),
        ], services.Icons, metrics, texts["format.align.horizontal"], (int)style.Horizontal);
        Horizontal.SelectionChanged += (_, index) => Update(_style with { Horizontal = (CellHorizontalAlignment)index });

        Vertical = new IconChoiceGroup(
        [
            new IconChoice("top", "ledger.align.top", texts["align.top"]),
            new IconChoice("middle", "ledger.align.middle", texts["align.middle"]),
            new IconChoice("bottom", "ledger.align.bottom", texts["align.bottom"]),
        ], services.Icons, metrics, texts["format.align.vertical"], (int)style.Vertical);
        Vertical.SelectionChanged += (_, index) => Update(_style with { Vertical = (CellVerticalAlignment)index });

        Indent = new MeasureField(
            new MeasureEntry(new MeasureOptions(texts["format.align.indentUnit"], 0, 15, 1) { Integer = true }, services.Units, CultureInfo.CurrentCulture),
            style.Indent, texts, metrics, services.Icons, texts["format.align.indent"], boxWidth: 44);
        Indent.ValueCommitted += (_, value) => Update(_style with { Indent = (int)value });

        var rotationField = new MeasureField(
            new MeasureEntry(new MeasureOptions("°", -90, 90, 1) { Integer = true, LargeStep = 15, Presets = [-90, -45, 0, 45, 90] }, services.Units, CultureInfo.CurrentCulture),
            style.RotationDegrees, texts, metrics, services.Icons, texts["format.align.rotation"], boxWidth: 44);
        Rotation = new AngleField(rotationField, services.Angles, metrics.SpacingMedium, texts["format.align.rotation"]);
        Rotation.ValueChanged += (_, value) => Update(_style with { RotationDegrees = (int)Math.Round(value) });

        Wrap = new CheckBox { Content = texts["format.align.wrap"], IsChecked = style.WrapText };
        Wrap.IsCheckedChanged += (_, _) => Update(_style with { WrapText = Wrap.IsChecked == true });

        var layout = new TwoPaneDialogLayout(metrics) { Preview = _preview };
        var position = layout.AddSection(texts["format.align.position"]);
        position.Add(texts["format.align.horizontal"], Horizontal, editorWidth: 240);
        position.Add(texts["format.align.vertical"], Vertical, editorWidth: 240);
        var indentRow = position.Add(texts["format.align.indent"], Indent, editorWidth: 160);
        if (indentRow.Label is { } handle) Indent.AttachScrubber(handle);

        var orientation = layout.AddSection(texts["format.align.orientation"]);
        orientation.Add(texts["format.align.rotation"], Rotation, editorWidth: 220, alignTop: true);

        var control = layout.AddSection(texts["format.align.textControl"]);
        control.Add(null, Wrap, DialogRowKind.Indented);

        _preview.Show(style);
        Content = layout;
    }

    public event EventHandler<CellStyle>? StyleChanged;

    public IconChoiceGroup Horizontal { get; }

    public IconChoiceGroup Vertical { get; }

    public MeasureField Indent { get; }

    public AngleField Rotation { get; }

    public CheckBox Wrap { get; }

    public CellAlignmentPreview Preview => _preview;

    public CellStyle Style => _style;

    public void Adopt(CellStyle style)
    {
        _style = style ?? throw new ArgumentNullException(nameof(style));
        _preview.Show(style);
    }

    private void Update(CellStyle style)
    {
        _style = style;
        _preview.Show(style);
        StyleChanged?.Invoke(this, style);
    }
}

public sealed class CellAlignmentPreview(string sample) : Control
{
    public string Sample { get; set; } = sample;

    private static readonly IBrush Paper = new ImmutableSolidColorBrush(Colors.White);
    private static readonly IPen Grid = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)), 1);
    private static readonly IPen Active = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)), 2);
    private static readonly IBrush Ink = new ImmutableSolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27));

    private CellStyle _style = CellStyle.Default;

    public CellStyle Shown => _style;

    public void Show(CellStyle style)
    {
        _style = style;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        context.DrawRectangle(Paper, null, bounds);

        var columnWidth = bounds.Width / 5;
        var rowHeight = bounds.Height / 5;
        for (var column = 1; column < 5; column++) context.DrawLine(Grid, new Point(column * columnWidth, 0), new Point(column * columnWidth, bounds.Height));
        for (var row = 1; row < 5; row++) context.DrawLine(Grid, new Point(0, row * rowHeight), new Point(bounds.Width, row * rowHeight));

        var cell = new Rect(columnWidth, rowHeight, columnWidth * 3, rowHeight * 3);
        var fill = _style.BackgroundHex is { Length: > 0 } hex && Color.TryParse(hex, out var background) ? new SolidColorBrush(background) : Paper;
        context.DrawRectangle(fill, null, cell);
        context.DrawRectangle(null, Active, cell);
        Edge(context, _style.Borders.Top, cell.TopLeft, cell.TopRight);
        Edge(context, _style.Borders.Bottom, cell.BottomLeft, cell.BottomRight);
        Edge(context, _style.Borders.Left, cell.TopLeft, cell.BottomLeft);
        Edge(context, _style.Borders.Right, cell.TopRight, cell.BottomRight);
        var ink = Color.TryParse(_style.Font.ColorHex, out var colour) ? new SolidColorBrush(colour) : Ink;

        var family = string.IsNullOrWhiteSpace(_style.Font.Family) ? FontFamily.Default : new FontFamily(_style.Font.Family);
        var text = new FormattedText(
            Sample, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(family, _style.Font.Italic ? FontStyle.Italic : FontStyle.Normal, _style.Font.Bold ? FontWeight.Bold : FontWeight.Normal),
            Math.Clamp(_style.Font.Size + 2, 9, 18), ink);
        if (CellStyleThumbnail.Decorations(_style.Font) is { Count: > 0 } decorations) text.SetTextDecorations(decorations);
        var inner = cell.Deflate(5);
        if (_style.WrapText && _style.RotationDegrees == 0) text.MaxTextWidth = Math.Max(10, inner.Width - _style.Indent * 8);

        var radians = -_style.RotationDegrees * Math.PI / 180;
        var width = Math.Abs(text.Width * Math.Cos(radians)) + Math.Abs(text.Height * Math.Sin(radians));
        var height = Math.Abs(text.Width * Math.Sin(radians)) + Math.Abs(text.Height * Math.Cos(radians));
        var indent = _style.Indent * 8;

        var x = _style.Horizontal switch
        {
            CellHorizontalAlignment.Center => inner.X + (inner.Width - width) / 2,
            CellHorizontalAlignment.Right => inner.Right - width - indent,
            _ => inner.X + indent,
        };
        var y = _style.Vertical switch
        {
            CellVerticalAlignment.Top => inner.Y,
            CellVerticalAlignment.Middle => inner.Y + (inner.Height - height) / 2,
            _ => inner.Bottom - height,
        };

        var centre = new Point(x + width / 2, y + height / 2);
        using (context.PushClip(cell))
        using (context.PushTransform(
            Matrix.CreateTranslation(-text.Width / 2, -text.Height / 2)
            * Matrix.CreateRotation(radians)
            * Matrix.CreateTranslation(centre.X, centre.Y)))
        {
            if (_style.Horizontal == CellHorizontalAlignment.Fill && _style.RotationDegrees == 0)
            {
                for (var offset = inner.X - x; offset < inner.Right - x; offset += text.Width + 2)
                    context.DrawText(text, new Point(offset, 0));
            }
            else
            {
                context.DrawText(text, new Point(0, 0));
            }
        }
    }

    private static void Edge(DrawingContext context, BorderEdge edge, Point from, Point to)
    {
        if (edge.Style == BorderStyle.None || !Color.TryParse(edge.ColorHex, out var colour)) return;
        var width = edge.Style switch { BorderStyle.Thick => 3, BorderStyle.Medium or BorderStyle.Double => 2, _ => 1 };
        context.DrawLine(new Pen(new SolidColorBrush(colour), width), from, to);
    }
}
