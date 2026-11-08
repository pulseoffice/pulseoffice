using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxBlockCarriers
{
    public ParagraphBlock? First(BlockContainer container) => container.Blocks.FirstOrDefault() switch
    {
        ParagraphBlock paragraph => paragraph,
        SectionBlock { Index: null } section => First(section.Content),
        _ => null,
    };

    public ParagraphBlock? Last(BlockContainer container) => container.Blocks.LastOrDefault() switch
    {
        ParagraphBlock paragraph => paragraph,
        SectionBlock { Index: null } section => Last(section.Content),
        _ => null,
    };

    public bool Carries(SectionBlock section) => First(section.Content) is not null && Last(section.Content) is not null;

    public bool Carries(TableCell cell) => cell.Content.Blocks.FirstOrDefault() is ParagraphBlock;
}

public sealed class DocxBlockProperties(DocxCustomXmlParts parts, DocxListHeaderIndent listHeaders, DocxBlockCarriers carriers)
{
    public const string Namespace = "urn:pulseoffice:block-properties";

    public const string BookmarkPrefix = "_PulseBlock";

    private const int LargestCount = 65536;

    private const int LargestKeyBytes = 4096;

    private readonly XNamespace _bp = Namespace;

    private sealed class Writing(DocxWriteContext context)
    {
        public DocxWriteContext Context { get; } = context;

        public List<XElement> Entries { get; } = [];

        public string Next() => BookmarkPrefix + (Entries.Count + 1).ToString(CultureInfo.InvariantCulture);
    }

    public void Write(MainDocumentPart main, DocxWriteContext context)
    {
        var writing = new Writing(context);
        Visit(context.Document.Body, writing);
        if (writing.Entries.Count == 0) return;
        parts.Add(main, new XElement(_bp + "blocks", new XAttribute(XNamespace.Xmlns + "bp", _bp), writing.Entries));
    }

    public void Read(MainDocumentPart main, DocxReadContext context)
    {
        var document = context.Document;
        var count = 0;
        var carried = new HashSet<SectionBlock>(ReferenceEqualityComparer.Instance);
        foreach (var root in parts.Roots(main, _bp + "blocks"))
            foreach (var entry in root.Elements())
            {
                if (++count > LargestCount) return;
                if (entry.Attribute("bookmark")?.Value is not { Length: > 0 } bookmark || !context.CarrierStarts.TryGetValue(bookmark, out var start)) continue;
                var end = context.CarrierEnds.GetValueOrDefault(bookmark, start);
                if (entry.Name == _bp + "listHeader") ReadListHeader(entry, Find(document.Body, start));
                else if (entry.Name == _bp + "cell") ReadCell(entry, document.Body, start);
                else if (entry.Name == _bp + "section") ReadSection(entry, document, start, end, carried);
            }
    }

    private void Visit(BlockContainer container, Writing writing)
    {
        foreach (var block in container.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph when listHeaders.IsHeader(paragraph.Content, writing.Context.Document.Styles):
                    WriteListHeader(paragraph, writing);
                    break;
                case TableBlock table:
                    foreach (var cell in table.Rows.SelectMany(row => row.Cells))
                    {
                        if (cell.NumberFormat is { } format && carriers.Carries(cell)) WriteCell((ParagraphBlock)cell.Content.Blocks[0], format, writing);
                        Visit(cell.Content, writing);
                    }
                    break;
                case SectionBlock { Index: null } section:
                    WriteSection(section, writing);
                    Visit(section.Content, writing);
                    break;
            }
        }
    }

    private void WriteListHeader(ParagraphBlock paragraph, Writing writing)
    {
        var content = paragraph.Content;
        var entry = new XElement(_bp + "listHeader", new XAttribute("level", content.BulletLevel.ToString(CultureInfo.InvariantCulture)));
        if (content.ListStyleName is { } list) entry.Add(new XAttribute("list", list));
        if (content.MarginLeft is { } margin) entry.Add(new XAttribute("margin", margin.ToString("R", CultureInfo.InvariantCulture)));
        Mark(paragraph, paragraph, entry, writing);
    }

    private void WriteCell(ParagraphBlock first, CellNumberFormat format, Writing writing)
    {
        var entry = new XElement(_bp + "cell",
            new XAttribute("kind", format.Kind.ToString()),
            new XAttribute("decimals", format.Decimals.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("grouping", format.Grouping ? "true" : "false"));
        if (format.Symbol is { } symbol) entry.Add(new XAttribute("symbol", symbol));
        if (format.LongDate) entry.Add(new XAttribute("long-date", "true"));
        Mark(first, first, entry, writing);
    }

    private void WriteSection(SectionBlock section, Writing writing)
    {
        if (carriers.First(section.Content) is not { } first || carriers.Last(section.Content) is not { } last) return;
        var entry = new XElement(_bp + "section", new XAttribute("name", section.Name));
        if (section.Protected) entry.Add(new XAttribute("protected", "true"));
        if (section.ProtectionKey is { Length: > 0 } key) entry.Add(new XAttribute("key", Convert.ToBase64String(key)));
        if (section.Hidden) entry.Add(new XAttribute("hidden", "true"));
        if (section.Condition is { } condition) entry.Add(new XAttribute("condition", condition));
        if (section.LinkedFile is { } link) entry.Add(new XAttribute("link", link));
        Mark(first, last, entry, writing);
    }

    private void Mark(ParagraphBlock first, ParagraphBlock last, XElement entry, Writing writing)
    {
        if (writing.Entries.Count >= LargestCount) return;
        if (!writing.Context.WrittenParagraphs.TryGetValue(first.Id, out var opening) || !writing.Context.WrittenParagraphs.TryGetValue(last.Id, out var closing)) return;
        var name = writing.Next();
        var id = writing.Context.Bookmarks[name].ToString(CultureInfo.InvariantCulture);
        var start = new W.BookmarkStart { Id = id, Name = name };
        if (opening.ParagraphProperties is { } properties) properties.InsertAfterSelf(start);
        else opening.PrependChild(start);
        closing.AppendChild(new W.BookmarkEnd { Id = id });
        entry.SetAttributeValue("bookmark", name);
        writing.Entries.Add(entry);
    }

    private void ReadListHeader(XElement entry, ParagraphBlock? paragraph)
    {
        if (paragraph is null) return;
        paragraph.Content = paragraph.Content with
        {
            ListHeader = true,
            ListStyleName = entry.Attribute("list")?.Value is { Length: > 0 } list ? list : null,
            BulletLevel = int.TryParse(entry.Attribute("level")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ? Math.Clamp(level, 0, 9) : 0,
            MarginLeft = double.TryParse(entry.Attribute("margin")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var margin) && double.IsFinite(margin) ? margin : null,
        };
    }

    private void ReadCell(XElement entry, BlockContainer body, string paragraphId)
    {
        if (Cell(body, paragraphId) is not { } cell || !Enum.TryParse<CellValueKind>(entry.Attribute("kind")?.Value, out var kind) || !Enum.IsDefined(kind)) return;
        var decimals = int.TryParse(entry.Attribute("decimals")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? Math.Clamp(parsed, 0, 20) : 2;
        cell.NumberFormat = new CellNumberFormat(kind, decimals, entry.Attribute("grouping")?.Value != "false")
        {
            Symbol = entry.Attribute("symbol")?.Value,
            LongDate = entry.Attribute("long-date")?.Value == "true",
        };
    }

    private void ReadSection(XElement entry, TextDocument document, string start, string end, HashSet<SectionBlock> carried)
    {
        if (Range(document.Body, start, end) is not { } range) return;
        var (container, first, last) = range;
        SectionBlock section;
        if (container.Owner is { } owner && !carried.Contains(owner) && first == 0 && last == container.Content.Count - 1) section = owner;
        else
        {
            var content = container.Content.Blocks.Skip(first).Take(last - first + 1).ToList();
            container.Content.RemoveRange(first, content.Count);
            section = new SectionBlock(document.NextId("s"), string.Empty, new BlockContainer(content));
            container.Content.Insert(first, section);
        }
        carried.Add(section);
        if (entry.Attribute("name")?.Value is { Length: > 0 } name) section.Name = name;
        section.Protected = entry.Attribute("protected")?.Value == "true";
        section.ProtectionKey = Key(entry.Attribute("key")?.Value);
        section.Hidden = entry.Attribute("hidden")?.Value == "true";
        section.Condition = entry.Attribute("condition")?.Value;
        section.LinkedFile = entry.Attribute("link")?.Value;
    }

    private byte[]? Key(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > LargestKeyBytes) return null;
        var bytes = new byte[text.Length];
        return Convert.TryFromBase64String(text, bytes, out var written) && written > 0 ? bytes[..written] : null;
    }

    private sealed record Holder(BlockContainer Content, SectionBlock? Owner);

    private (Holder Container, int First, int Last)? Range(BlockContainer root, string start, string end)
    {
        var pending = new Stack<Holder>();
        pending.Push(new Holder(root, null));
        while (pending.Count > 0)
        {
            var holder = pending.Pop();
            var first = Position(holder.Content, start);
            var last = Position(holder.Content, end);
            if (first >= 0 && last >= first)
            {
                if (first == last && holder.Content[first] is SectionBlock inner && Position(inner.Content, start) >= 0 && Position(inner.Content, end) >= 0)
                {
                    pending.Push(new Holder(inner.Content, inner));
                    continue;
                }
                return (holder, first, last);
            }
            if (first >= 0 && holder.Content[first] is SectionBlock nested) pending.Push(new Holder(nested.Content, nested));
        }
        return null;
    }

    private int Position(BlockContainer container, string paragraphId)
    {
        for (var index = 0; index < container.Count; index++)
            if (Holds(container[index], paragraphId)) return index;
        return -1;
    }

    private bool Holds(Block block, string paragraphId) => block switch
    {
        ParagraphBlock paragraph => paragraph.Id == paragraphId,
        SectionBlock section => section.Content.Blocks.Any(child => Holds(child, paragraphId)),
        _ => false,
    };

    private ParagraphBlock? Find(BlockContainer container, string paragraphId)
    {
        foreach (var block in container.Blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph when paragraph.Id == paragraphId:
                    return paragraph;
                case SectionBlock section when Find(section.Content, paragraphId) is { } inSection:
                    return inSection;
                case TableBlock table:
                    foreach (var cell in table.Rows.SelectMany(row => row.Cells))
                        if (Find(cell.Content, paragraphId) is { } inCell) return inCell;
                    break;
            }
        }
        return null;
    }

    private TableCell? Cell(BlockContainer container, string paragraphId)
    {
        foreach (var block in container.Blocks)
        {
            switch (block)
            {
                case SectionBlock section when Cell(section.Content, paragraphId) is { } inSection:
                    return inSection;
                case TableBlock table:
                    foreach (var cell in table.Rows.SelectMany(row => row.Cells))
                    {
                        if (cell.Content.Blocks.FirstOrDefault() is ParagraphBlock { } first && first.Id == paragraphId) return cell;
                        if (Cell(cell.Content, paragraphId) is { } nested) return nested;
                    }
                    break;
            }
        }
        return null;
    }
}
