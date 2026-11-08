using CellVerticalAlign = PulseOffice.Documents.Blocks.CellVerticalAlign;
using System.Globalization;
using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Interchange.Text.Assembly;
using PulseOffice.Formats.Interchange.Text.Plain;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Html;

public sealed class HtmlDocumentReader(
    IHtmlTokenizer tokenizer,
    ICssDeclarations css,
    IHtmlCellBorders borders,
    ITextEncodingDetector detector,
    ICodePages codePages,
    IDirectFormatting direct,
    ITextDocumentFactory factory,
    ImageHeader header,
    PulseOffice.Documents.Editing.IListThreads threads) : ITextDocumentReader
{
    private const long MaximumBytes = 64L * 1024 * 1024;
    private const int DeclarationWindow = 4096;

    public TextDocumentFormat Format => TextInterchangeFormats.Html;

    public bool CanRead(Stream stream)
    {
        var buffer = new byte[1024];
        var read = stream.Read(buffer, 0, buffer.Length);
        var head = detector.Decode(buffer[..read]).Text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return head.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
               || head.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
               || (head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && head.Contains("<html", StringComparison.OrdinalIgnoreCase));
    }

    public OperationResult<TextDocument> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > MaximumBytes)
                    return OperationResult<TextDocument>.Fail(Diagnostic.Error("HTML212", "The HTML file is too large to open."));
                buffer.Write(chunk, 0, read);
            }

            var document = factory.Create();
            var session = new HtmlReadSession(new TextDocumentAssembler(document, direct), css, borders, header);
            foreach (var token in tokenizer.Tokenize(Decode(buffer.ToArray()))) session.Accept(token);
            session.Finish();
            threads.Settle(document);
            return OperationResult<TextDocument>.Ok(document, session.Diagnostics);
        }
        catch (IOException exception)
        {
            return OperationResult<TextDocument>.Fail(Diagnostic.Error("HTML210", $"Reading the HTML file failed: {exception.Message}").With(exception.Message));
        }
    }

    private string Decode(byte[] data)
    {
        var detected = detector.Decode(data);
        if (detected.HadByteOrderMark || detected.Encoding is UnicodeEncoding or UTF32Encoding) return detected.Text;
        var head = Encoding.ASCII.GetString(data, 0, Math.Min(data.Length, DeclarationWindow));
        var at = head.IndexOf("charset", StringComparison.OrdinalIgnoreCase);
        if (at < 0) return detected.Text;
        var index = at + "charset".Length;
        while (index < head.Length && head[index] is ' ' or '\t' or '=' or '"' or '\'') index++;
        var start = index;
        while (index < head.Length && (char.IsAsciiLetterOrDigit(head[index]) || head[index] is '-' or '_' or '.' or ':')) index++;
        var name = head[start..index];
        if (name.Length == 0 || name.StartsWith("utf", StringComparison.OrdinalIgnoreCase) || codePages.Named(name) is not { } declared) return detected.Text;
        return declared.GetString(data);
    }
}

internal sealed class HtmlReadSession(TextDocumentAssembler assembler, ICssDeclarations css, IHtmlCellBorders borders, ImageHeader header)
{
    private const int MaximumDepth = 256;
    private const int MaximumImageBytes = 32 * 1024 * 1024;
    private const int MaximumColumns = 1024;
    private const double DefaultVerticalPadding = 1.5;
    private const double DefaultHorizontalPadding = 2.5;
    private const double MaximumPadding = 144;

    private readonly HashSet<string> _blocks = new(StringComparer.Ordinal)
    {
        "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "li", "blockquote", "pre", "address", "section", "article", "header", "footer",
        "nav", "aside", "main", "figure", "figcaption", "dt", "dd", "dl", "center", "caption", "body", "listing", "plaintext",
    };

    private readonly HashSet<string> _void = new(StringComparer.Ordinal) { "br", "hr", "img", "meta", "link", "input", "col", "area", "base", "wbr", "source", "embed", "param", "track" };

    private readonly List<(string Tag, RunShape Shape)> _inline = [];
    private readonly List<(string Tag, ParagraphShape Shape)> _block = [];
    private const int MaximumListNumber = 1_000_000;

    private readonly List<OpenHtmlList> _lists = [];
    private readonly Dictionary<string, int> _threadCounts = new(StringComparer.Ordinal);
    private const int MaximumRememberedLists = 64;

    private readonly Dictionary<string, List<string>> _lastThreads = new(StringComparer.Ordinal);
    private int _listThreads;
    private int _listSegments;
    private int _ignoredLists;
    private int _ignoredTables;
    private readonly Dictionary<string, (string Id, BlockContainer Content)> _notes = new(StringComparer.Ordinal);
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly Stack<TableState> _tables = new();
    private readonly List<(string Tag, int Blocks, int Inlines)> _frames = [];
    private readonly List<int> _preformatted = [];
    private readonly List<string> _preserving = [];
    private bool _inHead;
    private bool _inTitle;
    private bool _spacePending;
    private string? _skipAnchor;
    private int _noteDivDepth;
    private int _divDepth;
    private int _tableDepth;
    private bool _externalImages;

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    private RunShape CurrentRun => _inline.Count > 0 ? _inline[^1].Shape : RunShape.Plain;

    private ParagraphShape CurrentParagraph => _block.Count > 0 ? _block[^1].Shape : ParagraphShape.Plain;

    public void Accept(HtmlToken token)
    {
        switch (token)
        {
            case HtmlText text:
                Text(text.Text);
                break;
            case HtmlStartTag start:
                Start(start);
                break;
            case HtmlEndTag end:
                End(end.Name);
                break;
        }
    }

    public void Finish()
    {
        assembler.Close(CurrentParagraph);
        if (assembler.TablesTruncated)
            _diagnostics.Add(Diagnostic.Warning("ODF258", "A table was larger than 1024 columns or one million cells: the rows and columns beyond the limit were dropped."));
        if (_externalImages)
            _diagnostics.Add(Diagnostic.Info("HTML211", "Images linked from outside the HTML file are not loaded; only embedded images are read."));
    }

    private void Text(string text)
    {
        if (_inTitle)
        {
            assembler.Document.Properties.Title = text.Trim();
            return;
        }
        if (_inHead || _skipAnchor is not null) return;
        if (_preformatted.Count > 0 || _preserving.Count > 0)
        {
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0) assembler.EndParagraph(CurrentParagraph);
                assembler.Write(lines[i], CurrentRun);
            }
            return;
        }

        var collapsed = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character) && character != '\u00A0')
            {
                if (!_spacePending && (assembler.HasPendingContent || collapsed.Length > 0)) collapsed.Append(' ');
                _spacePending = true;
                continue;
            }
            _spacePending = false;
            collapsed.Append(character);
        }
        assembler.Write(collapsed.ToString(), CurrentRun);
    }

    private void Start(HtmlStartTag tag)
    {
        var name = tag.Name;
        if (name == "head") _inHead = true;
        if (name == "title") _inTitle = true;
        if (name == "meta" && string.Equals(tag.Attribute("name"), "author", StringComparison.OrdinalIgnoreCase) && tag.Attribute("content") is { Length: > 0 } author)
            assembler.Document.Properties.Author = author;
        if (_inHead || _skipAnchor is not null || _inline.Count + _block.Count > MaximumDepth) return;

        if (name == "div" && _noteDivDepth == 0 && tag.Attribute("id") is { } noteId && _notes.TryGetValue(noteId, out var note))
        {
            Flush();
            assembler.Enter(note.Content);
            _noteDivDepth = 1;
            _block.Add((name, Paragraph(tag, new ParagraphShape { StyleName = noteId.Contains("endnote", StringComparison.OrdinalIgnoreCase) ? DefaultStyleNames.Endnote : DefaultStyleNames.Footnote })));
            return;
        }

        if (name == "div" && _noteDivDepth == 0 && tag.Attribute("title") is "header" or "footer"
            && assembler.Document.Styles.Page.Find(assembler.Document.InitialPageStyle) is { } page)
        {
            Flush();
            var region = tag.Attribute("title") == "header" ? page.Header : page.Footer;
            region.Enabled = true;
            region.Default.Replace([]);
            assembler.Enter(region.Default);
            _noteDivDepth = 1;
            _block.Add((name, Paragraph(tag, new ParagraphShape { StyleName = tag.Attribute("title") == "header" ? DefaultStyleNames.Header : DefaultStyleNames.Footer })));
            return;
        }

        if (name is "span" or "div" && Frame(tag)) return;

        if (name == "div" && _noteDivDepth > 0) _noteDivDepth++;
        if (name == "div") _divDepth++;

        switch (name)
        {
            case "br":
                if (_preformatted.Count == 0 && _preserving.Count == 0) assembler.TrimTrailingSpace();
                assembler.Write("\n", CurrentRun);
                _spacePending = true;
                return;
            case "col":
                if (_tables.TryPeek(out var columns))
                {
                    var width = css.Points(css.Parse(tag.Attribute("style")).GetValueOrDefault("width") ?? tag.Attribute("width"));
                    var count = Number(tag.Attribute("span"));
                    for (var i = 0; i < count && columns.Widths.Count < MaximumColumns; i++) columns.Widths.Add(width);
                }
                return;
            case "hr":
                Flush();
                return;
            case "img":
                Image(tag);
                return;
            case "table" when _tableDepth >= MaximumDepth:
                _ignoredTables++;
                return;
            case "ul" or "ol" when _lists.Count >= MaximumDepth:
                _ignoredLists++;
                return;
            case "table":
                Flush();
                _tableDepth++;
                _tables.Push(new TableState { Bordered = borders.TableBordered(tag) });
                assembler.BeginTable();
                return;
            case "tr":
                Flush();
                assembler.BeginRow();
                if (_tables.TryPeek(out var opened)) opened.RowIsHeader = true;
                return;
            case "td" or "th":
                Cell(tag);
                return;
            case "ul" or "ol":
                Flush();
                OpenList(name == "ol", Hint(tag.Attribute("type")), ListNumber(tag.Attribute("start")));
                return;
            case "a":
                Anchor(tag);
                return;
        }

        if (_blocks.Contains(name))
        {
            Flush();
            _spacePending = false;
            if (name is "pre" or "listing" or "plaintext" || Preserves(tag)) _preformatted.Add(_block.Count);
            _block.Add((name, Paragraph(tag, Base(name, tag))));
            _inline.Add((name, Character(tag, BaseRun(name, CurrentRun))));
            return;
        }

        if (!_void.Contains(name) && !tag.SelfClosing)
        {
            _inline.Add((name, Character(tag, BaseRun(name, CurrentRun))));
            if (Preserves(tag)) _preserving.Add(name);
        }
    }

    private bool Preserves(HtmlStartTag tag) =>
        css.Parse(tag.Attribute("style")).GetValueOrDefault("white-space") is "pre" or "pre-wrap" or "break-spaces";

    private void End(string name)
    {
        if (name == "head") _inHead = false;
        if (name == "title") _inTitle = false;
        if (_inHead) return;
        if (name == "a" && _skipAnchor is not null)
        {
            _skipAnchor = null;
            return;
        }

        if (name == "div" && _noteDivDepth > 0)
        {
            _noteDivDepth--;
            if (_noteDivDepth == 0)
            {
                assembler.Leave(CurrentParagraph);
                Pop(_block, "div");
                return;
            }
        }
        if (name is "span" or "div" && EndFrame(name)) return;
        if (name == "div" && _divDepth > 0) _divDepth--;

        switch (name)
        {
            case "td" or "th":
                if (_tableDepth == 0) return;
                assembler.EndCell(CurrentParagraph);
                Pop(_block, name);
                Pop(_inline, name);
                return;
            case "tr":
                if (_tableDepth == 0) return;
                if (_tables.TryPeek(out var row)) row.EndRow();
                return;
            case "table" when _ignoredTables > 0:
                _ignoredTables--;
                return;
            case "ul" or "ol" when _ignoredLists > 0:
                _ignoredLists--;
                return;
            case "table":
                if (_tableDepth == 0) return;
                _tableDepth--;
                Flush();
                var state = _tables.Count > 0 ? _tables.Pop() : new TableState();
                assembler.EndTable(state.Widths.Count > 0 && state.Widths.All(width => width is > 0) ? [.. state.Widths.Select(width => Twip.FromPoints(width!.Value))] : null, state.HeaderRows);
                return;
            case "ul" or "ol":
                Flush();
                CloseList();
                return;
        }

        if (_blocks.Contains(name))
        {
            if (_block.Any(entry => entry.Tag == name))
            {
                Flush();
                var closing = _block.FindLastIndex(entry => entry.Tag == name);
                _preformatted.RemoveAll(index => index >= closing);
                Pop(_block, name);
                Pop(_inline, name);
            }
            return;
        }

        Pop(_inline, name);
        var preserved = _preserving.LastIndexOf(name);
        if (preserved >= 0) _preserving.RemoveRange(preserved, _preserving.Count - preserved);
    }

    private void Flush()
    {
        if (_preformatted.Count == 0 && _preserving.Count == 0) assembler.TrimTrailingSpace();
        assembler.Flush(CurrentParagraph);
        _spacePending = false;
    }

    private void Pop<T>(List<(string Tag, T Shape)> stack, string name)
    {
        var index = stack.FindLastIndex(entry => entry.Tag == name);
        if (index >= 0) stack.RemoveRange(index, stack.Count - index);
    }

    private bool Frame(HtmlStartTag tag)
    {
        var style = css.Parse(tag.Attribute("style"));
        if (style.GetValueOrDefault("float") is not ("left" or "right") || css.Points(style.GetValueOrDefault("width")) is not { } width || width <= 0) return false;
        var height = css.Points(style.GetValueOrDefault("height"));
        var minimum = css.Points(style.GetValueOrDefault("min-height"));
        assembler.BeginTextBox(new DocumentSize(Twip.FromPoints(Math.Min(width, 2000)), Twip.FromPoints(Math.Clamp(height ?? minimum ?? 28, 1, 2000))), height is null);
        _frames.Add((tag.Name, _block.Count, _inline.Count));
        _block.Add((tag.Name, new ParagraphShape { StyleName = DefaultStyleNames.FrameContents }));
        return true;
    }

    private bool EndFrame(string name)
    {
        if (_frames.Count == 0 || _frames[^1].Tag != name) return false;
        var (_, blocks, inlines) = _frames[^1];
        _frames.RemoveAt(_frames.Count - 1);
        if (_preformatted.Count == 0 && _preserving.Count == 0) assembler.TrimTrailingSpace();
        assembler.Leave(CurrentParagraph);
        if (_block.Count > blocks) _block.RemoveRange(blocks, _block.Count - blocks);
        if (_inline.Count > inlines) _inline.RemoveRange(inlines, _inline.Count - inlines);
        _spacePending = false;
        return true;
    }

    private void Cell(HtmlStartTag tag)
    {
        if (_tableDepth == 0) return;
        Flush();
        var bordered = _tables.TryPeek(out var table) && table.Bordered;
        if (tag.Name == "td" && table is not null) table.RowIsHeader = false;
        var style = css.Parse(tag.Attribute("style"));
        assembler.BeginCell(new CellShape
        {
            Borders = CellBorders(style, bordered),
            ColumnSpan = Number(tag.Attribute("colspan")),
            RowSpan = Number(tag.Attribute("rowspan")),
            Background = css.Background(style.GetValueOrDefault("background-color") ?? style.GetValueOrDefault("background") ?? tag.Attribute("bgcolor")),
            VerticalAlign = (style.GetValueOrDefault("vertical-align") ?? tag.Attribute("valign"))?.ToLowerInvariant() switch
            {
                "middle" or "center" => CellVerticalAlign.Middle,
                "bottom" => CellVerticalAlign.Bottom,
                _ => CellVerticalAlign.Top,
            },
        });
        var heading = new ParagraphShape { StyleName = tag.Name == "th" ? DefaultStyleNames.TableHeading : DefaultStyleNames.TableContents };
        _block.Add((tag.Name, CellParagraph(style, tag, heading)));
        _inline.Add((tag.Name, CellRun(style, CurrentRun)));
    }

    private ParagraphShape CellParagraph(IReadOnlyDictionary<string, string> style, HtmlStartTag tag, ParagraphShape shape)
    {
        var align = (style.GetValueOrDefault("text-align") ?? tag.Attribute("align"))?.ToLowerInvariant() switch
        {
            "center" => TextAlign.Center,
            "right" or "end" => TextAlign.Right,
            "justify" => TextAlign.Justify,
            "left" or "start" => TextAlign.Left,
            _ => (TextAlign?)null,
        };
        return align is null ? shape : shape with { Properties = new ParagraphProperties { Align = align }.Over(shape.Properties) };
    }

    private RunShape CellRun(IReadOnlyDictionary<string, string> style, RunShape shape)
    {
        var weight = style.GetValueOrDefault("font-weight");
        var fontStyle = style.GetValueOrDefault("font-style");
        var decoration = style.GetValueOrDefault("text-decoration") ?? style.GetValueOrDefault("text-decoration-line") ?? string.Empty;
        return Add(shape, new CharacterProperties
        {
            Bold = weight is null ? null : weight is "bold" or "bolder" || int.TryParse(weight, out var numeric) && numeric >= 600,
            Italic = fontStyle is null ? null : fontStyle is "italic" or "oblique",
            Underline = decoration.Contains("underline", StringComparison.OrdinalIgnoreCase) ? true : null,
            Strikethrough = decoration.Contains("line-through", StringComparison.OrdinalIgnoreCase) ? true : null,
            Color = css.Color(style.GetValueOrDefault("color")),
            Font = css.FontFamily(style.GetValueOrDefault("font-family")),
            FontSize = css.FontSize(style.GetValueOrDefault("font-size"), shape.Character.FontSize ?? 12),
        });
    }

    private double? Spacing(string? value) => css.Points(value) is { } points ? Math.Max(0, points) : null;

    private int Number(string? value, int fallback = 1) => int.TryParse(value, out var number) ? Math.Clamp(number, fallback, 1000) : fallback;

    private BoxBorders? CellBorders(IReadOnlyDictionary<string, string> style, bool bordered)
    {
        var edges = borders.Read(style, bordered);
        var padding = Padding(style);
        if (edges is null && padding is null) return null;
        var (top, right, bottom, left) = padding ?? (DefaultVerticalPadding, DefaultHorizontalPadding, DefaultVerticalPadding, DefaultHorizontalPadding);
        return new BoxBorders
        {
            Top = Line(edges?.Top), Bottom = Line(edges?.Bottom), Left = Line(edges?.Left), Right = Line(edges?.Right),
            PaddingTop = top, PaddingBottom = bottom, PaddingLeft = left, PaddingRight = right,
        };
    }

    private (double Top, double Right, double Bottom, double Left)? Padding(IReadOnlyDictionary<string, string> style)
    {
        var shorthand = style.GetValueOrDefault("padding")?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Spacing(part)).ToArray() ?? [];
        if (shorthand.Any(part => part is null)) shorthand = [];
        (double?, double?, double?, double?) sides = shorthand.Length switch
        {
            1 => (shorthand[0], shorthand[0], shorthand[0], shorthand[0]),
            2 => (shorthand[0], shorthand[1], shorthand[0], shorthand[1]),
            3 => (shorthand[0], shorthand[1], shorthand[2], shorthand[1]),
            4 => (shorthand[0], shorthand[1], shorthand[2], shorthand[3]),
            _ => (null, null, null, null),
        };
        var top = Spacing(style.GetValueOrDefault("padding-top")) ?? sides.Item1;
        var right = Spacing(style.GetValueOrDefault("padding-right")) ?? sides.Item2;
        var bottom = Spacing(style.GetValueOrDefault("padding-bottom")) ?? sides.Item3;
        var left = Spacing(style.GetValueOrDefault("padding-left")) ?? sides.Item4;
        if (top is null && right is null && bottom is null && left is null) return null;
        return (Math.Min(top ?? DefaultVerticalPadding, MaximumPadding), Math.Min(right ?? DefaultHorizontalPadding, MaximumPadding),
            Math.Min(bottom ?? DefaultVerticalPadding, MaximumPadding), Math.Min(left ?? DefaultHorizontalPadding, MaximumPadding));
    }

    private static BorderLine? Line(HtmlBorderLine? line) => line is null ? null : new BorderLine(line.WidthPoints, line.Color) { Style = line.Style };

    private void Anchor(HtmlStartTag tag)
    {
        var href = tag.Attribute("href");
        var classes = tag.Attribute("class") ?? string.Empty;
        if (classes.Contains("sdfootnoteanc", StringComparison.OrdinalIgnoreCase) || classes.Contains("sdendnoteanc", StringComparison.OrdinalIgnoreCase))
        {
            var target = (href ?? string.Empty).TrimStart('#');
            if (target.EndsWith("sym", StringComparison.Ordinal)) target = target[..^3];
            var kind = classes.Contains("sdendnoteanc", StringComparison.OrdinalIgnoreCase) ? NoteKind.Endnote : NoteKind.Footnote;
            var reserved = assembler.ReserveNote(kind);
            _notes[target] = reserved;
            _skipAnchor = "a";
            return;
        }
        if (classes.Contains("sdfootnotesym", StringComparison.OrdinalIgnoreCase) || classes.Contains("sdendnotesym", StringComparison.OrdinalIgnoreCase))
        {
            _skipAnchor = "a";
            return;
        }

        var bookmark = tag.Attribute("id") ?? tag.Attribute("name");
        if (bookmark is { Length: > 0 } && href is null) assembler.Mark(offset => new BookmarkMark(offset, bookmark, MarkEdge.Point));
        var link = SafeLink(href);
        _inline.Add(("a", Character(tag, link is null ? CurrentRun : CurrentRun with { Hyperlink = link })));
    }

    private string? SafeLink(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;
        href = href.Trim();
        if (href.StartsWith('#')) return href;
        return Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" or "ftp" or "file" ? href : null;
    }

    private void Image(HtmlStartTag tag)
    {
        var source = tag.Attribute("src");
        if (source is null) return;
        if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            _externalImages = true;
            return;
        }

        var comma = source.IndexOf(',');
        if (comma < 0) return;
        var meta = source[5..comma];
        if (!meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return;
        var mediaType = meta[..^7].Trim().ToLowerInvariant();
        if (mediaType is not ("image/png" or "image/jpeg" or "image/gif" or "image/bmp" or "image/webp")) return;
        var payload = source[(comma + 1)..].Trim();
        if (payload.Length > MaximumImageBytes / 3 * 4 + 4) return;
        var bytes = new byte[payload.Length / 4 * 3 + 3];
        if (!Convert.TryFromBase64String(payload, bytes, out var length)) return;
        var data = bytes[..length];
        var pixels = header.Size(data);
        var style = css.Parse(tag.Attribute("style"));
        var width = css.Points(style.GetValueOrDefault("width") ?? tag.Attribute("width")) ?? (pixels?.Width ?? 96) * 0.75;
        var height = css.Points(style.GetValueOrDefault("height") ?? tag.Attribute("height")) ?? (pixels?.Height ?? 96) * 0.75;
        var size = new DocumentSize(Twip.FromPoints(Math.Clamp(width, 1, 2000)), Twip.FromPoints(Math.Clamp(height, 1, 2000)));
        assembler.Image(new RasterImageObject
        {
            Id = assembler.Document.NextId("img"),
            Data = data,
            MediaType = mediaType,
            PixelWidth = pixels?.Width ?? 0,
            PixelHeight = pixels?.Height ?? 0,
            Description = tag.Attribute("alt") is { Length: > 0 } alt ? alt : null,
            NaturalSize = size,
        }, size);
    }

    private ParagraphShape Base(string name, HtmlStartTag tag)
    {
        if (name.Length == 2 && name[0] == 'h' && char.IsAsciiDigit(name[1]))
            return new ParagraphShape { StyleName = DefaultStyleNames.HeadingLevel(name[1] - '0') };
        return name switch
        {
            "li" when _lists.Count > 0 => List(tag),
            "li" => new ParagraphShape { ListStyleName = DefaultStyleNames.List1, Bullet = true },
            "pre" or "listing" or "plaintext" => new ParagraphShape { StyleName = DefaultStyleNames.PreformattedText },
            "blockquote" => new ParagraphShape { StyleName = DefaultStyleNames.Quotations },
            "caption" or "figcaption" => new ParagraphShape { StyleName = DefaultStyleNames.Caption },
            "center" => new ParagraphShape { Properties = new ParagraphProperties { Align = TextAlign.Center } },
            _ => CurrentParagraph with { },
        };
    }

    private ParagraphShape List(HtmlStartTag tag)
    {
        var list = _lists[^1];
        var level = Math.Min(_lists.Count - 1, ListStyle.LevelCount - 1);
        string? segment = null;
        int? start = null;
        if (list.Numbered && ListNumber(tag.Attribute("value")) is { } value)
        {
            segment = "li" + (++_listSegments).ToString(System.Globalization.CultureInfo.InvariantCulture);
            start = value;
        }
        else if (list.Numbered && list.Start is { } opening && !list.Started)
        {
            segment = list.Segment;
            start = opening;
        }
        _lists[^1] = list with { Started = true };
        if (list.Numbered && level == 0)
            _threadCounts[list.Thread] = start ?? (_threadCounts.TryGetValue(list.Thread, out var count) ? count + 1 : 1);
        return new ParagraphShape
        {
            ListStyleName = list.Style,
            Bullet = !list.Numbered,
            Numbered = list.Numbered,
            Level = level,
            ListId = list.Thread,
            ListSegment = segment,
            ListSegmentStart = start,
        };
    }

    private void OpenList(bool numbered, NumberFormatHint hint, int? start)
    {
        var style = !numbered ? DefaultStyleNames.List1 : hint switch
        {
            NumberFormatHint.Letters => DefaultStyleNames.NumberingAbc,
            NumberFormatHint.Roman => DefaultStyleNames.NumberingIvx,
            _ => DefaultStyleNames.Numbering123,
        };
        string thread;
        if (_lists.Count > 0)
        {
            thread = _lists[0].Thread;
            if (start == 1) start = null;
        }
        else if (Continued(style, numbered, start) is { } continued)
        {
            thread = continued;
            start = null;
        }
        else thread = "html" + (++_listThreads).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var segment = "ol" + (++_listSegments).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _lists.Add(new OpenHtmlList(numbered, style, thread, start, segment, false));
    }

    private void CloseList()
    {
        if (_lists.Count == 0) return;
        var list = _lists[^1];
        _lists.RemoveAt(_lists.Count - 1);
        if (_lists.Count > 0) return;
        if (!_lastThreads.TryGetValue(list.Style, out var recent))
        {
            recent = [];
            _lastThreads[list.Style] = recent;
        }
        recent.Remove(list.Thread);
        recent.Add(list.Thread);
        if (recent.Count > MaximumRememberedLists) recent.RemoveAt(0);
    }

    private string? Continued(string style, bool numbered, int? start)
    {
        if (!_lastThreads.TryGetValue(style, out var recent) || recent.Count == 0) return null;
        if (!numbered) return recent[^1];
        if (start is not { } opening) return null;
        for (var index = recent.Count - 1; index >= 0; index--)
            if ((_threadCounts.TryGetValue(recent[index], out var count) ? count : 0) == opening - 1) return recent[index];
        return null;
    }

    private int? ListNumber(string? text) =>
        int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number) ? Math.Clamp(number, 0, MaximumListNumber) : null;

    private sealed record OpenHtmlList(bool Numbered, string Style, string Thread, int? Start, string Segment, bool Started);

    private NumberFormatHint Hint(string? type) => type switch
    {
        "a" or "A" => NumberFormatHint.Letters,
        "i" or "I" => NumberFormatHint.Roman,
        _ => NumberFormatHint.Arabic,
    };

    private ParagraphShape Paragraph(HtmlStartTag tag, ParagraphShape shape)
    {
        var style = css.Parse(tag.Attribute("style"));
        var direction = Direction(tag.Attribute("dir") ?? style.GetValueOrDefault("direction"));
        var rightToLeft = (direction ?? shape.Properties.Direction) == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft;
        var align = (style.GetValueOrDefault("text-align") ?? tag.Attribute("align"))?.ToLowerInvariant() switch
        {
            "center" => TextAlign.Center,
            "end" => TextAlign.Right,
            "right" => rightToLeft ? TextAlign.Left : TextAlign.Right,
            "justify" => TextAlign.Justify,
            "start" => TextAlign.Left,
            "left" => rightToLeft ? TextAlign.Right : TextAlign.Left,
            _ => (TextAlign?)null,
        };
        var properties = new ParagraphProperties
        {
            Align = align,
            Direction = direction,
            MarginLeft = Spacing(style.GetValueOrDefault("margin-left")),
            MarginRight = Spacing(style.GetValueOrDefault("margin-right")),
            Indent = css.Points(style.GetValueOrDefault("text-indent")),
            SpaceBefore = Spacing(style.GetValueOrDefault("margin-top")),
            SpaceAfter = Spacing(style.GetValueOrDefault("margin-bottom")),
            PageBreakBefore = style.GetValueOrDefault("page-break-before") is "always" || style.GetValueOrDefault("break-before") is "page" ? true : null,
            KeepWithNext = style.GetValueOrDefault("page-break-after") is "avoid" || style.GetValueOrDefault("break-after") is "avoid" ? true : null,
            KeepTogether = style.GetValueOrDefault("page-break-inside") is "avoid" || style.GetValueOrDefault("break-inside") is "avoid" ? true : null,
            Background = css.Background(style.GetValueOrDefault("background-color") ?? style.GetValueOrDefault("background")),
        };
        var named = tag.Attribute("data-style") is { Length: > 0 } styleName ? shape with { StyleName = styleName } : shape;
        if (tag.Name == "li" && tag.Attribute("data-list-header") is "true") named = named with { ListHeader = true };
        return named with { Properties = properties.Over(shape.Properties) };
    }

    private PulseOffice.Text.Rich.Bidi.TextDirection? Direction(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "rtl" => PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft,
        "ltr" => PulseOffice.Text.Rich.Bidi.TextDirection.LeftToRight,
        _ => null,
    };

    private RunShape BaseRun(string name, RunShape current) => name switch
    {
        "b" or "strong" => Add(current, new CharacterProperties { Bold = true }),
        "i" or "em" or "cite" or "dfn" or "var" => Add(current, new CharacterProperties { Italic = true }),
        "u" or "ins" => Add(current, new CharacterProperties { Underline = true }),
        "s" or "strike" or "del" => Add(current, new CharacterProperties { Strikethrough = true }),
        "sup" => Add(current, new CharacterProperties { Baseline = TextBaseline.Superscript }),
        "sub" => Add(current, new CharacterProperties { Baseline = TextBaseline.Subscript }),
        "code" or "tt" or "kbd" or "samp" or "pre" => Add(current, new CharacterProperties { Font = "Liberation Mono" }),
        "mark" => Add(current, new CharacterProperties { Highlight = 0xFFFFFF00 }),
        "small" => Add(current, new CharacterProperties { FontSize = 10 }),
        "big" => Add(current, new CharacterProperties { FontSize = 14 }),
        "th" => Add(current, new CharacterProperties()),
        _ => current,
    };

    private RunShape Character(HtmlStartTag tag, RunShape shape)
    {
        var style = css.Parse(tag.Attribute("style"));
        var weight = style.GetValueOrDefault("font-weight");
        var decoration = style.GetValueOrDefault("text-decoration") ?? style.GetValueOrDefault("text-decoration-line") ?? string.Empty;
        var family = style.GetValueOrDefault("font-family") ?? tag.Attribute("face");
        var vertical = style.GetValueOrDefault("vertical-align");
        var fontStyle = style.GetValueOrDefault("font-style");
        var size = css.FontSize(style.GetValueOrDefault("font-size"), shape.Character.FontSize ?? 12) ?? FontTagSize(tag);
        var properties = new CharacterProperties
        {
            Bold = weight is null ? null : weight is "bold" or "bolder" || int.TryParse(weight, out var numeric) && numeric >= 600,
            Italic = fontStyle is null ? null : fontStyle is "italic" or "oblique",
            Underline = decoration.Contains("underline", StringComparison.OrdinalIgnoreCase) ? true : null,
            Strikethrough = decoration.Contains("line-through", StringComparison.OrdinalIgnoreCase) ? true : null,
            Overline = decoration.Contains("overline", StringComparison.OrdinalIgnoreCase) ? true : null,
            Color = css.Color(style.GetValueOrDefault("color") ?? tag.Attribute("color")),
            Highlight = css.Background(style.GetValueOrDefault("background-color") ?? style.GetValueOrDefault("background")),
            Font = css.FontFamily(family),
            FontSize = size,
            Baseline = vertical switch
            {
                "super" => TextBaseline.Superscript,
                "sub" => TextBaseline.Subscript,
                _ => null,
            },
            SmallCaps = style.GetValueOrDefault("font-variant") is "small-caps" ? true : null,
            Case = style.GetValueOrDefault("text-transform") switch
            {
                "uppercase" => TextCase.Upper,
                "lowercase" => TextCase.Lower,
                "capitalize" => TextCase.Capitalize,
                _ => null,
            },
            Language = tag.Attribute("lang") is { Length: > 0 } language ? language : null,
            RightToLeft = tag.Name is "span" or "bdo" && Direction(tag.Attribute("dir")) == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft ? true : null,
        };
        var styled = tag.Name == "span" && tag.Attribute("data-style") is { Length: > 0 } styleName ? shape with { StyleName = styleName } : shape;
        var field = tag.Attribute("data-field") is { Length: > 0 } fieldName && char.IsAsciiLetter(fieldName[0])
            && Enum.TryParse<TextFieldKind>(fieldName, out var kind) && Enum.IsDefined(kind) ? styled with { Field = kind }
            : tag.Name == "sdfield" && Field(tag) is var known && known != TextFieldKind.None ? styled with { Field = known }
            : styled;
        return Add(field, properties);
    }

    private TextFieldKind Field(HtmlStartTag tag) => (tag.Attribute("type")?.ToUpperInvariant(), tag.Attribute("subtype")?.ToUpperInvariant()) switch
    {
        ("PAGE", _) => TextFieldKind.PageNumber,
        ("DOCSTAT", "PAGE") => TextFieldKind.PageCount,
        ("DOCSTAT", "WORD") => TextFieldKind.WordCount,
        ("DOCSTAT", "CHAR") => TextFieldKind.CharacterCount,
        ("DOCSTAT", "PARAGRAPH" or "PARA") => TextFieldKind.ParagraphCount,
        ("DATETIME", _) when (tag.Attribute("format") ?? string.Empty).StartsWith("TIME", StringComparison.OrdinalIgnoreCase) => TextFieldKind.Time,
        ("DATETIME", _) => TextFieldKind.Date,
        ("DATE", _) => TextFieldKind.Date,
        ("TIME", _) => TextFieldKind.Time,
        ("AUTHOR", _) => TextFieldKind.Author,
        ("FILENAME", _) => TextFieldKind.FileName,
        ("DOCINFO", "TITLE") => TextFieldKind.Title,
        ("DOCINFO", "THEME") => TextFieldKind.Subject,
        ("DOCINFO", "KEYS") => TextFieldKind.Keywords,
        ("DOCINFO", "COMMENT") => TextFieldKind.Description,
        ("DOCINFO", "CREATE") => TextFieldKind.CreatedDate,
        ("DOCINFO", "CHANGE") => TextFieldKind.ModifiedDate,
        _ => TextFieldKind.None,
    };

    private double? FontTagSize(HtmlStartTag tag) =>
        tag.Name == "font" && tag.Attribute("size")?.Trim() is { Length: > 0 } declared
        && int.TryParse(declared, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var size)
            ? Math.Clamp(declared[0] is '+' or '-' ? 3 + size : size, 1, 7) switch { 1 => 7.5, 2 => 10, 3 => 12, 4 => 13.5, 5 => 18, 6 => 24, _ => 36 }
            : null;

    private RunShape Add(RunShape shape, CharacterProperties properties) =>
        shape with { Character = properties.Over(shape.Character) };
}

internal sealed class TableState
{
    private bool _counting = true;

    public bool RowIsHeader { get; set; }

    public bool Bordered { get; init; }

    public List<double?> Widths { get; } = [];

    public int HeaderRows { get; private set; }

    public void EndRow()
    {
        if (_counting && RowIsHeader) HeaderRows++;
        else _counting = false;
        RowIsHeader = false;
    }
}

internal enum NumberFormatHint
{
    Arabic,
    Letters,
    Roman,
}

public sealed class XhtmlDocumentReader(HtmlDocumentReader html) : ITextDocumentReader
{
    public TextDocumentFormat Format => TextInterchangeFormats.Xhtml;

    public bool CanRead(Stream stream) => html.CanRead(stream);

    public OperationResult<TextDocument> Read(Stream stream) => html.Read(stream);
}
