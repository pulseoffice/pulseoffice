using PulseOffice.Foundation.Colors;
using CellVerticalAlign = PulseOffice.Documents.Blocks.CellVerticalAlign;
using System.Globalization;
using System.Net;
using System.Text;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Interchange.Text.Outline;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Html;

public sealed record HtmlFlavor(bool Xhtml, bool HeadingIds)
{
    public static readonly HtmlFlavor Html = new(false, false);

    public static readonly HtmlFlavor Strict = new(true, true);
}

public sealed record HtmlHeading(int Level, string Id, string Text);

public sealed record HtmlPage(string Markup, IReadOnlyList<HtmlHeading> Headings);

public interface IHtmlImageSink
{
    string Source(RasterImageObject image);
}

public sealed class InlineImageSink : IHtmlImageSink
{
    public string Source(RasterImageObject image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return "data:" + image.MediaType + ";base64," + Convert.ToBase64String(image.Data.Span);
    }
}

public interface IHtmlPageRenderer
{
    HtmlPage Render(TextDocument document, HtmlFlavor flavor, IHtmlImageSink images);
}

public sealed class HtmlPageRenderer(IParagraphSemantics semantics, IStyleResolver resolver, IDirectFormatting direct) : IHtmlPageRenderer
{
    public HtmlPage Render(TextDocument document, HtmlFlavor flavor, IHtmlImageSink images)
    {
        ArgumentNullException.ThrowIfNull(document);
        var session = new HtmlWriteSession(document, semantics, resolver, direct, flavor, images);
        return new HtmlPage(session.Render(), session.Headings);
    }
}

public sealed class XhtmlDocumentWriter(IHtmlPageRenderer pages, IHtmlFidelity fidelity) : ITextDocumentWriter
{
    public TextDocumentFormat Format => TextInterchangeFormats.Xhtml;

    public FidelityReport CheckFidelity(TextDocument document) => fidelity.Check(document);

    public OperationResult<bool> Write(TextDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            var page = pages.Render(document, HtmlFlavor.Strict, new InlineImageSink());
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.Write(page.Markup);
            writer.Flush();
            return OperationResult<bool>.Ok(true);
        }
        catch (IOException exception)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("HTML200", $"Writing the HTML file failed: {exception.Message}").With(exception.Message));
        }
    }
}

public interface IHtmlFidelity
{
    FidelityReport Check(TextDocument document);
}

public sealed class HtmlFidelity : IHtmlFidelity
{
    public FidelityReport Check(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var losses = new List<Diagnostic>
        {
            Diagnostic.Warning("HTML201", "HTML has no pages: page styles and columns are lost; only the header and footer of the first page style are kept."),
        };
        if (document.Objects.All.Any(item => item.Content is not (EmbeddedContent { Value: RasterImageObject } or TextBoxContent)))
            losses.Add(Diagnostic.Warning("HTML202", "Only images and text boxes are written to HTML; shapes, charts, formulas and other objects are lost."));
        if (document.Objects.All.Any(item => item.Content is TextBoxContent && item.Anchor is not (ParagraphAnchor or CharacterAnchor) || item.Placement.X.Value != 0 || item.Placement.Y.Value != 0))
            losses.Add(Diagnostic.Info("HTML206", "Objects keep their size but not their position or wrapping in HTML."));
        if (document.Comments.All.Count > 0 || document.Changes.All.Count > 0)
            losses.Add(Diagnostic.Warning("HTML203", "Comments and tracked changes are not written to HTML."));
        if (Paragraphs(document.Body).Any(paragraph => paragraph.Marks.OfType<BookmarkMark>().Any(mark => mark.Edge != MarkEdge.Point)))
            losses.Add(Diagnostic.Info("HTML204", "Bookmarks become positions in HTML: the marked range is lost."));
        if (document.Macros.All.Any())
            losses.Add(Diagnostic.Warning("HTML205", "Macros are not written to HTML."));
        return new FidelityReport(losses);
    }

    private IEnumerable<ParagraphBlock> Paragraphs(BlockContainer container) => container.Blocks.SelectMany(block => block switch
    {
        ParagraphBlock paragraph => [paragraph],
        TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Paragraphs(cell.Content)),
        SectionBlock section => Paragraphs(section.Content),
        _ => (IEnumerable<ParagraphBlock>)[],
    });
}

public sealed class HtmlDocumentWriter(IHtmlPageRenderer pages, IHtmlFidelity fidelity) : ITextDocumentWriter
{
    public TextDocumentFormat Format => TextInterchangeFormats.Html;

    public FidelityReport CheckFidelity(TextDocument document) => fidelity.Check(document);

    public OperationResult<bool> Write(TextDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            var page = pages.Render(document, HtmlFlavor.Html, new InlineImageSink()).Markup;
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.Write(page);
            writer.Flush();
            return OperationResult<bool>.Ok(true);
        }
        catch (IOException exception)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("HTML200", $"Writing the HTML file failed: {exception.Message}").With(exception.Message));
        }
    }
}

internal sealed class HtmlWriteSession(TextDocument document, IParagraphSemantics semantics, IStyleResolver resolver, IDirectFormatting direct, HtmlFlavor flavor, IHtmlImageSink images)
{
    private readonly List<HtmlHeading> _headings = [];
    private HashSet<string>? _bookmarkNames;
    private int _headingNumber;

    public IReadOnlyList<HtmlHeading> Headings => _headings;

    private string Close => flavor.Xhtml ? "/>" : ">";

    private string Anchor => flavor.Xhtml ? "id" : "name";

    private readonly StringBuilder _html = new();
    private readonly List<(ListKind Kind, NumberFormat Format, string Thread, int Next)> _lists = [];
    private readonly ListThreadCounters _counters = new(document.Styles);
    private readonly List<bool> _items = [];
    private readonly List<(Note Note, string Name)> _notes = [];
    private readonly HashSet<string> _paragraphStyles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _characterStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _classNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _takenClassNames = new(StringComparer.Ordinal);

    public string Render()
    {
        var body = new StringBuilder();
        if (document.Styles.Page.Find(document.InitialPageStyle) is { Header.Enabled: true } headed) Region("header", headed.Header.Default, body);
        Blocks(document.Body, body);
        CloseLists(body, 0);
        foreach (var (note, name) in _notes.ToList())
        {
            var kind = note.Kind == NoteKind.Footnote ? "sdfootnote" : "sdendnote";
            body.Append("<div id=\"").Append(name).Append("\">\n");
            var first = true;
            foreach (var block in note.Content.Blocks)
            {
                if (first && block is ParagraphBlock paragraph)
                {
                    var anchor = $"<a class=\"{kind}sym\" {Anchor}=\"{name}sym\" href=\"#{name}anc\">{Number(name)}</a>";
                    Paragraph(paragraph, body, anchor);
                }
                else Block(block, body);
                first = false;
            }
            CloseLists(body, 0);
            body.Append("</div>\n");
        }

        if (document.Styles.Page.Find(document.InitialPageStyle) is { Footer.Enabled: true } footed) Region("footer", footed.Footer.Default, body);

        var title = document.Properties.Title is { Length: > 0 } named ? named : _headings.FirstOrDefault(heading => heading.Text.Length > 0)?.Text ?? "Document";
        if (flavor.Xhtml) _html.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
        _html.Append("<!DOCTYPE html>\n<html");
        if (flavor.Xhtml) _html.Append(" xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\"");
        if (document.Language is { Length: > 0 } language)
        {
            _html.Append(" lang=\"").Append(Attribute(language)).Append('"');
            if (flavor.Xhtml) _html.Append(" xml:lang=\"").Append(Attribute(language)).Append('"');
        }
        _html.Append(">\n<head>\n<meta charset=\"utf-8\"").Append(Close).Append("\n<title>").Append(Escape(title)).Append("</title>\n");
        if (document.Properties.Author is { Length: > 0 } author) _html.Append("<meta name=\"author\" content=\"").Append(Attribute(author)).Append('"').Append(Close).Append('\n');
        _html.Append("<style>\nbody{max-width:48em;margin:2em auto;line-height:1.4}\ntable{border-collapse:collapse}\ntd,th{padding:2px 6px;vertical-align:top}\n");
        foreach (var name in _paragraphStyles.Order(StringComparer.Ordinal)) _html.Append('.').Append(ClassName("p", name)).Append('{').Append(ParagraphCss(name)).Append("}\n");
        foreach (var name in _characterStyles.Order(StringComparer.Ordinal)) _html.Append('.').Append(ClassName("c", name)).Append('{').Append(CharacterCss(resolver.ResolveCharacterStyle(document.Styles, name))).Append("}\n");
        _html.Append("</style>\n</head>\n<body>\n").Append(body).Append("</body>\n</html>\n");
        return _html.ToString();
    }

    private void Region(string kind, BlockContainer content, StringBuilder html)
    {
        html.Append("<div title=\"").Append(kind).Append("\">\n");
        Blocks(content, html);
        CloseLists(html, 0);
        html.Append("</div>\n");
    }

    private string Number(string name) => new(name.Where(char.IsAsciiDigit).ToArray());

    private void Blocks(BlockContainer container, StringBuilder html)
    {
        foreach (var block in container.Blocks) Block(block, html);
    }

    private void Block(Block block, StringBuilder html)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                Paragraph(paragraph, html, null);
                break;
            case TableBlock table:
                CloseLists(html, 0);
                Table(table, html);
                break;
            case SectionBlock section:
                CloseLists(html, 0);
                html.Append("<div data-section=\"").Append(Attribute(section.Name)).Append("\">\n");
                Blocks(section.Content, html);
                CloseLists(html, 0);
                html.Append("</div>\n");
                break;
        }
    }

    private void Table(TableBlock table, StringBuilder html)
    {
        html.Append("<table>\n<colgroup>");
        foreach (var column in table.Columns)
            html.Append("<col style=\"width:").Append(Points(column.ToPoints())).Append('"').Append(Close);
        html.Append("</colgroup>\n");
        for (var r = 0; r < table.Rows.Count; r++)
        {
            html.Append("<tr>\n");
            foreach (var cell in table.Rows[r].Cells.Where(cell => !cell.Covered))
            {
                var tag = r < table.HeaderRows ? "th" : "td";
                html.Append('<').Append(tag);
                if (cell.ColumnSpan > 1) html.Append(" colspan=\"").Append(cell.ColumnSpan).Append('"');
                if (cell.RowSpan > 1) html.Append(" rowspan=\"").Append(cell.RowSpan).Append('"');
                var style = new List<string>();
                if (cell.Background is { } background) style.Add("background-color:" + Color(background));
                if (cell.VerticalAlign != CellVerticalAlign.Top) style.Add("vertical-align:" + (cell.VerticalAlign == CellVerticalAlign.Middle ? "middle" : "bottom"));
                if (cell.Borders is { HasLines: true } borders)
                    foreach (var side in (ReadOnlySpan<BorderSide>)[BorderSide.Top, BorderSide.Right, BorderSide.Bottom, BorderSide.Left])
                        if (borders.Line(side) is { } line) style.Add("border-" + side.ToString().ToLowerInvariant() + ":" + BorderCss(line));
                if (cell.Borders is { } box)
                    style.Add("padding:" + Points(box.PaddingTop) + " " + Points(box.PaddingRight) + " " + Points(box.PaddingBottom) + " " + Points(box.PaddingLeft));
                if (style.Count > 0) html.Append(" style=\"").Append(string.Join(";", style)).Append('"');
                html.Append(">\n");
                var savedLists = _lists.ToList();
                var savedItems = _items.ToList();
                _lists.Clear();
                _items.Clear();
                Blocks(cell.Content, html);
                CloseLists(html, 0);
                _lists.AddRange(savedLists);
                _items.AddRange(savedItems);
                html.Append("</").Append(tag).Append(">\n");
            }
            html.Append("</tr>\n");
        }
        html.Append("</table>\n");
    }

    private void Paragraph(ParagraphBlock block, StringBuilder html, string? prefix)
    {
        var content = block.Content;
        var heading = semantics.HeadingLevel(document.Styles, content);
        var list = semantics.List(document.Styles, content);
        int? itemValue = null;
        if (list.Kind == ListKind.None) CloseLists(html, 0);
        else itemValue = OpenLists(html, list, content with { ListStyleName = list.StyleName });

        var tag = list.Kind != ListKind.None ? "li" : heading > 0 ? "h" + Math.Min(heading, 6).ToString(CultureInfo.InvariantCulture) : "p";
        var frames = document.Objects.All.Where(item => item.Content is TextBoxContent && item.Anchor is ParagraphAnchor or CharacterAnchor && item.Anchor.Accept(new AnchorParagraph()) == block.Id).ToList();
        if (flavor.Xhtml && tag != "li")
            foreach (var frame in frames) Frame(frame, (TextBoxContent)frame.Content, html);
        html.Append('<').Append(tag);
        if (itemValue is { } value) html.Append(" value=\"").Append(value.ToString(CultureInfo.InvariantCulture)).Append('"');
        if (flavor.HeadingIds && heading > 0)
        {
            var id = HeadingId();
            _headings.Add(new HtmlHeading(heading, id, content.Text.Trim()));
            html.Append(" id=\"").Append(id).Append('"');
        }
        var styleName = content.StyleName;
        if (styleName is { Length: > 0 } && styleName != DefaultStyleNames.Standard)
        {
            _paragraphStyles.Add(styleName);
            html.Append(" class=\"").Append(ClassName("p", styleName)).Append("\" data-style=\"").Append(Attribute(styleName)).Append('"');
        }

        var own = direct.Paragraph(content);
        if (own.Direction is { } direction) html.Append(" dir=\"").Append(direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft ? "rtl" : "ltr").Append('"');
        var css = ParagraphCss(own);
        if (tag == "li" && content.ListHeader)
        {
            html.Append(" data-list-header=\"true\"");
            css += "list-style-type:none;counter-increment:list-item 0;";
        }
        if (content.Text.Contains("  ", StringComparison.Ordinal) || content.Text.Contains('\t') || content.Text.StartsWith(' ') || content.Text.EndsWith(' '))
            css += "white-space:pre-wrap;";
        if (css.Length > 0) html.Append(" style=\"").Append(css).Append('"');
        html.Append('>');
        if (prefix is not null) html.Append(prefix);
        if (!flavor.Xhtml)
            foreach (var frame in frames) Frame(frame, (TextBoxContent)frame.Content, html);
        Inline(block, html);
        if (tag != "li") html.Append("</").Append(tag).Append(">\n");
    }

    private string HeadingId()
    {
        _bookmarkNames ??= BookmarkNames(document.Body)
            .Concat(document.Notes.All.SelectMany(note => BookmarkNames(note.Content)))
            .ToHashSet(StringComparer.Ordinal);
        string id;
        do id = "h" + (++_headingNumber).ToString(CultureInfo.InvariantCulture);
        while (_bookmarkNames.Contains(id));
        return id;
    }

    private IEnumerable<string> BookmarkNames(BlockContainer container) => container.Blocks.SelectMany(block => block switch
    {
        ParagraphBlock paragraph => paragraph.Marks.OfType<BookmarkMark>().Select(mark => mark.Name),
        TableBlock table => table.Rows.SelectMany(row => row.Cells).SelectMany(cell => BookmarkNames(cell.Content)),
        _ => [],
    });

    private void Frame(AnchoredObject frame, TextBoxContent box, StringBuilder html)
    {
        var wrapper = flavor.Xhtml ? "div" : "span";
        html.Append('<').Append(wrapper).Append(" style=\"float:left;width:").Append(Points(frame.Size.Width.ToPoints()));
        html.Append(box.AutoGrowHeight ? ";min-height:" : ";height:").Append(Points(frame.Size.Height.ToPoints()));
        html.Append("\">");
        var savedLists = _lists.ToList();
        var savedItems = _items.ToList();
        _lists.Clear();
        _items.Clear();
        var inner = new StringBuilder();
        Blocks(box.Content, inner);
        CloseLists(inner, 0);
        _lists.AddRange(savedLists);
        _items.AddRange(savedItems);
        html.Append(inner.ToString().Replace(">\n<", "><", StringComparison.Ordinal).TrimEnd('\n')).Append("</").Append(wrapper).Append('>');
    }

    private void Inline(ParagraphBlock block, StringBuilder html)
    {
        var marks = block.Marks.OrderBy(mark => mark.Offset).ToList();
        var markIndex = 0;
        var offset = 0;
        foreach (var run in block.Content.Runs)
        {
            var start = 0;
            while (start <= run.Text.Length)
            {
                while (markIndex < marks.Count && marks[markIndex].Offset <= offset + start)
                {
                    Mark(marks[markIndex], html);
                    markIndex++;
                }
                var next = markIndex < marks.Count ? Math.Min(run.Text.Length, marks[markIndex].Offset - offset) : run.Text.Length;
                if (next <= start)
                {
                    if (start >= run.Text.Length) break;
                    next = run.Text.Length;
                }
                Run(run, run.Text[start..next], html);
                start = next;
                if (start >= run.Text.Length) break;
            }
            offset += run.Text.Length;
        }
        for (; markIndex < marks.Count; markIndex++) Mark(marks[markIndex], html);
    }

    private void Mark(InlineMark mark, StringBuilder html)
    {
        switch (mark)
        {
            case BookmarkMark { Edge: not MarkEdge.End } bookmark:
                html.Append("<a id=\"").Append(Attribute(bookmark.Name)).Append("\"></a>");
                break;
            case NoteMark note when document.Notes.All.FirstOrDefault(item => item.Id == note.NoteId) is { } found:
                var kind = found.Kind == NoteKind.Footnote ? "sdfootnote" : "sdendnote";
                var number = _notes.Count(item => item.Note.Kind == found.Kind) + 1;
                var name = kind + number.ToString(CultureInfo.InvariantCulture);
                _notes.Add((found, name));
                html.Append("<a class=\"").Append(kind).Append("anc\" ").Append(Anchor).Append("=\"").Append(name).Append("anc\" href=\"#").Append(name).Append("sym\"><sup>")
                    .Append(Escape(found.Citation ?? number.ToString(CultureInfo.InvariantCulture))).Append("</sup></a>");
                break;
            case ObjectMark objectMark when document.Objects.Find(objectMark.ObjectId) is { Content: EmbeddedContent { Value: RasterImageObject image } } item:
                html.Append("<img src=\"").Append(Attribute(images.Source(image)))
                    .Append("\" style=\"width:").Append(Points(item.Size.Width.ToPoints())).Append(";height:").Append(Points(item.Size.Height.ToPoints())).Append('"');
                html.Append(" alt=\"").Append(Attribute(image.Description ?? image.Title ?? string.Empty)).Append('"').Append(Close);
                break;
        }
    }

    private void Run(TextRun run, string text, StringBuilder html)
    {
        if (text.Length == 0) return;
        var closers = new Stack<string>();
        if (run.Hyperlink is { Length: > 0 } link && Safe(link))
        {
            html.Append("<a href=\"").Append(Attribute(link)).Append("\">");
            closers.Push("</a>");
        }

        var sdfield = run.Field switch
        {
            TextFieldKind.PageNumber => "type=PAGE subtype=RANDOM format=PAGE",
            TextFieldKind.PageCount => "type=DOCSTAT subtype=PAGE format=PAGE",
            TextFieldKind.Author => "type=AUTHOR format=NAME",
            TextFieldKind.Title => "type=DOCINFO subtype=TITLE",
            TextFieldKind.FileName => "type=FILENAME format=NAME",
            _ => null,
        };
        if (flavor.Xhtml) sdfield = null;
        if (sdfield is not null)
        {
            html.Append("<sdfield ").Append(sdfield).Append('>');
            closers.Push("</sdfield>");
        }

        var format = direct.Run(run);
        var attributes = new StringBuilder();
        if (run.StyleName is { Length: > 0 } styleName && styleName != DefaultStyleNames.InternetLink)
        {
            _characterStyles.Add(styleName);
            attributes.Append(" class=\"").Append(ClassName("c", styleName)).Append("\" data-style=\"").Append(Attribute(styleName)).Append('"');
        }
        if (run.Field != TextFieldKind.None && sdfield is null) attributes.Append(" data-field=\"").Append(run.Field).Append('"');
        if (format.Language is { Length: > 0 } language) attributes.Append(" lang=\"").Append(Attribute(language)).Append('"');
        if (format.RightToLeft is true) attributes.Append(" dir=\"rtl\"");
        var css = CharacterCss(format with { Bold = null, Italic = null, Underline = null, Strikethrough = null, Baseline = null });
        if (css.Length > 0) attributes.Append(" style=\"").Append(css).Append('"');
        if (attributes.Length > 0)
        {
            html.Append("<span").Append(attributes).Append('>');
            closers.Push("</span>");
        }

        void Wrap(bool? on, string tag)
        {
            if (on is not true) return;
            html.Append('<').Append(tag).Append('>');
            closers.Push("</" + tag + ">");
        }

        Wrap(format.Bold, "strong");
        Wrap(format.Italic, "em");
        Wrap(format.Underline, "u");
        Wrap(format.Strikethrough, "s");
        Wrap(format.Baseline == TextBaseline.Superscript, "sup");
        Wrap(format.Baseline == TextBaseline.Subscript, "sub");
        if (format.Bold is false) AppendStyled(html, closers, "font-weight:normal");
        if (format.Italic is false) AppendStyled(html, closers, "font-style:normal");

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) html.Append("<br").Append(Close);
            html.Append(Escape(lines[i]));
        }
        while (closers.Count > 0) html.Append(closers.Pop());
    }

    private void AppendStyled(StringBuilder html, Stack<string> closers, string css)
    {
        html.Append("<span style=\"").Append(css).Append("\">");
        closers.Push("</span>");
    }

    private bool Safe(string link) =>
        link.StartsWith('#') || Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" or "ftp" or "file";

    private int? OpenLists(StringBuilder html, ListPlacement list, TextParagraph paragraph)
    {
        var depth = list.Level + 1;
        var thread = _counters.Thread(paragraph);
        if (_lists.Count > depth) CloseLists(html, depth);
        var foreign = _lists.FindIndex(open => open.Thread != thread);
        if (foreign >= 0) CloseLists(html, foreign);
        if (_lists.Count == depth && (_lists[^1].Kind != list.Kind || _lists[^1].Format != list.Format)) CloseLists(html, depth - 1);
        if (_lists.Count == depth && _items[^1])
        {
            html.Append("</li>\n");
            _items[^1] = false;
        }
        var numbered = list.Kind == ListKind.Numbered;
        int? itemValue = null;
        var value = numbered ? _counters.Next(paragraph, list.Level) : 0;
        if (numbered && paragraph is { ListRestart: not null, ListHeader: false }) itemValue = value;
        if (numbered && _lists.Count == depth)
        {
            if (value != _lists[^1].Next) itemValue = value;
            _lists[^1] = _lists[^1] with { Next = value + 1 };
        }
        while (_lists.Count < depth)
        {
            var tag = numbered ? "ol" : "ul";
            var innermost = _lists.Count == depth - 1;
            var start = !numbered ? 1 : innermost ? value : _counters.Upcoming(paragraph, _lists.Count);
            html.Append('<').Append(tag);
            if (start != 1) html.Append(" start=\"").Append(start.ToString(CultureInfo.InvariantCulture)).Append('"');
            var type = list.Format switch
            {
                NumberFormat.LowerLetter => "a",
                NumberFormat.UpperLetter => "A",
                NumberFormat.LowerRoman => "i",
                NumberFormat.UpperRoman => "I",
                _ => null,
            };
            if (list.Kind == ListKind.Numbered && type is not null) html.Append(" type=\"").Append(type).Append('"');
            html.Append('>');
            _lists.Add((list.Kind, list.Format, thread, innermost ? value + 1 : start));
            _items.Add(false);
        }
        _items[^1] = true;
        return itemValue;
    }

    private void CloseLists(StringBuilder html, int depth)
    {
        while (_lists.Count > depth)
        {
            if (_items[^1]) html.Append("</li>");
            html.Append(_lists[^1].Kind == ListKind.Numbered ? "</ol>" : "</ul>");
            _lists.RemoveAt(_lists.Count - 1);
            _items.RemoveAt(_items.Count - 1);
            if (_lists.Count == 0) html.Append('\n');
        }
    }

    private string ParagraphCss(string styleName)
    {
        var resolved = resolver.ResolveParagraphStyle(document.Styles, styleName);
        return ParagraphCss(resolved.Paragraph) + CharacterCss(resolved.Character);
    }

    private string ParagraphCss(ParagraphProperties properties)
    {
        var css = new StringBuilder();
        var rightToLeft = properties.Direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft;
        if (properties.Align is { } align && align != TextAlign.Left)
            css.Append("text-align:").Append(align switch { TextAlign.Center => "center", TextAlign.Right => rightToLeft ? "end" : "right", TextAlign.Justify => "justify", _ => "left" }).Append(';');
        if (properties.MarginLeft is { } left) css.Append("margin-left:").Append(Points(left)).Append(';');
        if (properties.MarginRight is { } right) css.Append("margin-right:").Append(Points(right)).Append(';');
        if (properties.Indent is { } indent) css.Append("text-indent:").Append(Points(indent)).Append(';');
        if (properties.SpaceBefore is { } before) css.Append("margin-top:").Append(Points(before)).Append(';');
        if (properties.SpaceAfter is { } after) css.Append("margin-bottom:").Append(Points(after)).Append(';');
        if (properties.PageBreakBefore is true) css.Append("page-break-before:always;");
        if (properties.KeepWithNext is true) css.Append("page-break-after:avoid;");
        if (properties.KeepTogether is true) css.Append("page-break-inside:avoid;");
        if (properties.Background is { } background) css.Append("background-color:").Append(Color(background)).Append(';');
        if (rightToLeft) css.Append("direction:rtl;");
        return css.ToString();
    }

    private string CharacterCss(CharacterProperties properties)
    {
        var css = new StringBuilder();
        if (properties.Font is { Length: > 0 } font) css.Append("font-family:'").Append(FontName(font)).Append("';");
        if (properties.FontSize is { } size) css.Append("font-size:").Append(Points(size)).Append(';');
        if (properties.Bold is { } bold) css.Append("font-weight:").Append(bold ? "bold" : "normal").Append(';');
        if (properties.Italic is { } italic) css.Append("font-style:").Append(italic ? "italic" : "normal").Append(';');
        var decorations = new List<string>();
        if (properties.Underline is true || properties.DoubleUnderline is true) decorations.Add("underline");
        if (properties.Strikethrough is true) decorations.Add("line-through");
        if (properties.Overline is true) decorations.Add("overline");
        if (decorations.Count > 0) css.Append("text-decoration:").Append(string.Join(' ', decorations)).Append(';');
        if (properties.DoubleUnderline is true) css.Append("text-decoration-style:double;");
        if (properties.Baseline is { } baseline && baseline != TextBaseline.Normal) css.Append("vertical-align:").Append(baseline == TextBaseline.Superscript ? "super" : "sub").Append(';');
        if (properties.Color is { } color) css.Append("color:").Append(Color(color)).Append(';');
        if (properties.Highlight is { } highlight) css.Append("background-color:").Append(Color(highlight)).Append(';');
        if (properties.SmallCaps is true) css.Append("font-variant:small-caps;");
        if (properties.Case is { } textCase && textCase != TextCase.None)
            css.Append("text-transform:").Append(textCase switch { TextCase.Upper => "uppercase", TextCase.Lower => "lowercase", _ => "capitalize" }).Append(';');
        if (properties.LetterSpacing is { } spacing && spacing != 0) css.Append("letter-spacing:").Append(Points(spacing)).Append(';');
        if (properties.Hidden is true) css.Append("display:none;");
        return css.ToString();
    }

    private string FontName(string font) =>
        new([.. font.Where(character => !char.IsControl(character) && character is not ('\'' or '"' or '<' or '>' or ';' or '{' or '}' or '\\'))]);

    private string ClassName(string prefix, string name)
    {
        var key = prefix + "\u0000" + name;
        if (_classNames.TryGetValue(key, out var known)) return known;
        var candidate = prefix + "-" + new string(name.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
        var unique = candidate;
        for (var suffix = 2; _takenClassNames.Contains(unique); suffix++) unique = candidate + "-" + suffix.ToString(CultureInfo.InvariantCulture);
        _takenClassNames.Add(unique);
        _classNames[key] = unique;
        return unique;
    }

    private string BorderCss(BorderLine line) =>
        Points(line.Width) + " " + line.Style switch
        {
            BorderLineStyle.Dotted => "dotted",
            BorderLineStyle.Dashed => "dashed",
            BorderLineStyle.Double => "double",
            _ => "solid",
        } + " " + Color(line.Color);

    private string Points(double points) => Math.Round(points, 2).ToString(CultureInfo.InvariantCulture) + "pt";

    private string Color(uint argb) => new ArgbColor(argb).ToHashedHex();

    private string Escape(string text) => WebUtility.HtmlEncode(XmlSafe(text));

    private string Attribute(string text) => WebUtility.HtmlEncode(XmlSafe(text));

    private string XmlSafe(string text)
    {
        if (text.All(character => System.Xml.XmlConvert.IsXmlChar(character))) return text;
        var kept = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (System.Xml.XmlConvert.IsXmlChar(text[i])) kept.Append(text[i]);
            else if (i + 1 < text.Length && System.Xml.XmlConvert.IsXmlSurrogatePair(text[i + 1], text[i]))
            {
                kept.Append(text, i, 2);
                i++;
            }
        }
        return kept.ToString();
    }
}
