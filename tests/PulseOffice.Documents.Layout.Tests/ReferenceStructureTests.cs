using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Documents.Layout.Measurement.OpenType;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class FlatOdtStructureLoader
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";

    private LayoutFixture _fixture = null!;
    private XElement _root = null!;

    public void Load(LayoutFixture fixture, string path)
    {
        _fixture = fixture;
        _root = XDocument.Load(path).Root!;
        var document = fixture.Document;
        foreach (var style in _root.Descendants(Style + "style").Where(style => (string?)style.Attribute(Style + "family") == "paragraph"))
        {
            var name = (string)style.Attribute(Style + "name")!;
            var paragraph = style.Element(Style + "paragraph-properties");
            var text = style.Element(Style + "text-properties");
            document.Styles.Paragraph.Add(new ParagraphStyle(name)
            {
                ParentName = name == "Standard" ? null : (string?)style.Attribute(Style + "parent-style-name"),
                Paragraph = new ParagraphProperties
                {
                    SpaceBefore = Points(paragraph?.Attribute(Fo + "margin-top")),
                    SpaceAfter = Points(paragraph?.Attribute(Fo + "margin-bottom")),
                    MarginLeft = Points(paragraph?.Attribute(Fo + "margin-left")),
                    Indent = Points(paragraph?.Attribute(Fo + "text-indent")),
                },
                Character = new CharacterProperties { FontSize = Points(text?.Attribute(Fo + "font-size")) },
            });
        }
        var page = document.Styles.Page.Find(LayoutFixture.Page)!;
        var layout = _root.Descendants(Style + "page-layout").First();
        var properties = layout.Element(Style + "page-layout-properties")!;
        page.Size = new DocumentSize(Twip.FromPoints(Points(properties.Attribute(Fo + "page-width"))!.Value), Twip.FromPoints(Points(properties.Attribute(Fo + "page-height"))!.Value));
        page.Margins = new Margins(Length(properties, "margin-left"), Length(properties, "margin-top"), Length(properties, "margin-right"), Length(properties, "margin-bottom"));
        page.NumberFormat = (string?)properties.Attribute(Style + "num-format") switch
        {
            "i" => NumberFormat.LowerRoman,
            "I" => NumberFormat.UpperRoman,
            "a" => NumberFormat.LowerLetter,
            "A" => NumberFormat.UpperLetter,
            _ => NumberFormat.Arabic,
        };
        if (properties.Element(Style + "columns") is { } columns && (string?)columns.Attribute(Fo + "column-count") is not null)
        {
            page.Columns = new SectionColumns(int.Parse((string)columns.Attribute(Fo + "column-count")!, CultureInfo.InvariantCulture), Length(columns, "column-gap"));
        }
        var master = _root.Descendants(Style + "master-page").First();
        if (master.Element(Style + "header") is { } header)
        {
            page.Header.Enabled = true;
            page.Header.Spacing = Length(layout.Element(Style + "header-style")!.Element(Style + "header-footer-properties")!, "margin-bottom");
            foreach (var p in header.Elements(Text + "p")) page.Header.Default.Add(Paragraph(p, new BlockContainer()));
        }
        if (master.Element(Style + "footer") is { } footer)
        {
            page.Footer.Enabled = true;
            page.Footer.Spacing = Length(layout.Element(Style + "footer-style")!.Element(Style + "header-footer-properties")!, "margin-top");
            foreach (var p in footer.Elements(Text + "p")) page.Footer.Default.Add(Paragraph(p, new BlockContainer()));
        }
        var padding = _root.Descendants(Style + "table-cell-properties").Select(cell => Points(cell.Attribute(Fo + "padding"))).FirstOrDefault() ?? 0;
        document.Styles.Table.Add(new TableStyle("Tab") { Table = new TableProperties { CellPadding = padding, BorderWidth = 0 } });
        var body = _root.Descendants(Office + "text").First();
        foreach (var element in body.Elements())
        {
            if (element.Name == Text + "p") Paragraph(element, document.Body);
            else if (element.Name == Table + "table") document.Body.Add(TableOf(element));
        }
    }

    private ParagraphBlock Paragraph(XElement element, BlockContainer container)
    {
        var document = _fixture.Document;
        var id = document.NextId("p");
        var runs = new List<TextRun>();
        var marks = new List<InlineMark>();
        var length = 0;
        foreach (var node in element.Nodes())
        {
            if (node is XText text)
            {
                runs.Add(new TextRun(text.Value));
                length += text.Value.Length;
            }
            else if (node is XElement child)
            {
                if (child.Name == Text + "page-number") runs.Add(new TextRun(child.Value) { Field = TextFieldKind.PageNumber });
                else if (child.Name == Text + "page-count") runs.Add(new TextRun(child.Value) { Field = TextFieldKind.PageCount });
                else if (child.Name == Text + "note")
                {
                    var noteId = document.NextId("n");
                    var content = new BlockContainer();
                    foreach (var p in child.Element(Text + "note-body")!.Elements(Text + "p")) Paragraph(p, content);
                    document.Notes.Add(new Note(noteId, NoteKind.Footnote, content));
                    marks.Add(new NoteMark(length, noteId));
                    continue;
                }
                else if (child.Name == Draw + "frame")
                {
                    var frameContent = new BlockContainer();
                    foreach (var p in child.Element(Draw + "text-box")?.Elements(Text + "p") ?? []) Paragraph(p, frameContent);
                    var styleName = (string?)child.Attribute(Draw + "style-name");
                    var graphic = GraphicProperties(styleName);
                    var padding = Padding(styleName);
                    if (styleName is not null && padding is not null && document.Styles.Frame.Find(styleName) is null)
                    {
                        document.Styles.Frame.Add(new FrameStyle(styleName) { Frame = new FrameProperties { Padding = padding } });
                    }
                    document.Objects.Add(new AnchoredObject(document.NextId("o"), new ParagraphAnchor(id), new TextBoxContent(frameContent),
                        new DocumentSize(Length(child, "width", Svg), Length(child, "height", Svg)))
                    {
                        Placement = new ObjectPlacement(Length(child, "x", Svg), Length(child, "y", Svg)),
                        Wrap = WrapMode.Parallel,
                        StyleName = padding is null ? null : styleName,
                        WrapSpacing = graphic is null ? Margins.Zero : new Margins(Length(graphic, "margin-left"), Length(graphic, "margin-top"), Length(graphic, "margin-right"), Length(graphic, "margin-bottom")),
                    });
                    continue;
                }
                else
                {
                    runs.Add(new TextRun(child.Value));
                }
                length += runs[^1].Text.Length;
            }
        }
        var paragraph = new TextParagraph(string.Empty) { StyleName = (string?)element.Attribute(Text + "style-name") }.WithRuns(runs);
        var block = new ParagraphBlock(id, paragraph) { Marks = marks };
        container.Add(block);
        return block;
    }

    private XElement? GraphicProperties(string? name)
    {
        while (name is not null)
        {
            var style = _root.Descendants(Style + "style").FirstOrDefault(s => (string?)s.Attribute(Style + "name") == name && (string?)s.Attribute(Style + "family") == "graphic");
            if (style is null) return null;
            if (style.Element(Style + "graphic-properties") is { } properties && properties.Attribute(Fo + "margin-left") is not null) return properties;
            name = (string?)style.Attribute(Style + "parent-style-name");
        }
        return null;
    }

    private double? Padding(string? name)
    {
        while (name is not null)
        {
            var style = _root.Descendants(Style + "style").FirstOrDefault(s => (string?)s.Attribute(Style + "name") == name && (string?)s.Attribute(Style + "family") == "graphic");
            if (style is null) return null;
            if (Points(style.Element(Style + "graphic-properties")?.Attribute(Fo + "padding")) is { } value) return value;
            name = (string?)style.Attribute(Style + "parent-style-name");
        }
        return null;
    }

    private TableBlock TableOf(XElement element)
    {
        var widths = element.Elements(Table + "table-column").Select(column =>
        {
            var style = _root.Descendants(Style + "style").First(s => (string?)s.Attribute(Style + "name") == (string?)column.Attribute(Table + "style-name"));
            return Length(style.Element(Style + "table-column-properties")!, "column-width", Style);
        }).ToList();
        var header = element.Element(Table + "table-header-rows")?.Elements(Table + "table-row").ToList() ?? [];
        var rows = header.Concat(element.Elements(Table + "table-row")).Select(row =>
            new TableRow(row.Elements().Where(cell => cell.Name == Table + "table-cell" || cell.Name == Table + "covered-table-cell").Select(cell =>
            {
                var content = new BlockContainer();
                foreach (var p in cell.Elements(Text + "p")) Paragraph(p, content);
                var spanned = (string?)cell.Attribute(Table + "number-rows-spanned");
                return new TableCell(content)
                {
                    Covered = cell.Name == Table + "covered-table-cell",
                    RowSpan = spanned is null ? 1 : int.Parse(spanned, CultureInfo.InvariantCulture),
                };
            }))).ToList();
        return new TableBlock(_fixture.Document.NextId("t"), rows, widths) { HeaderRows = header.Count, StyleName = "Tab" };
    }

    private Twip Length(XElement element, string name, XNamespace? space = null) => Twip.FromPoints(Points(element.Attribute((space ?? Fo) + name)) ?? 0);

    private double? Points(XAttribute? attribute)
    {
        if (attribute is null) return null;
        var value = attribute.Value;
        var number = double.Parse(value[..^2], CultureInfo.InvariantCulture);
        return value.EndsWith("cm", StringComparison.Ordinal) ? number * 72 / 2.54 : number;
    }
}

public sealed class ReferenceStructureTests(ITestOutputHelper output)
{
    private const string Fonts = "/Applications/LibreOffice.app/Contents/Resources/fonts/truetype";

    public static TheoryData<string, double> Documents() => new()
    {
        { "06-header-footer", 1.0 },
        { "07-columns", 1.0 },
        { "08-footnotes", 1.0 },
        { "09-table", 1.0 },
        { "10-image-wrap", 1.0 },
        { "11-column-notes", 1.0 },
        { "12-merged-rows", 1.0 },
        { "13-page-fields", 1.0 },
        { "14-frame-text", 1.0 },
        { "15-merged-split", 1.0 },
        { "16-roman-pages", 1.0 },
        { "17-column-pending-notes", 1.0 },
        { "18-frame-padding", 1.0 },
        { "19-merged-group", 1.0 },
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void LaStrutturaDellaPaginaCoincideConIlRiferimento(string name, double threshold)
    {
        Assert.SkipUnless(Directory.Exists(Fonts), "Reference fonts are not installed.");
        var measurer = new OpenTypeMeasurer(new PulseOffice.Typography.OpenType.Metrics.DirectoryFontLocator([Fonts]), new HorizontalHeaderMetrics(), new ExactAdvances(), new FixedPitchMeasurer());
        var fixture = new LayoutFixture(width: 595.28, height: 841.89, measurer);
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Reference");
        new FlatOdtStructureLoader().Load(fixture, Path.Combine(directory, name + ".fodt"));

        var layout = fixture.Layout();
        var expected = Expected(Path.Combine(directory, name + ".txt"));
        var ours = layout.Pages.Select(Candidates).ToList();

        var matching = 0;
        var total = expected.Sum(p => p.Count);
        for (var p = 0; p < Math.Min(expected.Count, ours.Count); p++)
        {
            var pool = ours[p].ToList();
            foreach (var line in expected[p])
            {
                var index = pool.IndexOf(line);
                if (index >= 0)
                {
                    matching++;
                    pool.RemoveAt(index);
                }
                else
                {
                    output.WriteLine($"page {p + 1} LO: {line}");
                }
            }
            foreach (var line in pool.Where(line => expected.All(page => !page.Contains(line)))) output.WriteLine($"page {p + 1} us: {line}");
        }
        output.WriteLine($"{name}: pages LO {expected.Count} us {ours.Count}; lines matching {matching}/{total}");
        Assert.Equal(expected.Count, ours.Count);
        Assert.True(matching >= total * threshold, $"{matching}/{total}");
    }

    private IEnumerable<string> Candidates(PageBox page)
    {
        var lines = page.AllLines.ToList();
        foreach (var line in lines) yield return Key(Text(line));
        foreach (var row in lines.GroupBy(line => Math.Round(line.Baseline, 1)).Where(group => group.Count() > 1))
        {
            yield return Key(string.Concat(row.OrderBy(line => line.Left).Select(Text)));
        }
    }

    private string Text(LineBox line) =>
        string.Concat(line.Line.Runs.Where(run => run.Kind is GlyphRunKind.Text or GlyphRunKind.Hyphen or GlyphRunKind.Label or GlyphRunKind.Citation).Select(run => run.Text));

    private string Key(string text) => Regex.Replace(text, @"\s+", string.Empty);

    private List<List<string>> Expected(string path)
    {
        var pages = new List<List<string>>();
        foreach (var raw in File.ReadAllLines(path))
        {
            if (raw.StartsWith("pagine:", StringComparison.Ordinal)) continue;
            if (raw.StartsWith("--- pagina", StringComparison.Ordinal))
            {
                pages.Add([]);
                continue;
            }
            var line = Key(raw.Replace("\uFFFC", string.Empty, StringComparison.Ordinal));
            if (line.Length > 0) pages[^1].Add(line);
        }
        return pages;
    }
}
