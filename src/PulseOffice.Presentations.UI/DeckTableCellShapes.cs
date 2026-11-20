using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations.UI;

public sealed record TableCellShapeLook(TableCellLook Look, CellVerticalAlign Vertical, double FontSize, TableCellMargins? Margins = null, CellTextDirection Direction = CellTextDirection.Horizontal);

public interface IDeckTableCellShapes
{
    TableCellText CellText(int row, int column, string plain, TableCellText? rich);

    ShapeObject Shape(string id, TableCellText cell, TableCellShapeLook look);

    TableCellText Cell(ShapeObject edited, TableCellText original, TableCellShapeLook look);
}

public sealed class DeckTableCellShapes : IDeckTableCellShapes
{
    public const string DefaultInk = "#1E293B";

    private const double DefaultInset = 4;

    public TableCellText CellText(int row, int column, string plain, TableCellText? rich)
    {
        ArgumentNullException.ThrowIfNull(plain);
        if (rich is not null && rich.Matches(plain)) return rich with { Row = row, Column = column };
        var lines = plain.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return new TableCellText(row, column, [.. lines.Select(line => new TableCellParagraph([new TableTextRun(line)]))]);
    }

    public ShapeObject Shape(string id, TableCellText cell, TableCellShapeLook look)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(look);
        var style = TextStyle(cell, look);
        var effective = Effective(look);
        var runs = new List<ShapeTextRun>();
        for (var index = 0; index < cell.Paragraphs.Count; index++)
        {
            var paragraph = cell.Paragraphs[index];
            if (index > 0) runs.Add(Run("\n", runs.Count > 0 ? runs[^1] : null, effective));
            foreach (var run in paragraph.Runs.Where(run => run.Text.Length > 0))
            {
                var own = run.Over(effective);
                runs.Add(new ShapeTextRun(run.Text.Replace('\n', ShapeTextRun.LineBreak))
                {
                    Bold = own.Bold,
                    Italic = own.Italic,
                    Underline = own.Underline,
                    Strikethrough = own.Strikethrough,
                    Size = own.FontSize is { } size && Math.Abs(size - style.Size) > 0.001 ? size : null,
                    ColorHex = own.TextHex is { } ink && !string.Equals(ink, style.ColorHex, StringComparison.OrdinalIgnoreCase) ? ink : null,
                    Family = own.FontFamily is { Length: > 0 } family && family != style.Family ? family : null,
                });
            }
        }
        var text = string.Concat(runs.Select(run => run.Text));
        return new ShapeObject
        {
            Id = id,
            Shape = ShapeKind.Rectangle,
            Style = new ShapeStyle { Fill = ShapeFill.None, Line = ShapeLine.None, Text = style },
            Text = text,
            Runs = runs.Count > 0 ? runs : null,
            Alignments = Alignments(cell, look, style.Horizontal),
        };
    }

    public TableCellText Cell(ShapeObject edited, TableCellText original, TableCellShapeLook look)
    {
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(look);
        var style = edited.Style.Text;
        var runs = edited.Runs is { Count: > 0 } own ? own : [new ShapeTextRun(edited.Text ?? string.Empty)];
        var characters = new List<(char Character, TableTextRun Format)>();
        foreach (var run in runs)
        {
            var format = new TableTextRun(string.Empty)
            {
                Bold = run.Bold || style.Bold,
                Italic = run.Italic || style.Italic,
                Underline = run.Underline || style.Underline,
                Strikethrough = run.Strikethrough,
                TextHex = run.ColorHex ?? look.Look.TextHex,
                FontFamily = run.Family ?? look.Look.FontFamily,
                FontSize = run.Size ?? look.Look.FontSize,
            }.Beyond(look.Look);
            foreach (var character in run.Text) characters.Add((character == ShapeTextRun.LineBreak ? '\n' : character, format));
        }

        var plain = string.Concat(runs.Select(run => run.Text));
        var paragraphs = new List<TableCellParagraph>();
        var current = new List<TableTextRun>();
        var position = 0;
        foreach (var (character, format) in characters)
        {
            var at = position++;
            if (plain[at] == '\n')
            {
                paragraphs.Add(Paragraph(current, edited, paragraphs.Count));
                current = [];
                continue;
            }
            var piece = format with { Text = character.ToString() };
            if (current.Count > 0 && current[^1].SameFormat(piece)) current[^1] = current[^1] with { Text = current[^1].Text + character };
            else current.Add(piece);
        }
        paragraphs.Add(Paragraph(current, edited, paragraphs.Count));
        return new TableCellText(original.Row, original.Column, paragraphs).AlignedBeyond(look.Look);
    }

    private TableCellParagraph Paragraph(List<TableTextRun> runs, ShapeObject edited, int index)
    {
        var own = edited.Alignments is { } alignments && index < alignments.Count ? alignments[index] : null;
        return new([.. runs]) { Alignment = Cell(own ?? edited.Style.Text.Horizontal) };
    }

    private IReadOnlyList<TextHorizontalPlacement?>? Alignments(TableCellText cell, TableCellShapeLook look, TextHorizontalPlacement shape)
    {
        var placements = cell.Paragraphs.Select(paragraph => Placement(paragraph.AlignedOver(look.Look))).ToList();
        if (placements.All(placement => placement == shape)) return null;
        return [.. placements.Select(placement => placement == shape ? (TextHorizontalPlacement?)null : placement)];
    }

    private TextHorizontalPlacement Placement(CellHorizontalAlign align) => align switch
    {
        CellHorizontalAlign.Center => TextHorizontalPlacement.Centre,
        CellHorizontalAlign.Right => TextHorizontalPlacement.Right,
        CellHorizontalAlign.Justify => TextHorizontalPlacement.Justify,
        _ => TextHorizontalPlacement.Left,
    };

    private CellHorizontalAlign Cell(TextHorizontalPlacement placement) => placement switch
    {
        TextHorizontalPlacement.Centre => CellHorizontalAlign.Center,
        TextHorizontalPlacement.Right => CellHorizontalAlign.Right,
        TextHorizontalPlacement.Justify => CellHorizontalAlign.Justify,
        _ => CellHorizontalAlign.Left,
    };

    private TableCellLook Effective(TableCellShapeLook look) =>
        look.Look with { FontSize = look.Look.FontSize ?? look.FontSize };

    private ShapeTextStyle TextStyle(TableCellText cell, TableCellShapeLook look)
    {
        var first = cell.Paragraphs.Count > 0 ? cell.Paragraphs[0].AlignedOver(look.Look) : look.Look.Horizontal ?? CellHorizontalAlign.Left;
        return ShapeTextStyle.Default with
        {
            Family = look.Look.FontFamily is { Length: > 0 } family ? family : ShapeTextStyle.Default.Family,
            Size = look.Look.FontSize ?? look.FontSize,
            Bold = false,
            Italic = false,
            Underline = false,
            ColorHex = look.Look.TextHex ?? DefaultInk,
            Horizontal = Placement(first),
            Vertical = look.Vertical switch
            {
                CellVerticalAlign.Center => TextVerticalPlacement.Middle,
                CellVerticalAlign.Bottom => TextVerticalPlacement.Bottom,
                _ => TextVerticalPlacement.Top,
            },
            Wrap = true,
            Layout = look.Direction switch
            {
                CellTextDirection.Down => new PulseOffice.Text.Rich.TextLayout { Flow = PulseOffice.Text.Rich.TextFlow.Vertical },
                CellTextDirection.Up => new PulseOffice.Text.Rich.TextLayout { Flow = PulseOffice.Text.Rich.TextFlow.Vertical270 },
                CellTextDirection.Stacked => new PulseOffice.Text.Rich.TextLayout { Flow = PulseOffice.Text.Rich.TextFlow.Stacked },
                _ => PulseOffice.Text.Rich.TextLayout.Default,
            },
            Inset = Twip.FromPoints(look.Margins?.Left ?? DefaultInset),
        };
    }

    private ShapeTextRun Run(string text, ShapeTextRun? before, TableCellLook look) =>
        before is null ? new ShapeTextRun(text) { Bold = look.Bold, Italic = look.Italic, Underline = look.Underline } : before with { Text = text };
}
