using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdsAnnotationVocabulary
{
    public XNamespace Pulse { get; } = "urn:pulseoffice:spreadsheet";

    public XName ParentName => Pulse + "parent-name";

    public XName Resolved => Pulse + "resolved";

    public XName Annotation => Namespaces.Office + "annotation";

    public XName AnnotationEnd => Namespaces.Office + "annotation-end";

    public XName Name => Namespaces.Office + "name";

    public XName Paragraph => Namespaces.Text + "p";

    public string NameOf(CellAddress address) => "__Annotation__" + address.ToA1();
}

internal sealed class OdsAnnotationBuilder(IOdfParagraphText paragraphs)
{
    private const char Marker = '';

    private readonly OdsAnnotationVocabulary _names = new();
    private readonly CommentBoxGeometry _geometry = new();

    public void Place(XElement cell, CellComment comment, Worksheet sheet, CellAddress address)
    {
        var named = comment.Replies.Count > 0 || comment.AnnotatedText is not null;
        var name = _names.NameOf(address);
        var root = Annotation(comment.Text, comment.Author, comment.CreatedAt, comment.IsAlwaysVisible);
        if (named) root.Add(new XAttribute(_names.Name, name));
        if (comment.IsResolved) root.Add(new XAttribute(_names.Resolved, "true"));
        if (comment.Box is { } box)
        {
            var bounds = _geometry.Bounds(sheet.Layout, address, box);
            root.Add(
                new XAttribute(Namespaces.Svg + "x", Points(bounds.X)),
                new XAttribute(Namespaces.Svg + "y", Points(bounds.Y)),
                new XAttribute(Namespaces.Svg + "width", Points(bounds.Width)),
                new XAttribute(Namespaces.Svg + "height", Points(bounds.Height)));
        }

        var replies = comment.Replies.Select(reply =>
        {
            var annotation = Annotation(reply.Text, reply.Author, reply.CreatedAt, false);
            annotation.Add(new XAttribute(_names.ParentName, name));
            return annotation;
        }).ToList();

        var paragraph = cell.Element(_names.Paragraph);
        if (comment.AnnotatedText is { } span && paragraph is not null && Insert(paragraph, span.Start + span.Length, new XElement(_names.AnnotationEnd, new XAttribute(_names.Name, name))))
        {
            if (span.Start > 0 && Insert(paragraph, span.Start, root)) cell.AddFirst(replies);
            else cell.AddFirst(new object[] { root }.Concat(replies));
            return;
        }

        cell.AddFirst(new object[] { root }.Concat(replies));
    }

    private XElement Annotation(string text, string? author, DateTimeOffset created, bool display)
    {
        var annotation = new XElement(_names.Annotation, new XAttribute(Namespaces.Office + "display", display ? "true" : "false"));
        if (author is { Length: > 0 }) annotation.Add(new XElement(Namespaces.Dc + "creator", author));
        annotation.Add(new XElement(Namespaces.Dc + "date", created.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
        annotation.Add(paragraphs.Paragraphs(text));
        return annotation;
    }

    private bool Insert(XElement paragraph, int offset, XElement marker)
    {
        var position = 0;
        foreach (var node in paragraph.Nodes().ToList())
        {
            if (position == offset)
            {
                node.AddBeforeSelf(marker);
                return true;
            }

            switch (node)
            {
                case XText text when position + text.Value.Length > offset:
                    var split = offset - position;
                    text.AddBeforeSelf(new XText(text.Value[..split]), marker);
                    text.Value = text.Value[split..];
                    return true;
                case XText text:
                    position += text.Value.Length;
                    break;
                case XElement element when element.Name == Namespaces.Text + "s":
                    position += int.TryParse((string?)element.Attribute(Namespaces.Text + "c"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 1;
                    break;
                case XElement element when element.Name.Namespace == Namespaces.Office:
                    break;
                case XElement element:
                    position += paragraphs.Line(new XElement(_names.Paragraph, element)).Length;
                    break;
            }

            if (position > offset) return false;
        }

        if (position != offset) return false;
        paragraph.Add(marker);
        return true;
    }

    private string Points(double value) => value.ToString("0.##", CultureInfo.InvariantCulture) + "pt";

    public int OffsetOf(XElement paragraph, XElement marker)
    {
        var copy = new XElement(paragraph);
        var path = Path(paragraph, marker);
        if (path is null) return 0;
        XElement current = copy;
        foreach (var index in path) current = (XElement)current.Nodes().ElementAt(index);
        current.ReplaceWith(new XText(Marker.ToString()));
        var line = paragraphs.Line(copy);
        return Math.Max(line.IndexOf(Marker, StringComparison.Ordinal), 0);
    }

    private List<int>? Path(XElement root, XElement target)
    {
        var path = new List<int>();
        for (var node = target; node != root; node = node.Parent!)
        {
            if (node.Parent is null) return null;
            path.Insert(0, node.Parent.Nodes().ToList().IndexOf(node));
        }
        return path;
    }
}

internal sealed class CommentPartReader(TableCells cells, IOdfParagraphText paragraphs) : IOdsSheetPartReader
{
    private readonly OdsAnnotationVocabulary _names = new();
    private readonly OdsAnnotationBuilder _offsets = new(paragraphs);
    private readonly OdfLength _length = new();
    private readonly CommentBoxGeometry _geometry = new();

    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        foreach (var cell in cells.In(table))
        {
            var annotations = cell.Element.Elements(_names.Annotation)
                .Concat(cell.Element.Elements(_names.Paragraph).SelectMany(paragraph => paragraph.Descendants(_names.Annotation)))
                .ToList();
            if (annotations.FirstOrDefault(annotation => annotation.Attribute(_names.ParentName) is null) is not { } root) continue;

            var text = paragraphs.Read(root);
            var name = root.Attribute(_names.Name)?.Value;
            var replies = name is null
                ? []
                : annotations.Where(annotation => annotation.Attribute(_names.ParentName)?.Value == name)
                    .Select(reply => new CommentReply { Text = paragraphs.Read(reply), Author = Author(reply), CreatedAt = Created(reply) })
                    .ToList();
            if (text.Length == 0 && replies.Count == 0) continue;

            sheet.Comments[cell.Address] = new CellComment
            {
                Text = text,
                Author = Author(root),
                IsAlwaysVisible = root.Attribute(Namespaces.Office + "display")?.Value == "true",
                CreatedAt = Created(root),
                IsResolved = root.Attribute(_names.Resolved)?.Value == "true",
                Replies = replies,
                Box = Box(root, sheet, cell.Address),
                AnnotatedText = name is null ? null : Span(cell.Element, root, name),
            };
        }
    }

    private CommentBox? Box(XElement annotation, Worksheet sheet, CellAddress address)
    {
        if (_length.Parse(annotation.Attribute(Namespaces.Svg + "x")?.Value) is not { } x
            || _length.Parse(annotation.Attribute(Namespaces.Svg + "y")?.Value) is not { } y
            || _length.Parse(annotation.Attribute(Namespaces.Svg + "width")?.Value) is not { } width
            || _length.Parse(annotation.Attribute(Namespaces.Svg + "height")?.Value) is not { } height
            || width.Value <= 0 || height.Value <= 0) return null;
        return _geometry.Relative(sheet.Layout, address, new NoteBounds(x.ToPoints(), y.ToPoints(), width.ToPoints(), height.ToPoints()));
    }

    private CommentTextSpan? Span(XElement cell, XElement root, string name)
    {
        if (cell.Elements(_names.Paragraph).FirstOrDefault() is not { } paragraph) return null;
        if (paragraph.Descendants(_names.AnnotationEnd).FirstOrDefault(end => end.Attribute(_names.Name)?.Value == name) is not { } end) return null;
        var start = root.Parent == cell ? 0 : root.Ancestors(_names.Paragraph).FirstOrDefault() == paragraph ? _offsets.OffsetOf(paragraph, root) : 0;
        var stop = _offsets.OffsetOf(paragraph, end);
        return stop > start ? new CommentTextSpan(start, stop - start) : null;
    }

    private string? Author(XElement annotation) => annotation.Element(Namespaces.Dc + "creator")?.Value;

    private DateTimeOffset Created(XElement annotation) =>
        DateTimeOffset.TryParse(annotation.Element(Namespaces.Dc + "date")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created) ? created : DateTimeOffset.Now;
}
