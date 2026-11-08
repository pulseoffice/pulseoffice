using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxStoredPageStyles(DocxSectionCodec sections, IDefaultStyleSheetFactory defaults, DocxCustomXmlParts parts)
{
    public const string Namespace = "urn:pulseoffice:page-styles";

    private const int LargestCount = 512;

    private readonly XNamespace _ps = Namespace;
    private readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private readonly Lazy<StyleCollection<PageStyle>> _standard = new(() => defaults.Create().Page);

    public IEnumerable<string> UsedNames(TextDocument document)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(Blocks(document.Body).OfType<ParagraphBlock>().SelectMany(paragraph => new[] { paragraph.Content.PageStyleName, paragraph.Content.NextPageStyleName })
            .Concat(Blocks(document.Body).OfType<TableBlock>().Select(table => table.PageStyleName))
            .OfType<string>().Append(document.InitialPageStyle));
        while (pending.TryPop(out var name))
        {
            if (!used.Add(name)) continue;
            if (document.Styles.Page.Find(name)?.NextStyleName is { } next) pending.Push(next);
        }
        return used;
    }

    public void Write(MainDocumentPart main, DocxWriteContext context)
    {
        var written = context.WrittenPageStyles.ToHashSet(StringComparer.Ordinal);
        var stored = context.Document.Styles.Page.All.Where(style => !written.Contains(style.Name) && Worth(style)).Take(LargestCount).ToList();
        if (stored.Count == 0) return;
        var root = new XElement(_ps + "pageStyles", new XAttribute(XNamespace.Xmlns + "ps", _ps));
        foreach (var style in stored)
        {
            var section = sections.Write(style, null, false, true, main, context);
            var entry = new XElement(_ps + "pageStyle", new XAttribute("name", style.Name), XElement.Parse(section.OuterXml));
            if (style.Usage != PageUsage.All) entry.Add(new XAttribute("usage", style.Usage.ToString()));
            if (style.NextStyleName is { } next) entry.Add(new XAttribute("next", next));
            if (style.EvenPagesDifferent) entry.Add(new XAttribute("even", "true"));
            root.Add(entry);
        }

        parts.Add(main, root);
    }

    public void Read(MainDocumentPart main, DocxReadContext context)
    {
        var document = context.Document;
        var used = UsedNames(document).ToHashSet(StringComparer.Ordinal);
        foreach (var root in parts.Roots(main, _ps + "pageStyles"))
        {
            foreach (var entry in root.Elements(_ps + "pageStyle").Take(LargestCount))
            {
                if (entry.Attribute("name")?.Value is not { Length: > 0 } name || used.Contains(name)) continue;
                if (entry.Element(_w + "sectPr") is not { } xml) continue;
                W.SectionProperties section;
                try
                {
                    section = new W.SectionProperties(xml.ToString(SaveOptions.DisableFormatting));
                }
                catch (System.Xml.XmlException)
                {
                    continue;
                }
                var watermark = context.Watermark;
                var lines = document.LineNumbering;
                var style = sections.Read(name, section, main, context, out _);
                context.Watermark = watermark;
                document.LineNumbering = lines;
                if (Enum.TryParse<PageUsage>(entry.Attribute("usage")?.Value, out var usage) && Enum.IsDefined(usage)) style.Usage = usage;
                style.NextStyleName = entry.Attribute("next")?.Value is { Length: > 0 } next ? next : null;
                style.EvenPagesDifferent = entry.Attribute("even")?.Value == "true";
                document.Styles.Page.Replace(style);
            }
        }
    }

    private bool Worth(PageStyle style) =>
        style.Header.Enabled || style.Footer.Enabled || style.Header.First is not null || style.Header.Even is not null
        || style.Footer.First is not null || style.Footer.Even is not null || style.Watermark is not null
        || _standard.Value.Find(style.Name) is null;

    private IEnumerable<Block> Blocks(BlockContainer container) => container.Blocks.SelectMany(block => block switch
    {
        SectionBlock section => [block, .. Blocks(section.Content)],
        TableBlock table => [block, .. table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Blocks(cell.Content))],
        _ => (IEnumerable<Block>)[block],
    });
}
