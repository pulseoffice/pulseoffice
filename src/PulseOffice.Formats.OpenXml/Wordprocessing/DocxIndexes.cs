using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Indexes;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxIndexScan(string? Opening, bool Closes, string? Whole, string? Data = null);

public sealed class DocxIndexCodec(IIndexStyles styles, DocxIndexSettings settings)
{
    private readonly IReadOnlyList<string> _tableSequences = ["Table", "Tabella", "Tabelle", "Tabla", "Tableau"];

    public string Instruction(IndexDefinition index) => Instruction(index, 1);

    public string Instruction(IndexDefinition index, int columns)
    {
        var builder = new StringBuilder();
        switch (index.Kind)
        {
            case IndexKind.Contents:
                builder.Append(CultureInfo.InvariantCulture, $" TOC \\o \"1-{Math.Clamp(index.Levels, 1, 9)}\"");
                if (index.FromMarks) builder.Append(" \\f");
                break;
            case IndexKind.Illustrations or IndexKind.Tables:
                builder.Append(" TOC \\c \"").Append(index.Sequence ?? (index.Kind == IndexKind.Tables ? "Table" : "Figure")).Append('"');
                if (index.Captions == CaptionDisplay.Text) builder.Append(" \\a \"").Append(index.Sequence ?? "Figure").Append('"');
                break;
            case IndexKind.Alphabetical:
                builder.Append(" INDEX");
                if (columns > 1) builder.Append(CultureInfo.InvariantCulture, $" \\c \"{Math.Min(columns, 4)}\"");
                if (index.LetterSeparators) builder.Append(" \\h \"A\"");
                return builder.Append(' ').ToString();
            case IndexKind.Bibliography:
                return " BIBLIOGRAPHY ";
            default:
                builder.Append(" TOC \\f \"").Append(index.UserName ?? "User-Defined").Append('"');
                break;
        }
        if (index.Hyperlinks) builder.Append(" \\h");
        if (!index.PageNumbers) builder.Append(" \\n");
        return builder.Append(" \\z \\u ").ToString();
    }

    public IndexDefinition? Parse(string instruction)
    {
        var text = instruction.Trim();
        var space = text.IndexOf(' ', StringComparison.Ordinal);
        var keyword = (space < 0 ? text : text[..space]).ToUpperInvariant();
        var switches = Switches(space < 0 ? string.Empty : text[space..]);
        switch (keyword)
        {
            case "BIBLIOGRAPHY":
                return new IndexDefinition(IndexKind.Bibliography) { PageNumbers = false, Hyperlinks = false };
            case "INDEX":
                return new IndexDefinition(IndexKind.Alphabetical) { LetterSeparators = switches.ContainsKey("h"), Hyperlinks = false };
            case "TOC":
                var common = (Hyperlinks: switches.ContainsKey("h"), Pages: !switches.ContainsKey("n"));
                if (switches.TryGetValue("c", out var sequence) && sequence.Length > 0)
                {
                    var kind = _tableSequences.Contains(sequence, StringComparer.OrdinalIgnoreCase) ? IndexKind.Tables : IndexKind.Illustrations;
                    return new IndexDefinition(kind)
                    {
                        Sequence = sequence,
                        Captions = switches.ContainsKey("a") ? CaptionDisplay.Text : CaptionDisplay.Caption,
                        Hyperlinks = common.Hyperlinks,
                        PageNumbers = common.Pages,
                    };
                }
                if (switches.TryGetValue("f", out var name) && name.Length > 0 && !switches.ContainsKey("o"))
                    return new IndexDefinition(IndexKind.User) { UserName = name, Hyperlinks = common.Hyperlinks, PageNumbers = common.Pages };
                var levels = switches.TryGetValue("o", out var range) && range.Split('-') is [_, var last] && int.TryParse(last, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 3;
                return new IndexDefinition(IndexKind.Contents)
                {
                    Levels = levels,
                    FromMarks = switches.ContainsKey("f"),
                    Hyperlinks = common.Hyperlinks,
                    PageNumbers = common.Pages,
                };
            default:
                return null;
        }
    }

    public IndexDefinition? Parse(string instruction, string? data) =>
        Parse(instruction) is { } parsed ? settings.Decode(data, parsed) : null;

    private string? Data(IndexDefinition index, int columns) =>
        Parse(Instruction(index, columns)) is { } plain && plain with { Title = index.Title } == index ? null : settings.Encode(index);

    public int Columns(string instruction)
    {
        var text = instruction.Trim();
        var space = text.IndexOf(' ', StringComparison.Ordinal);
        if (space < 0 || !text[..space].Equals("INDEX", StringComparison.OrdinalIgnoreCase)) return 1;
        return Switches(text[space..]).TryGetValue("c", out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? Math.Clamp(count, 1, 4)
            : 1;
    }

    private Dictionary<string, string> Switches(string rest)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < rest.Length)
        {
            if (rest[i] != '\\')
            {
                i++;
                continue;
            }
            if (i + 1 >= rest.Length) break;
            var name = rest[i + 1].ToString();
            i += 2;
            while (i < rest.Length && rest[i] == ' ') i++;
            var value = string.Empty;
            if (i < rest.Length && rest[i] == '"')
            {
                var end = rest.IndexOf('"', i + 1);
                if (end < 0) end = rest.Length;
                value = rest[(i + 1)..end];
                i = end + 1;
            }
            found[name] = value;
        }
        return found;
    }

    private readonly IReadOnlyList<string> _wordTitles = ["TOC Heading", "table of authorities", "Table of Authorities", "Index Heading", "index heading", "Contents Heading"];

    public bool IsTitle(IndexDefinition index, string? style) =>
        style is not null && (style == styles.Heading(index) || _wordTitles.Contains(style, StringComparer.Ordinal));

    public IEnumerable<OpenXmlElement> Write(SectionBlock section, IndexDefinition index, IReadOnlyList<OpenXmlElement> written)
    {
        var paragraphs = written.ToList();
        if (index.Kind == IndexKind.Objects) return paragraphs;
        var first = index.Title.Length > 0 && section.Content.Blocks.FirstOrDefault() is ParagraphBlock title && title.StyleName == styles.Heading(index) && paragraphs.FirstOrDefault() is W.Paragraph ? 1 : 0;
        if (first >= paragraphs.Count || paragraphs[first] is not W.Paragraph) paragraphs.Insert(first, new W.Paragraph());
        if (paragraphs[^1] is not W.Paragraph) paragraphs.Add(new W.Paragraph());
        var opening = (W.Paragraph)paragraphs[first];
        var after = opening.ParagraphProperties;
        var begin = new W.FieldChar { FieldCharType = W.FieldCharValues.Begin, Dirty = true };
        if (Data(index, section.Columns.Count) is { } data) begin.Append(new W.FieldData(data));
        var runs = new OpenXmlElement[]
        {
            new W.Run(begin),
            new W.Run(new W.FieldCode(Instruction(index, section.Columns.Count)) { Space = SpaceProcessingModeValues.Preserve }),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
        };
        var at = after is null ? 0 : 1;
        foreach (var run in runs) opening.InsertAt(run, at++);
        ((W.Paragraph)paragraphs[^1]).Append(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
        var gallery = index.Kind switch
        {
            IndexKind.Contents => "Table of Contents",
            IndexKind.Bibliography => "Bibliographies",
            _ => null,
        };
        if (gallery is null) return paragraphs;
        var sdt = new W.SdtBlock(
            new W.SdtProperties(new W.SdtContentDocPartObject(new W.DocPartGallery { Val = gallery }, new W.DocPartUnique())),
            new W.SdtContentBlock(paragraphs));
        return [sdt];
    }

    public DocxIndexScan Scan(W.Paragraph paragraph)
    {
        var depth = 0;
        string? outer = null;
        string? whole = null;
        string? data = null;
        string? opening = null;
        var instruction = new StringBuilder();
        var collecting = false;
        var closes = false;
        foreach (var element in paragraph.Descendants())
        {
            switch (element)
            {
                case W.FieldChar { FieldCharType.Value: var type }:
                    if (type == W.FieldCharValues.Begin)
                    {
                        depth++;
                        if (depth == 1)
                        {
                            opening = element.GetFirstChild<W.FieldData>()?.Text;
                            collecting = true;
                            outer = null;
                            instruction.Clear();
                        }
                    }
                    else if (type == W.FieldCharValues.Separate && depth == 1)
                    {
                        collecting = false;
                        if (Parse(instruction.ToString()) is not null)
                        {
                            outer = instruction.ToString();
                            data = opening;
                        }
                    }
                    else if (type == W.FieldCharValues.End)
                    {
                        if (depth == 0) closes = true;
                        else
                        {
                            if (depth == 1 && outer is not null) whole = outer;
                            depth--;
                            if (depth == 0) outer = null;
                        }
                    }
                    break;
                case W.SimpleField simple when depth == 0 && simple.Instruction?.Value is { } simpleInstruction && Parse(simpleInstruction) is not null:
                    if (whole is null) data = simple.GetFirstChild<W.FieldData>()?.Text;
                    whole ??= simpleInstruction;
                    break;
                case W.FieldCode code when collecting && depth == 1:
                    instruction.Append(code.Text);
                    break;
            }
        }
        return new DocxIndexScan(depth > 0 ? outer : null, closes, whole, data);
    }
}
