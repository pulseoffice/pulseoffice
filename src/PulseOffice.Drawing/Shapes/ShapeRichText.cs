using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Text.Rich;

namespace PulseOffice.Drawing.Shapes;

public interface IShapeRichText
{
    RichText ToRich(ShapeObject shape);

    ShapeObject FromRich(ShapeObject shape, RichText text);

    TextParagraph Listed(TextParagraph paragraph, ShapeListItem? item);

    ShapeListItem? Item(TextParagraph paragraph);
}

public sealed class ShapeRichText(IShapeTextEditing editing, IShapeTextLists lists) : IShapeRichText
{
    public ShapeRichText() : this(new ShapeTextEditing(), new ShapeTextLists()) { }

    public RichText ToRich(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var style = shape.Style.Text;
        var runs = shape.Runs is { Count: > 0 } own ? own : [new ShapeTextRun(shape.Text ?? string.Empty)];
        var paragraphs = new List<TextParagraph>();
        var current = new List<TextRun>();
        var items = lists.Items(shape);

        void Close()
        {
            var item = paragraphs.Count < items.Count ? items[paragraphs.Count] : null;
            paragraphs.Add(Listed(new TextParagraph(string.Concat(current.Select(run => run.Text)))
            {
                FontSize = style.Size,
                Runs = [.. current.Where(run => run.Text.Length > 0)],
            }, item));
            current = [];
        }

        foreach (var run in runs)
        {
            var pieces = run.Text.Split('\n');
            for (var index = 0; index < pieces.Length; index++)
            {
                if (index > 0) Close();
                current.Add(Rich(run, pieces[index], style));
            }
        }
        Close();
        return new RichText(PlaceholderRole.Free, paragraphs);
    }

    public ShapeObject FromRich(ShapeObject shape, RichText text)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(text);
        var pieces = text.Paragraphs
            .SelectMany(paragraph => (paragraph.Runs.Count > 0 ? paragraph.Runs : [new TextRun(paragraph.Text)])
                .Where(run => run.Text.Length > 0)
                .Select(run => (Run: run, Paragraph: paragraph)))
            .ToList();
        var style = shape.Style.Text with
        {
            Bold = shape.Style.Text.Bold && pieces.All(piece => piece.Run.Bold || piece.Paragraph.Bold),
            Italic = shape.Style.Text.Italic && pieces.All(piece => piece.Run.Italic || piece.Paragraph.Italic),
            Underline = shape.Style.Text.Underline && pieces.All(piece => piece.Run.Underline || piece.Paragraph.Underline),
        };
        var runs = new List<ShapeTextRun>();
        for (var index = 0; index < text.Paragraphs.Count; index++)
        {
            var paragraph = text.Paragraphs[index];
            if (index > 0) runs.Add(runs.Count > 0 ? runs[^1] with { Text = "\n" } : new ShapeTextRun("\n"));
            var own = paragraph.Runs.Count > 0 ? paragraph.Runs : [new TextRun(paragraph.Text)];
            runs.AddRange(own.Select(run => Shape(run, paragraph, style)));
        }

        var rebuilt = shape with { Style = shape.Style with { Text = style }, Text = string.Concat(runs.Select(run => run.Text)), Runs = runs };
        if (rebuilt.Text.Length == 0) return shape with { Text = null, Runs = null, Lists = null };
        return lists.WithItems(editing.Format(rebuilt, 0, 0, run => run), text.Paragraphs.Select(Item));
    }

    public TextParagraph Listed(TextParagraph paragraph, ShapeListItem? item)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        return paragraph with
        {
            Bullet = item is { Numbered: false },
            Numbered = item is { Numbered: true },
            NumberFormat = item is { Numbered: true } ? item.NumberFormat : null,
            BulletLevel = item?.Level ?? 0,
            BulletSymbol = item is { Numbered: false } ? item.Symbol : null,
            MarginLeft = item?.Margin,
            Indent = item?.Indent,
        };
    }

    public ShapeListItem? Item(TextParagraph paragraph) =>
        paragraph.IsListItem
            ? new ShapeListItem
            {
                Level = paragraph.BulletLevel,
                Numbered = paragraph.Numbered,
                NumberFormat = paragraph.Numbered ? paragraph.NumberFormat : null,
                Symbol = paragraph.Numbered ? null : paragraph.BulletSymbol,
                Margin = paragraph.MarginLeft,
                Indent = paragraph.Indent,
            }
            : null;

    private TextRun Rich(ShapeTextRun run, string text, ShapeTextStyle style) => new(text)
    {
        Bold = run.Bold || style.Bold,
        Italic = run.Italic || style.Italic,
        Underline = run.Underline || style.Underline,
        FontSize = run.Size ?? style.Size,
        Font = run.Family ?? style.Family,
        Color = Argb(run.ColorHex),
        Hyperlink = run.Link,
    };

    private ShapeTextRun Shape(TextRun run, TextParagraph paragraph, ShapeTextStyle style)
    {
        var size = run.FontSize ?? paragraph.FontSize;
        var colour = run.Color ?? paragraph.Color;
        return new ShapeTextRun(run.Text)
        {
            Bold = (run.Bold || paragraph.Bold) && !style.Bold,
            Italic = (run.Italic || paragraph.Italic) && !style.Italic,
            Underline = (run.Underline || paragraph.Underline) && !style.Underline,
            Size = Math.Abs(size - style.Size) < 0.01 ? null : size,
            Family = run.Font is { Length: > 0 } family && family != style.Family ? family : null,
            ColorHex = colour is { } argb ? new ArgbColor(argb).ToHashedHex() : null,
            Link = run.Hyperlink,
        };
    }

    private uint? Argb(string? hex)
    {
        var digits = (hex ?? string.Empty).TrimStart('#');
        if (digits.Length == 8) digits = digits[2..];
        return digits.Length == 6 && uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
            ? 0xFF000000 | rgb
            : null;
    }
}
