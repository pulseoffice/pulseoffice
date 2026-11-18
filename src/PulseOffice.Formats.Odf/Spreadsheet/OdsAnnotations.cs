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

    public XName Reply => Pulse + "reply";

    public XName ReplyHeader => Pulse + "reply-header";

    public XName Creator => Pulse + "creator";

    public XName Date => Pulse + "date";

    public XName Mention => Pulse + "mention";

    public XName Start => Pulse + "start";

    public XName Length => Pulse + "length";

    public XName Person => Pulse + "person";

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
    private readonly CommentMentions _mentions = new();

    public void Place(XElement cell, CellComment comment, Worksheet sheet, CellAddress address)
    {
        var name = _names.NameOf(address);
        var root = Annotation(comment.Text, comment.Author, comment.CreatedAt, comment.IsAlwaysVisible);
        if (comment.AnnotatedText is not null) root.Add(new XAttribute(_names.Name, name));
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

        for (var index = 0; index < comment.Replies.Count; index++) AddReply(root, comment.Replies[index], index);
        AddMentions(root, comment.Text, comment.Mentions, null);
        for (var index = 0; index < comment.Replies.Count; index++) AddMentions(root, comment.Replies[index].Text, comment.Replies[index].Mentions, index);

        if (comment.AnnotatedText is { } span && Locate(cell, span.Start + span.Length) is { } end
            && Insert(end.Paragraph, end.Offset, new XElement(_names.AnnotationEnd, new XAttribute(_names.Name, name))))
        {
            if (span.Start == 0 || Locate(cell, span.Start) is not { } start || !Insert(start.Paragraph, start.Offset, root)) cell.AddFirst(root);
            return;
        }

        cell.AddFirst(root);
    }

    private (XElement Paragraph, int Offset)? Locate(XElement cell, int offset)
    {
        var origin = 0;
        foreach (var paragraph in cell.Elements(_names.Paragraph))
        {
            var length = paragraphs.Line(paragraph).Length;
            if (offset <= origin + length) return (paragraph, offset - origin);
            origin += length + 1;
        }

        return null;
    }

    public int CellOffsetOf(XElement cell, XElement marker)
    {
        if (marker.Parent == cell) return 0;
        var origin = 0;
        foreach (var paragraph in cell.Elements(_names.Paragraph))
        {
            if (marker.Ancestors(_names.Paragraph).Contains(paragraph)) return origin + OffsetOf(paragraph, marker);
            origin += paragraphs.Line(paragraph).Length + 1;
        }

        return -1;
    }

    private void AddReply(XElement root, CommentReply reply, int index)
    {
        var order = index.ToString(CultureInfo.InvariantCulture);
        var header = paragraphs.Paragraph(Header(reply));
        header.Add(new XAttribute(_names.Reply, order), new XAttribute(_names.ReplyHeader, "true"), new XAttribute(_names.Date, Stamp(reply.CreatedAt)));
        if (reply.Author is { Length: > 0 } author) header.Add(new XAttribute(_names.Creator, author));
        root.Add(header);
        foreach (var line in paragraphs.Paragraphs(reply.Text))
        {
            line.Add(new XAttribute(_names.Reply, order));
            root.Add(line);
        }
    }

    private void AddMentions(XElement root, string text, IEnumerable<CommentMention> mentions, int? reply)
    {
        foreach (var mention in _mentions.Kept(text, mentions))
        {
            var element = new XElement(_names.Mention,
                new XAttribute(_names.Start, mention.Start.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(_names.Length, mention.Length.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(_names.Person, mention.Person));
            if (reply is { } index) element.Add(new XAttribute(_names.Reply, index.ToString(CultureInfo.InvariantCulture)));
            root.Add(element);
        }
    }

    private string Header(CommentReply reply)
    {
        var date = reply.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return reply.Author is { Length: > 0 } author ? $"{author}, {date}:" : $"{date}:";
    }

    public string Stamp(DateTimeOffset created) => created.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private XElement Annotation(string text, string? author, DateTimeOffset created, bool display)
    {
        var annotation = new XElement(_names.Annotation, new XAttribute(Namespaces.Office + "display", display ? "true" : "false"));
        if (author is { Length: > 0 }) annotation.Add(new XElement(Namespaces.Dc + "creator", author));
        annotation.Add(new XElement(Namespaces.Dc + "date", Stamp(created)));
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
    private readonly CommentMentions _mentions = new();

    private const int MaximumReplies = 10_000;

    public void Read(XElement table, Worksheet sheet, OdsReadContext context)
    {
        foreach (var cell in cells.In(table))
        {
            var annotations = cell.Element.Elements(_names.Annotation)
                .Concat(cell.Element.Elements(_names.Paragraph).SelectMany(paragraph => paragraph.Descendants(_names.Annotation)))
                .ToList();
            if (annotations.FirstOrDefault(annotation => annotation.Attribute(_names.ParentName) is null) is not { } root) continue;

            var name = root.Attribute(_names.Name)?.Value;
            var structured = root.Elements(_names.Paragraph).Any(paragraph => paragraph.Attribute(_names.Reply) is not null);
            var text = structured
                ? string.Join('\n', root.Elements(_names.Paragraph).Where(paragraph => paragraph.Attribute(_names.Reply) is null).Select(paragraphs.Line))
                : paragraphs.Read(root);
            var replies = structured
                ? StructuredReplies(root)
                : name is null
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
                Mentions = Mentions(root, text, null),
                Box = Box(root, sheet, cell.Address),
                AnnotatedText = name is null ? null : Span(cell.Element, root, name),
            };
        }
    }

    private List<CommentReply> StructuredReplies(XElement root)
    {
        var replies = new List<CommentReply>();
        foreach (var header in root.Elements(_names.Paragraph).Where(paragraph => paragraph.Attribute(_names.ReplyHeader)?.Value == "true"))
        {
            if (replies.Count >= MaximumReplies) break;
            var order = header.Attribute(_names.Reply)?.Value ?? string.Empty;
            var text = string.Join('\n', root.Elements(_names.Paragraph)
                .Where(paragraph => paragraph.Attribute(_names.Reply)?.Value == order && paragraph.Attribute(_names.ReplyHeader) is null)
                .Select(paragraphs.Line));
            replies.Add(new CommentReply
            {
                Text = text,
                Author = header.Attribute(_names.Creator)?.Value,
                CreatedAt = Parsed(header.Attribute(_names.Date)?.Value),
                Mentions = int.TryParse(order, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? Mentions(root, text, index) : [],
            });
        }

        return replies;
    }

    private IReadOnlyList<CommentMention> Mentions(XElement root, string text, int? reply) =>
        _mentions.Kept(text, root.Elements(_names.Mention)
            .Where(mention => Number(mention.Attribute(_names.Reply)?.Value) == reply)
            .Select(mention => (Start: Number(mention.Attribute(_names.Start)?.Value), Length: Number(mention.Attribute(_names.Length)?.Value), Person: mention.Attribute(_names.Person)?.Value))
            .Where(mention => mention.Start is not null && mention.Length is not null && mention.Person is not null)
            .Select(mention => new CommentMention(mention.Start!.Value, mention.Length!.Value, mention.Person!)));

    private int? Number(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

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
        if (cell.Elements(_names.Paragraph).SelectMany(paragraph => paragraph.Descendants(_names.AnnotationEnd)).FirstOrDefault(end => end.Attribute(_names.Name)?.Value == name) is not { } end) return null;
        var start = Math.Max(_offsets.CellOffsetOf(cell, root), 0);
        var stop = _offsets.CellOffsetOf(cell, end);
        return stop > start ? new CommentTextSpan(start, stop - start) : null;
    }

    private string? Author(XElement annotation) => annotation.Element(Namespaces.Dc + "creator")?.Value;

    private DateTimeOffset Created(XElement annotation) => Parsed(annotation.Element(Namespaces.Dc + "date")?.Value);

    private DateTimeOffset Parsed(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created) ? created : DateTimeOffset.Now;
}
