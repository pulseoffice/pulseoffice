using System.Globalization;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxNoteSeparatorCodec
{
    private const double EighthsPerPoint = 8;

    private const double Tolerance = 0.01;

    public W.Paragraph Write(TextDocument document)
    {
        var separator = document.Notes.FootnoteSeparator;
        if (separator == new NoteSeparator()) return new W.Paragraph(new W.Run(new W.SeparatorMark()));
        var properties = new W.ParagraphProperties(
            new W.SpacingBetweenLines { Before = Twips(separator.SpaceBefore), After = Twips(separator.SpaceAfter), Line = "240", LineRule = W.LineSpacingRuleValues.Auto });
        if (separator.Thickness > 0)
        {
            properties.Append(new W.ParagraphBorders(new W.TopBorder
            {
                Val = W.BorderValues.Single,
                Size = (uint)Math.Clamp(Math.Round(separator.Thickness * EighthsPerPoint), 2, 96),
                Space = 0,
                Color = "auto",
            }));
            properties.Append(new W.Indentation { Right = Twips((1 - separator.RelativeLength) * TextWidth(document)) });
        }
        return new W.Paragraph(properties, new W.Run(new W.RunProperties(new W.FontSize { Val = "2" }), new W.Text(string.Empty)));
    }

    public NoteSeparator Read(W.FootnoteEndnoteType? note, TextDocument document)
    {
        var defaults = new NoteSeparator();
        var paragraph = note?.GetFirstChild<W.Paragraph>();
        if (paragraph is null) return defaults;
        var properties = paragraph.ParagraphProperties;
        var spacing = properties?.SpacingBetweenLines;
        var border = properties?.ParagraphBorders?.TopBorder;
        var marked = paragraph.Descendants<W.SeparatorMark>().Any();
        var thickness = border?.Size?.Value is { } size && border.Val?.Value != W.BorderValues.None && border.Val?.Value != W.BorderValues.Nil
            ? size / EighthsPerPoint
            : marked ? defaults.Thickness : 0;
        var relative = defaults.RelativeLength;
        if (border is not null && !marked)
        {
            var width = TextWidth(document);
            var right = Points(properties?.Indentation?.Right?.Value) ?? 0;
            relative = width > 0 ? Math.Clamp(1 - right / width, 0, 1) : 1;
        }
        return new NoteSeparator
        {
            RelativeLength = Settled(relative, defaults.RelativeLength),
            Thickness = Settled(thickness, defaults.Thickness),
            SpaceBefore = Settled(Points(spacing?.Before?.Value) ?? defaults.SpaceBefore, defaults.SpaceBefore),
            SpaceAfter = Settled(Points(spacing?.After?.Value) ?? defaults.SpaceAfter, defaults.SpaceAfter),
        };
    }

    private double TextWidth(TextDocument document) =>
        document.Styles.Page.Find(document.InitialPageStyle) is { } page
            ? Math.Max(0, (page.Width - page.Margins.Left - page.Margins.Right - page.Gutter).ToPoints())
            : 0;

    private string Twips(double points) => Math.Max(0, Math.Round(points * PulseOffice.Foundation.Units.Twip.PerPoint)).ToString(CultureInfo.InvariantCulture);

    private double? Points(string? twips) =>
        double.TryParse(twips, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value / PulseOffice.Foundation.Units.Twip.PerPoint : null;

    private double Settled(double value, double standard) => Math.Abs(value - standard) < Tolerance ? standard : value;
}
