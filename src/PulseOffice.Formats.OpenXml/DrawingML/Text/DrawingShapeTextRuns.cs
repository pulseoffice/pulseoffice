using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class DrawingShapeTextRuns(IShapeTextEditing editing, IShapeTextLists lists, DrawingShapeLists listCodec)
{
    public DrawingShapeTextRuns() : this(new ShapeTextEditing(), new ShapeTextLists(), new DrawingShapeLists()) { }

    public ShapeObject Read(OpenXmlElement? body, ShapeObject shape, IDrawingRunLinks links)
    {
        if (body is null) return shape;
        var runs = new List<ShapeTextRun>();
        var items = new List<ShapeListItem?>();
        var directions = new List<PulseOffice.Text.Rich.Bidi.TextDirection?>();
        var first = true;
        foreach (var paragraph in body.Elements<D.Paragraph>())
        {
            items.Add(listCodec.Read(paragraph.ParagraphProperties));
            directions.Add(paragraph.ParagraphProperties?.RightToLeft?.HasValue == true
                ? paragraph.ParagraphProperties.RightToLeft.Value ? PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft : PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight
                : null);
            if (!first) runs.Add(runs.Count > 0 ? runs[^1] with { Text = "\n" } : new ShapeTextRun("\n"));
            first = false;
            foreach (var child in paragraph.ChildElements)
            {
                if (child is D.Run run) runs.Add(Run(run.Text?.Text ?? string.Empty, run.RunProperties, links));
                else if (child is D.Field field) runs.Add(Run(field.Text?.Text ?? string.Empty, field.RunProperties, links));
                else if (child is D.Break line) runs.Add(Run(ShapeTextRun.LineBreak.ToString(), line.RunProperties, links));
            }
        }

        var read = editing.WithRuns(shape, runs);
        var directed = directions.Any(direction => direction is not null);
        if (items.Any(item => item is not null) || directed)
            return lists.WithDirections(lists.WithItems(read.Text is null ? shape : read, items), directions);
        return editing.Formatted(read) || Multiline(read.Text) ? read : shape;
    }

    public bool Formatted(ShapeObject shape) => editing.Formatted(shape) || lists.Listed(shape) || lists.Directed(shape);

    public OpenXmlElement Write(OpenXmlElement written, ShapeObject shape, IDrawingRunLinks links)
    {
        var text = editing.PlainText(shape);
        if (!Formatted(shape) && !Multiline(text)) return written;
        if (written.Descendants<D.Paragraph>().FirstOrDefault() is not { } template || template.Parent is not { } body) return written;

        var paragraphProperties = template.GetFirstChild<D.ParagraphProperties>();
        var runProperties = template.Elements<D.Run>().FirstOrDefault()?.RunProperties;
        var end = template.GetFirstChild<D.EndParagraphRunProperties>();
        foreach (var old in body.Elements<D.Paragraph>().ToList()) old.Remove();

        var runs = shape.Runs is { Count: > 0 } own ? own : [new ShapeTextRun(text)];
        var items = lists.Items(shape);
        var directions = lists.Directions(shape);
        var index = 0;
        D.Paragraph Next()
        {
            var properties = listCodec.Write(paragraphProperties, index < items.Count ? items[index] : null);
            properties.RightToLeft = index < directions.Count && directions[index] is { } direction
                ? direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft
                : null;
            index++;
            return Paragraph(properties);
        }
        var paragraph = Next();
        body.Append(paragraph);
        foreach (var run in runs)
        {
            var lines = run.Text.Split('\n');
            for (var line = 0; line < lines.Length; line++)
            {
                if (line > 0)
                {
                    Close(paragraph, end);
                    paragraph = Next();
                    body.Append(paragraph);
                }
                Append(paragraph, lines[line], Properties(runProperties, run, links));
            }
        }
        Close(paragraph, end);
        return written;
    }

    private bool Multiline(string? text) =>
        text is not null && text.AsSpan().IndexOfAny('\n', ShapeTextRun.LineBreak) >= 0;

    private void Append(D.Paragraph paragraph, string line, D.RunProperties properties)
    {
        var pieces = line.Split(ShapeTextRun.LineBreak);
        for (var piece = 0; piece < pieces.Length; piece++)
        {
            if (piece > 0) paragraph.Append(new D.Break(BreakProperties(properties)));
            if (pieces[piece].Length > 0) paragraph.Append(new D.Run((D.RunProperties)properties.CloneNode(true), new D.Text(pieces[piece])));
        }
    }

    private D.RunProperties BreakProperties(D.RunProperties properties)
    {
        var copy = (D.RunProperties)properties.CloneNode(true);
        foreach (var link in copy.ChildElements.Where(child => child is D.HyperlinkOnClick or D.HyperlinkOnMouseOver).ToList()) link.Remove();
        return copy;
    }

    private D.Paragraph Paragraph(D.ParagraphProperties properties) =>
        properties.HasAttributes || properties.HasChildren ? new D.Paragraph(properties) : new D.Paragraph();

    private void Close(D.Paragraph paragraph, D.EndParagraphRunProperties? end)
    {
        if (end is not null) paragraph.Append((D.EndParagraphRunProperties)end.CloneNode(true));
    }

    private D.RunProperties Properties(D.RunProperties? template, ShapeTextRun run, IDrawingRunLinks links)
    {
        var properties = (D.RunProperties?)template?.CloneNode(true) ?? new D.RunProperties();
        if (run.Bold) properties.Bold = true;
        if (run.Italic) properties.Italic = true;
        if (run.Underline) properties.Underline = D.TextUnderlineValues.Single;
        if (run.Size is { } size) properties.FontSize = (int)Math.Round(size * 100);
        if (run.ColorHex is { } colour)
        {
            foreach (var fill in properties.ChildElements.Where(child => child is D.SolidFill or D.NoFill or D.GradientFill or D.PatternFill).ToList()) fill.Remove();
            var solid = new D.SolidFill(new D.RgbColorModelHex { Val = colour.TrimStart('#')[^6..] });
            if (properties.GetFirstChild<D.Outline>() is { } outline) outline.InsertAfterSelf(solid);
            else properties.PrependChild(solid);
        }
        if (run.Family is { } family)
        {
            properties.GetFirstChild<D.LatinFont>()?.Remove();
            var latin = new D.LatinFont { Typeface = family };
            if (properties.ChildElements.FirstOrDefault(child => child is D.EastAsianFont or D.ComplexScriptFont or D.SymbolFont or D.HyperlinkOnClick or D.HyperlinkOnMouseOver or D.RightToLeft or D.ExtensionList) is { } after)
                after.InsertBeforeSelf(latin);
            else properties.Append(latin);
        }
        if (run.Link is { } target && links.Build(target) is { } hyperlink)
        {
            if (properties.ChildElements.FirstOrDefault(child => child is D.HyperlinkOnMouseOver or D.RightToLeft or D.ExtensionList) is { } after)
                after.InsertBeforeSelf(hyperlink);
            else properties.Append(hyperlink);
        }
        return properties;
    }

    private ShapeTextRun Run(string text, OpenXmlElement? properties, IDrawingRunLinks links)
    {
        var typed = properties as D.TextCharacterPropertiesType;
        var underline = typed?.Underline?.Value;
        return new ShapeTextRun(text)
        {
            Bold = typed?.Bold?.Value ?? false,
            Italic = typed?.Italic?.Value ?? false,
            Underline = underline is not null && underline != D.TextUnderlineValues.None,
            Size = typed?.FontSize?.Value is { } size ? size / 100.0 : null,
            ColorHex = properties?.GetFirstChild<D.SolidFill>()?.GetFirstChild<D.RgbColorModelHex>()?.Val?.Value is { Length: 6 } rgb
                ? "#" + rgb.ToUpper(CultureInfo.InvariantCulture)
                : null,
            Family = properties?.GetFirstChild<D.LatinFont>()?.Typeface?.Value is { Length: > 0 } face && !face.StartsWith('+') ? face : null,
            Link = links.Read(properties?.GetFirstChild<D.HyperlinkOnClick>()),
        };
    }
}
