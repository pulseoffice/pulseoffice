using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Documents.Layout.Measurement.OpenType;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class FlatOdtFixture
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";

    public void Load(LayoutFixture fixture, string path)
    {
        var root = XDocument.Load(path).Root!;
        var styles = root.Descendants(Style + "style")
            .Where(style => (string?)style.Attribute(Style + "family") == "paragraph")
            .ToDictionary(style => (string)style.Attribute(Style + "name")!);
        foreach (var element in root.Descendants(Text + "p"))
        {
            styles.TryGetValue((string?)element.Attribute(Text + "style-name") ?? string.Empty, out var style);
            var paragraph = style?.Element(Style + "paragraph-properties");
            var text = style?.Element(Style + "text-properties");
            var size = Points(text?.Attribute(Fo + "font-size")) ?? 12;
            var bold = (string?)text?.Attribute(Fo + "font-weight") == "bold";
            fixture.Add(element.Value, p => Shape(p, paragraph, size, bold));
        }
    }

    private TextParagraph Shape(TextParagraph p, XElement? properties, double size, bool bold)
    {
        var explicitFlags = ParagraphOverrides.None;
        var align = (string?)properties?.Attribute(Fo + "text-align") switch
        {
            "justify" => TextAlign.Justify,
            "center" => TextAlign.Center,
            "end" or "right" => TextAlign.Right,
            _ => TextAlign.Left,
        };
        if (align != TextAlign.Left) explicitFlags |= ParagraphOverrides.Align;
        var line = (string?)properties?.Attribute(Fo + "line-height");
        var spacing = line is not null && line.EndsWith('%') ? double.Parse(line[..^1], CultureInfo.InvariantCulture) / 100 : 1.0;
        if (line is not null) explicitFlags |= ParagraphOverrides.LineSpacing;
        var before = Points(properties?.Attribute(Fo + "margin-top")) ?? 0;
        var after = Points(properties?.Attribute(Fo + "margin-bottom")) ?? 0;
        explicitFlags |= ParagraphOverrides.SpaceBefore | ParagraphOverrides.SpaceAfter;
        return p with
        {
            Runs = [new TextRun(p.Text) { FontSize = size, Bold = bold }],
            Align = align,
            LineSpacing = spacing,
            SpaceBefore = before,
            SpaceAfter = after,
            MarginLeft = Points(properties?.Attribute(Fo + "margin-left")),
            MarginRight = Points(properties?.Attribute(Fo + "margin-right")),
            Indent = Points(properties?.Attribute(Fo + "text-indent")),
            KeepWithNext = (string?)properties?.Attribute(Fo + "keep-with-next") == "always" ? true : null,
            PageBreakBefore = (string?)properties?.Attribute(Fo + "break-before") == "page" ? true : null,
            Widows = Number(properties?.Attribute(Fo + "widows")),
            Orphans = Number(properties?.Attribute(Fo + "orphans")),
            Explicit = explicitFlags,
        };
    }

    private int? Number(XAttribute? attribute) => attribute is null ? null : int.Parse(attribute.Value, CultureInfo.InvariantCulture);

    private double? Points(XAttribute? attribute)
    {
        if (attribute is null) return null;
        var value = attribute.Value;
        var number = double.Parse(value[..^2], CultureInfo.InvariantCulture);
        return value.EndsWith("cm", StringComparison.Ordinal) ? number * 72 / 2.54 : number;
    }
}

public sealed class ReferenceComparisonTests(ITestOutputHelper output)
{
    private const string Fonts = "/Applications/LibreOffice.app/Contents/Resources/fonts/truetype";

    public static TheoryData<string> Documents() => ["01-plain", "02-justify", "03-multipage", "04-align-break", "05-widows"];

    [Theory]
    [MemberData(nameof(Documents))]
    public void LeRigheEIPagineCoincidonoConIlRiferimento(string name)
    {
        Assert.SkipUnless(Directory.Exists(Fonts), "Reference fonts are not installed.");
        var measurer = new OpenTypeMeasurer(new PulseOffice.Typography.OpenType.Metrics.DirectoryFontLocator([Fonts]), new HorizontalHeaderMetrics(), new ExactAdvances(), new FixedPitchMeasurer());
        var fixture = new LayoutFixture(width: 595.28, height: 841.89, measurer);
        var page = fixture.Document.Styles.Page.Find(LayoutFixture.Page)!;
        page.Margins = new Foundation.Units.Margins(Foundation.Units.Twip.FromCentimeters(2), Foundation.Units.Twip.FromCentimeters(2), Foundation.Units.Twip.FromCentimeters(2), Foundation.Units.Twip.FromCentimeters(2));
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Reference");
        new FlatOdtFixture().Load(fixture, Path.Combine(directory, name + ".fodt"));

        var layout = fixture.Layout();
        var expected = Expected(Path.Combine(directory, name + ".txt"));
        var ours = layout.Pages.Select(p => p.Lines.Select(line => Normalize(Text(line))).ToList()).ToList();

        var matching = 0;
        var total = expected.Sum(p => p.Count);
        for (var p = 0; p < Math.Min(expected.Count, ours.Count); p++)
        {
            for (var l = 0; l < Math.Min(expected[p].Count, ours[p].Count); l++)
            {
                if (expected[p][l] == ours[p][l]) matching++;
                else output.WriteLine($"page {p + 1} line {l + 1}\n  LO : {expected[p][l]}\n  us : {ours[p][l]}");
            }
        }
        var breaks = new HashSet<string>(expected.SelectMany(p => p));
        var sameBreaks = ours.SelectMany(p => p).Count(breaks.Contains);
        output.WriteLine($"{name}: line breaks matching {sameBreaks}/{ours.Sum(p => p.Count)}");
        output.WriteLine($"{name}: lines per page LO {string.Join(',', expected.Select(p => p.Count))} us {string.Join(',', ours.Select(p => p.Count))}");
        output.WriteLine($"{name}: pages LO {expected.Count} us {ours.Count}; lines matching {matching}/{total}");
        Assert.Equal(expected.Count, ours.Count);
        Assert.Equal(ours.Sum(p => p.Count), sameBreaks);
        Assert.Equal(total, matching);
    }

    private string Text(LineBox line) =>
        string.Concat(line.Line.Runs.Where(run => run.Kind is GlyphRunKind.Text or GlyphRunKind.Hyphen).Select(run => run.Text));

    private string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();

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
            var line = Normalize(raw);
            if (line.Length > 0) pages[^1].Add(line);
        }
        return pages;
    }
}
