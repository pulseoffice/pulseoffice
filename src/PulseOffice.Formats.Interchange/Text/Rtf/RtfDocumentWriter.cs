using System.Globalization;
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
using CellVerticalAlign = PulseOffice.Documents.Blocks.CellVerticalAlign;

namespace PulseOffice.Formats.Interchange.Text.Rtf;

public sealed class RtfDocumentWriter(IParagraphSemantics semantics, IStyleResolver resolver, IDirectFormatting direct, IRtfVocabulary vocabulary) : ITextDocumentWriter
{
    public TextDocumentFormat Format => TextInterchangeFormats.Rtf;

    public FidelityReport CheckFidelity(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var losses = new List<Diagnostic>();
        if (document.Styles.Page.All.Any(style => style.Name != document.InitialPageStyle && (style.Header.Enabled || style.Footer.Enabled))
            || document.Styles.Page.Find(document.InitialPageStyle) is { } initial && (initial.FirstPageDifferent || initial.EvenPagesDifferent)
            || Blocks(document.Body).OfType<SectionBlock>().Any())
            losses.Add(Diagnostic.Warning("RTF001", "Only the header and footer of the first page style are written to RTF; other page styles, first and even page variants and sections are lost."));
        if (document.Objects.All.Any(item => item.Content is not (EmbeddedContent { Value: RasterImageObject { MediaType: "image/png" or "image/jpeg" } } or TextBoxContent)))
            losses.Add(Diagnostic.Warning("RTF002", "Only PNG and JPEG images and text boxes are written to RTF; shapes, charts, formulas, other images and objects are lost."));
        if (Blocks(document.Body).OfType<TableBlock>().Any(table => table.Rows.SelectMany(row => row.Cells).Any(cell => cell.Content.Blocks.Any(block => block is not ParagraphBlock))))
            losses.Add(Diagnostic.Info("RTF003", "Tables nested in table cells become paragraphs in RTF."));
        if (document.Comments.All.Count > 0 || document.Changes.All.Count > 0)
            losses.Add(Diagnostic.Warning("RTF004", "Comments and tracked changes are not written to RTF."));
        if (document.Macros.All.Any())
            losses.Add(Diagnostic.Warning("RTF005", "Macros are not written to RTF."));
        if (document.Notes.All.Any(note => note.Citation is { Length: > 0 }))
            losses.Add(Diagnostic.Info("RTF006", "Footnotes with a custom mark become automatically numbered in RTF."));
        return new FidelityReport(losses);
    }

    public OperationResult<bool> Write(TextDocument document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            var text = new RtfWriteSession(document, semantics, resolver, direct, vocabulary).Render();
            var bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes);
            return OperationResult<bool>.Ok(true);
        }
        catch (IOException exception)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("RTF100", $"Writing the RTF file failed: {exception.Message}").With(exception.Message));
        }
    }

    private IEnumerable<Block> Blocks(BlockContainer container) => container.Blocks.SelectMany(block => block switch
    {
        SectionBlock section => [block, .. Blocks(section.Content)],
        TableBlock table => [block, .. table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Blocks(cell.Content))],
        _ => (IEnumerable<Block>)[block],
    });
}

internal sealed class RtfWriteSession(TextDocument document, IParagraphSemantics semantics, IStyleResolver resolver, IDirectFormatting direct, IRtfVocabulary vocabulary)
{
    private readonly List<string> _fonts = ["Liberation Serif"];
    private readonly List<uint> _colors = [];
    private readonly RtfBorderLineStyles _lineStyles = new();
    private readonly Dictionary<string, int> _paragraphStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _characterStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Name, ListKind Kind, string? Thread), (int Id, ListPlacement Placement)> _lists = [];
    private readonly List<(int Number, int List, int Level, int? StartAt)> _overrides = [];
    private readonly Dictionary<int, int> _currentOverride = [];
    private readonly HashSet<string> _openNotes = new(StringComparer.Ordinal);

    public string Render()
    {
        var body = new StringBuilder();
        var standard = resolver.ResolveParagraphStyle(document.Styles, DefaultStyleNames.Standard).Character;
        if (standard.Font is { Length: > 0 } font) _fonts[0] = font;
        if (document.Styles.Page.Find(document.InitialPageStyle) is { } initial)
        {
            Region("header", initial.Header, body);
            Region("footer", initial.Footer, body);
        }
        Blocks(document.Body, body, false);

        var sheet = new StringBuilder("{\\stylesheet{\\s0 Normal;}");
        foreach (var (name, index) in _paragraphStyles.OrderBy(pair => pair.Value))
        {
            var resolved = resolver.ResolveParagraphStyle(document.Styles, name);
            sheet.Append(@"{\s").Append(index);
            var heading = semantics.HeadingLevel(document.Styles, new TextParagraph(string.Empty) { StyleName = name });
            if (heading > 0) sheet.Append(@"\outlinelevel").Append(heading - 1);
            sheet.Append(ParagraphControls(resolved.Paragraph)).Append(CharacterControls(resolved.Character, true));
            sheet.Append(' ').Append(TableEntry(vocabulary.ExportName(name))).Append(";}");
        }
        foreach (var (name, index) in _characterStyles.OrderBy(pair => pair.Value))
            sheet.Append(@"{\*\cs").Append(index).Append(@"\additive").Append(CharacterControls(resolver.ResolveCharacterStyle(document.Styles, name), true)).Append(' ').Append(TableEntry(name)).Append(";}");
        sheet.Append("}\n");
        var rtf = new StringBuilder();
        rtf.Append(@"{\rtf1\ansi\ansicpg1252\deff0\uc1").Append('\n');
        rtf.Append(@"{\fonttbl");
        for (var i = 0; i < _fonts.Count; i++) rtf.Append(@"{\f").Append(i).Append(@"\fnil\fcharset0 ").Append(TableEntry(_fonts[i])).Append(";}");
        rtf.Append("}\n{\\colortbl;");
        foreach (var color in _colors) rtf.Append(@"\red").Append((color >> 16) & 0xFF).Append(@"\green").Append((color >> 8) & 0xFF).Append(@"\blue").Append(color & 0xFF).Append(';');
        rtf.Append("}\n").Append(sheet);
        if (_lists.Count > 0)
        {
            rtf.Append(@"{\*\listtable");
            foreach (var (_, (id, placement)) in _lists)
            {
                rtf.Append(@"{\list\listtemplateid").Append(id).Append(@"\listsimple0");
                var style = placement.StyleName is { } styleName ? document.Styles.List.Find(styleName) : null;
                for (var level = 0; level < 9; level++)
                {
                    var format = placement.Kind == ListKind.Bullet ? NumberFormat.Bullet : style?.Level(level).Format ?? NumberFormat.Arabic;
                    if (placement.Kind == ListKind.Numbered && format is NumberFormat.Bullet or NumberFormat.None) format = NumberFormat.Arabic;
                    var code = vocabulary.ListCode(format);
                    var indent = 360 * (level + 1);
                    rtf.Append(@"{\listlevel\levelnfc").Append(code).Append(@"\levelnfcn").Append(code).Append(@"\leveljc0\leveljcn0\levelfollow0\levelstartat").Append(Math.Max(0, style?.Level(level).StartAt ?? 1)).Append(@"\levelspace0\levelindent0");
                    rtf.Append(format == NumberFormat.Bullet
                        ? @"{\leveltext\'01\u8226 ?;}{\levelnumbers;}"
                        : @"{\leveltext\'02\'0" + level.ToString(CultureInfo.InvariantCulture) + @".;}{\levelnumbers\'01;}");
                    rtf.Append(@"\fi-360\li").Append(indent).Append('}');
                }
                rtf.Append(@"{\listname ;}\listid").Append(id).Append('}');
            }
            rtf.Append("}\n{\\*\\listoverridetable");
            foreach (var (number, list, overridden, startAt) in _overrides)
            {
                rtf.Append(@"{\listoverride\listid").Append(list);
                if (startAt is { } value)
                {
                    rtf.Append(@"\listoverridecount9");
                    for (var level = 0; level < 9; level++)
                        rtf.Append(level == overridden ? @"{\lfolevel\listoverridestartat\levelstartat" + value.ToString(CultureInfo.InvariantCulture) + "}" : @"{\lfolevel}");
                }
                else rtf.Append(@"\listoverridecount0");
                rtf.Append(@"\ls").Append(number).Append('}');
            }
            rtf.Append("}\n");
        }

        rtf.Append(@"{\info");
        if (document.Properties.Title is { Length: > 0 } title) rtf.Append(@"{\title ").Append(Text(title)).Append('}');
        if (document.Properties.Author is { Length: > 0 } author) rtf.Append(@"{\author ").Append(Text(author)).Append('}');
        if (document.Properties.Subject is { Length: > 0 } subject) rtf.Append(@"{\subject ").Append(Text(subject)).Append('}');
        rtf.Append("}\n");
        if (document.Styles.Page.Find(document.InitialPageStyle) is { } page)
            rtf.Append(@"\paperw").Append(page.Size.Width.Value).Append(@"\paperh").Append(page.Size.Height.Value)
               .Append(@"\margl").Append(page.Margins.Left.Value).Append(@"\margr").Append(page.Margins.Right.Value)
               .Append(@"\margt").Append(page.Margins.Top.Value).Append(@"\margb").Append(page.Margins.Bottom.Value).Append('\n');
        rtf.Append(body).Append("}\n");
        return rtf.ToString();
    }

    private void Region(string kind, HeaderFooterContent region, StringBuilder rtf)
    {
        if (!region.Enabled) return;
        var paragraphs = region.Default.Blocks.OfType<ParagraphBlock>().ToList();
        rtf.Append("{\\").Append(kind);
        for (var i = 0; i < paragraphs.Count; i++) Paragraph(paragraphs[i], rtf, false, i == paragraphs.Count - 1 ? string.Empty : @"\par");
        rtf.Append("}\n");
    }

    private void Blocks(BlockContainer container, StringBuilder rtf, bool inTable)
    {
        foreach (var block in container.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    Paragraph(paragraph, rtf, inTable, @"\par");
                    break;
                case TableBlock table when !inTable:
                    Table(table, rtf);
                    break;
                case TableBlock table:
                    foreach (var cell in table.Rows.SelectMany(row => row.Cells).Where(cell => !cell.Covered)) Blocks(cell.Content, rtf, true);
                    break;
                case SectionBlock section:
                    Blocks(section.Content, rtf, inTable);
                    break;
            }
        }
    }

    private void Table(TableBlock table, StringBuilder rtf)
    {
        var origins = Origins(table);
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            rtf.Append(@"\trowd\trgaph108");
            if (r < table.HeaderRows) rtf.Append(@"\trhdr");
            var right = 0;
            for (var c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                if (origins[r][c] is { } found)
                {
                    var (originRow, originColumn) = found;
                    var source = table.Rows[originRow].Cells[originColumn];
                    if (source.RowSpan > 1) rtf.Append(originRow == r ? @"\clvmgf" : @"\clvmrg");
                    if (source.ColumnSpan > 1) rtf.Append(originColumn == c ? @"\clmgf" : @"\clmrg");
                    if (source.Background is { } background) rtf.Append(@"\clcbpat").Append(Color(background));
                    if (source.VerticalAlign == CellVerticalAlign.Middle) rtf.Append(@"\clvertalc");
                    if (source.VerticalAlign == CellVerticalAlign.Bottom) rtf.Append(@"\clvertalb");
                    if (source.Borders is { } box) CellBox(box, rtf);
                }
                right += c < table.Columns.Count ? table.Columns[c].Value : 1440;
                rtf.Append(@"\cellx").Append(right);
            }
            rtf.Append('\n');
            foreach (var cell in row.Cells)
            {
                if (cell.Covered)
                {
                    rtf.Append(@"\pard\plain\intbl\cell").Append('\n');
                    continue;
                }
                var blocks = cell.Content.Blocks.ToList();
                if (blocks.Count == 0) rtf.Append(@"\pard\plain\intbl\cell").Append('\n');
                for (var i = 0; i < blocks.Count; i++)
                {
                    var last = i == blocks.Count - 1;
                    if (blocks[i] is ParagraphBlock paragraph) Paragraph(paragraph, rtf, true, last ? @"\cell" : @"\par");
                    else
                    {
                        Blocks(new BlockContainer([blocks[i]]), rtf, true);
                        if (last) rtf.Append(@"\pard\plain\intbl\cell").Append('\n');
                    }
                }
            }
            rtf.Append(@"\row").Append('\n');
        }
        rtf.Append(@"\pard\plain").Append('\n');
    }

    private List<(int Row, int Column)?[]> Origins(TableBlock table)
    {
        var origins = table.Rows.Select(row => new (int Row, int Column)?[row.Cells.Count]).ToList();
        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Rows[r].Cells.Count; c++)
            {
                var cell = table.Rows[r].Cells[c];
                if (cell.Covered) continue;
                for (var row = r; row < Math.Min(table.Rows.Count, r + Math.Max(1, cell.RowSpan)); row++)
                    for (var column = c; column < Math.Min(origins[row].Length, c + Math.Max(1, cell.ColumnSpan)); column++)
                        origins[row][column] ??= (r, c);
            }
        return origins;
    }

    private void CellBox(BoxBorders box, StringBuilder rtf)
    {
        foreach (var (side, word) in (ReadOnlySpan<(BorderSide, string)>)[(BorderSide.Top, @"\clbrdrt"), (BorderSide.Left, @"\clbrdrl"), (BorderSide.Bottom, @"\clbrdrb"), (BorderSide.Right, @"\clbrdrr")])
        {
            if (box.Line(side) is not { } line || !double.IsFinite(line.Width) || line.Width <= 0) continue;
            rtf.Append(word).Append(_lineStyles.Word(line.Style));
            rtf.Append(@"\brdrw").Append(Math.Clamp((int)Math.Round(line.Width * 20), 1, 255)).Append(@"\brdrcf").Append(Color(line.Color));
        }
        rtf.Append(@"\clpadl").Append(PaddingTwips(box.PaddingTop)).Append(@"\clpadfl3")
            .Append(@"\clpadt").Append(PaddingTwips(box.PaddingLeft)).Append(@"\clpadft3")
            .Append(@"\clpadb").Append(PaddingTwips(box.PaddingBottom)).Append(@"\clpadfb3")
            .Append(@"\clpadr").Append(PaddingTwips(box.PaddingRight)).Append(@"\clpadfr3");
    }

    private void ParagraphBox(BoxBorders box, StringBuilder rtf)
    {
        foreach (var (side, word) in (ReadOnlySpan<(BorderSide, string)>)[(BorderSide.Top, @"\brdrt"), (BorderSide.Left, @"\brdrl"), (BorderSide.Bottom, @"\brdrb"), (BorderSide.Right, @"\brdrr")])
        {
            var padding = PaddingTwips(box.Padding(side));
            if (box.Line(side) is not { } line || !double.IsFinite(line.Width) || line.Width <= 0)
            {
                if (padding > 0) rtf.Append(word).Append(@"\brdrnone\brsp").Append(padding);
                continue;
            }
            rtf.Append(word).Append(_lineStyles.Word(line.Style))
                .Append(@"\brdrw").Append(Math.Clamp((int)Math.Round(line.Width * 20), 1, 255)).Append(@"\brdrcf").Append(Color(line.Color))
                .Append(@"\brsp").Append(padding);
        }
    }

    private int PaddingTwips(double points) => double.IsFinite(points) ? Math.Clamp((int)Math.Round(points * 20), 0, 2880) : 0;

    private void Paragraph(ParagraphBlock block, StringBuilder rtf, bool inTable, string terminator)
    {
        var content = block.Content;
        rtf.Append(@"\pard\plain");
        var styled = content.StyleName is { Length: > 0 } style && style != DefaultStyleNames.Standard && document.Styles.Paragraph.Find(style) is not null;
        if (styled) rtf.Append(@"\s").Append(ParagraphStyle(content.StyleName!));
        var heading = semantics.HeadingLevel(document.Styles, content);
        if (heading > 0) rtf.Append(@"\outlinelevel").Append(heading - 1);
        var list = semantics.List(document.Styles, content);
        if (list.Kind != ListKind.None)
        {
            var id = Override(List(list, content.ListId), list.Level, content.ListHeader ? null : content.ListRestart);
            rtf.Append(@"\ls").Append(id).Append(@"\ilvl").Append(list.Level).Append(@"\fi-360\li").Append(360 * (list.Level + 1));
        }
        if (inTable) rtf.Append(@"\intbl");
        rtf.Append(ParagraphControls(direct.Paragraph(content)));
        if (styled) rtf.Append(CharacterControls(Visible(resolver.ResolveParagraphStyle(document.Styles, content.StyleName).Character), true));
        rtf.Append('\n');
        foreach (var frame in document.Objects.All.Where(item => item.Content is TextBoxContent && item.Anchor is ParagraphAnchor or CharacterAnchor && item.Anchor.Accept(new AnchorParagraph()) == block.Id))
            Frame(frame, (TextBoxContent)frame.Content, rtf);
        Inline(block, rtf);
        rtf.Append(terminator).Append('\n');
    }

    private void Frame(AnchoredObject frame, TextBoxContent box, StringBuilder rtf)
    {
        var left = frame.Placement.X.Value;
        var top = frame.Placement.Y.Value;
        rtf.Append(@"{\shp{\*\shpinst\shpleft").Append(left).Append(@"\shptop").Append(top)
            .Append(@"\shpright").Append(left + frame.Size.Width.Value).Append(@"\shpbottom").Append(top + frame.Size.Height.Value)
            .Append(@"\shpbxcolumn\shpbypara\shpwr2\shpwrk0\shpz").Append(Math.Max(0, frame.ZOrder))
            .Append(@"{\sp{\sn shapeType}{\sv 202}}{\sp{\sn fFitShapeToText}{\sv ").Append(box.AutoGrowHeight ? 1 : 0).Append("}}{\\shptxt ");
        var paragraphs = box.Content.Blocks.OfType<ParagraphBlock>().ToList();
        for (var i = 0; i < paragraphs.Count; i++) Paragraph(paragraphs[i], rtf, false, i == paragraphs.Count - 1 ? string.Empty : @"\par");
        rtf.Append("}}}\n");
    }

    private int Override(int list, int level, int? restart)
    {
        if (restart is null && _currentOverride.TryGetValue(list, out var current)) return current;
        var number = _overrides.Count + 1;
        _overrides.Add((number, list, level, restart is { } value ? Math.Max(0, value) : null));
        _currentOverride[list] = number;
        return number;
    }

    private int List(ListPlacement placement, string? thread)
    {
        var key = (placement.StyleName ?? string.Empty, placement.Kind, thread);
        if (!_lists.TryGetValue(key, out var entry))
        {
            entry = (_lists.Count + 1, placement);
            _lists[key] = entry;
        }
        return entry.Id;
    }

    private int ParagraphStyle(string name)
    {
        if (!_paragraphStyles.TryGetValue(name, out var index))
        {
            index = _paragraphStyles.Count + 1;
            _paragraphStyles[name] = index;
        }
        return index;
    }

    private int CharacterStyle(string name)
    {
        if (!_characterStyles.TryGetValue(name, out var index))
        {
            index = 100 + _characterStyles.Count;
            _characterStyles[name] = index;
        }
        return index;
    }

    private void Inline(ParagraphBlock block, StringBuilder rtf)
    {
        var marks = block.Marks.OrderBy(mark => mark.Offset).ToList();
        var markIndex = 0;
        var offset = 0;
        foreach (var run in block.Content.Runs)
        {
            var start = 0;
            while (true)
            {
                while (markIndex < marks.Count && marks[markIndex].Offset <= offset + start)
                    Mark(marks[markIndex++], rtf);
                var next = markIndex < marks.Count ? Math.Min(run.Text.Length, marks[markIndex].Offset - offset) : run.Text.Length;
                if (next <= start) next = run.Text.Length;
                Run(run, run.Text[start..next], rtf);
                start = next;
                if (start >= run.Text.Length) break;
            }
            offset += run.Text.Length;
        }
        for (; markIndex < marks.Count; markIndex++) Mark(marks[markIndex], rtf);
    }

    private void Mark(InlineMark mark, StringBuilder rtf)
    {
        switch (mark)
        {
            case BookmarkMark bookmark:
                if (bookmark.Edge != MarkEdge.End) rtf.Append(@"{\*\bkmkstart ").Append(Text(bookmark.Name)).Append('}');
                if (bookmark.Edge != MarkEdge.Start) rtf.Append(@"{\*\bkmkend ").Append(Text(bookmark.Name)).Append('}');
                break;
            case NoteMark note when document.Notes.All.FirstOrDefault(item => item.Id == note.NoteId) is { } found && _openNotes.Add(found.Id):
                rtf.Append(@"{\super\chftn}{\footnote");
                if (found.Kind == NoteKind.Endnote) rtf.Append(@"\ftnalt");
                var blocks = found.Content.Blocks.OfType<ParagraphBlock>().ToList();
                for (var i = 0; i < blocks.Count; i++)
                {
                    var paragraph = new StringBuilder();
                    Paragraph(blocks[i], paragraph, false, i == blocks.Count - 1 ? string.Empty : @"\par");
                    if (i == 0)
                    {
                        var space = paragraph.ToString().IndexOf('\n');
                        paragraph.Insert(space + 1, @"{\super\chftn}");
                    }
                    rtf.Append(paragraph.ToString().TrimEnd('\n'));
                }
                rtf.Append('}');
                break;
            case ObjectMark objectMark when document.Objects.Find(objectMark.ObjectId) is { Content: EmbeddedContent { Value: RasterImageObject image } } item
                                            && image.MediaType is "image/png" or "image/jpeg":
                rtf.Append(@"{\pict").Append(image.MediaType == "image/png" ? @"\pngblip" : @"\jpegblip");
                if (image.PixelWidth > 0) rtf.Append(@"\picw").Append(image.PixelWidth).Append(@"\pich").Append(image.PixelHeight);
                rtf.Append(@"\picwgoal").Append(item.Size.Width.Value).Append(@"\pichgoal").Append(item.Size.Height.Value).Append('\n');
                var hex = Convert.ToHexString(image.Data.Span).ToLowerInvariant();
                for (var i = 0; i < hex.Length; i += 128) rtf.Append(hex, i, Math.Min(128, hex.Length - i)).Append('\n');
                rtf.Append('}');
                break;
        }
    }

    private void Run(TextRun run, string text, StringBuilder rtf)
    {
        if (text.Length == 0) return;
        var inner = new StringBuilder("{");
        if (run.StyleName is { Length: > 0 } styleName && styleName != DefaultStyleNames.InternetLink && document.Styles.Character.Find(styleName) is not null)
            inner.Append(@"\cs").Append(CharacterStyle(styleName)).Append(CharacterControls(Visible(resolver.ResolveCharacterStyle(document.Styles, styleName)), true));
        inner.Append(CharacterControls(direct.Run(run), true));
        if (inner.Length > 1) inner.Append(' ');
        inner.Append(Text(text)).Append('}');
        var content = inner.ToString();
        if (run.Field != TextFieldKind.None && vocabulary.FieldInstruction(run.Field, run.FieldArgument) is { } instruction)
            content = @"{\field{\*\fldinst " + Text(instruction) + @"}{\fldrslt " + content + "}}";
        if (run.Hyperlink is { Length: > 0 } link)
        {
            var quoted = link.Replace("\"", "%22", StringComparison.Ordinal);
            var target = quoted.StartsWith('#') ? @"HYPERLINK \l """ + Text(quoted[1..]) + "\"" : "HYPERLINK \"" + Text(quoted) + "\"";
            content = @"{\field{\*\fldinst " + target + @"}{\fldrslt " + content + "}}";
        }
        rtf.Append(content);
    }

    private string ParagraphControls(ParagraphProperties properties)
    {
        var rtf = new StringBuilder();
        rtf.Append(properties.Align switch
        {
            TextAlign.Center => @"\qc",
            TextAlign.Right => @"\qr",
            TextAlign.Justify => @"\qj",
            TextAlign.Left => @"\ql",
            _ => string.Empty,
        });
        if (properties.MarginLeft is { } left) rtf.Append(@"\li").Append(Twips(left));
        if (properties.MarginRight is { } right) rtf.Append(@"\ri").Append(Twips(right));
        if (properties.Indent is { } indent) rtf.Append(@"\fi").Append(Twips(indent));
        if (properties.SpaceBefore is { } before) rtf.Append(@"\sb").Append(Twips(before));
        if (properties.SpaceAfter is { } after) rtf.Append(@"\sa").Append(Twips(after));
        if (properties.KeepWithNext is { } keepNext) rtf.Append(keepNext ? @"\keepn" : @"\keepn0");
        if (properties.KeepTogether is { } keep) rtf.Append(keep ? @"\keep" : @"\keep0");
        if (properties.PageBreakBefore is true) rtf.Append(@"\pagebb");
        if (properties.Direction is { } direction) rtf.Append(direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft ? @"\rtlpar" : @"\ltrpar");
        if (properties.Borders is { } borders) ParagraphBox(borders, rtf);
        if (properties.Background is { } shading) rtf.Append(@"\cbpat").Append(Color(shading));
        return rtf.ToString();
    }

    private string CharacterControls(CharacterProperties properties, bool registerFont)
    {
        var rtf = new StringBuilder();
        void Toggle(bool? value, string word)
        {
            if (value is { } on) rtf.Append('\\').Append(word).Append(on ? string.Empty : "0");
        }

        Toggle(properties.Bold, "b");
        Toggle(properties.Italic, "i");
        if (properties.DoubleUnderline is true) rtf.Append(@"\uldb");
        else if (properties.Underline is { } underline) rtf.Append(underline ? @"\ul" : @"\ulnone");
        Toggle(properties.Strikethrough, "strike");
        Toggle(properties.SmallCaps, "scaps");
        Toggle(properties.Shadow, "shad");
        Toggle(properties.Outline, "outl");
        if (properties.Case is { } textCase && textCase is TextCase.None or TextCase.Upper) Toggle(textCase == TextCase.Upper, "caps");
        if (properties.Relief is FontRelief.Embossed) rtf.Append(@"\embo");
        if (properties.Relief is FontRelief.Engraved) rtf.Append(@"\impr");
        Toggle(properties.Hidden, "v");
        if (properties.LetterSpacing is { } spacing) rtf.Append(@"\expndtw").Append((int)Math.Round(spacing * 20));
        if (properties.ScaleWidth is { } scale) rtf.Append(@"\charscalex").Append((int)Math.Round(scale));
        if (properties.Kerning is { } kerning) rtf.Append(@"\kerning").Append(kerning ? 1 : 0);
        if (properties.RightToLeft is true) rtf.Append(@"\rtlch");
        if (properties.Baseline == TextBaseline.Superscript) rtf.Append(@"\super");
        if (properties.Baseline == TextBaseline.Subscript) rtf.Append(@"\sub");
        if (properties.FontSize is { } size) rtf.Append(@"\fs").Append((int)Math.Round(Math.Clamp(size, 1, 1638) * 2));
        if (properties.Font is { Length: > 0 } font)
        {
            var index = _fonts.IndexOf(font);
            if (index < 0 && registerFont)
            {
                _fonts.Add(font);
                index = _fonts.Count - 1;
            }
            if (index >= 0) rtf.Append(@"\f").Append(index);
        }
        if (properties.Color is { } color) rtf.Append(@"\cf").Append(Color(color));
        if (properties.Highlight is { } highlight) rtf.Append(@"\highlight").Append(Color(highlight));
        if (properties.Language is { Length: > 0 } language)
        {
            try
            {
                rtf.Append(@"\lang").Append(CultureInfo.GetCultureInfo(language).LCID);
            }
            catch (CultureNotFoundException)
            {
            }
        }
        return rtf.ToString();
    }

    private CharacterProperties Visible(CharacterProperties properties) => properties with
    {
        Bold = properties.Bold is true ? true : null,
        Italic = properties.Italic is true ? true : null,
        Underline = properties.Underline is true ? true : null,
        Strikethrough = properties.Strikethrough is true ? true : null,
        SmallCaps = properties.SmallCaps is true ? true : null,
        Shadow = properties.Shadow is true ? true : null,
        Outline = properties.Outline is true ? true : null,
        DoubleUnderline = properties.DoubleUnderline is true ? true : null,
        Font = null,
        Language = null,
    };

    private int Color(uint argb)
    {
        var rgb = argb & 0xFFFFFF;
        var index = _colors.IndexOf(rgb);
        if (index < 0)
        {
            _colors.Add(rgb);
            index = _colors.Count - 1;
        }
        return index + 1;
    }

    private int Twips(double points) => (int)Math.Round(points * 20);

    private string TableEntry(string name) => Text(name).Replace(";", @"\'3b", StringComparison.Ordinal);

    private string Text(string text)
    {
        var rtf = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            switch (character)
            {
                case '\\' or '{' or '}':
                    rtf.Append('\\').Append(character);
                    break;
                case '\t':
                    rtf.Append(@"\tab ");
                    break;
                case '\n':
                    rtf.Append(@"\line ");
                    break;
                case '\u00A0':
                    rtf.Append(@"\~");
                    break;
                case '\u00AD':
                    rtf.Append(@"\-");
                    break;
                case < ' ':
                    break;
                case > '~':
                    rtf.Append(@"\u").Append((short)character).Append('?');
                    break;
                default:
                    rtf.Append(character);
                    break;
            }
        }
        return rtf.ToString();
    }
}
