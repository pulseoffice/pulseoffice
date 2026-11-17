using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round155PlainTextTests
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Fact]
    public void Fontwork_line_breaks_and_paragraphs_from_another_suite_become_spaces()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Data").Objects.Add(new PlacedObject
        {
            Id = "art1",
            Content = new FontworkObject { Id = "art1", Text = "x" },
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(3)),
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);

        var package = Patch(stream.ToArray(), "xl/drawings/drawing1.xml", drawing =>
        {
            var paragraph = drawing.Descendants(A + "p").Single();
            var run = paragraph.Element(A + "r")!;
            paragraph.ReplaceWith(
                new XElement(A + "p", Run(run, "A\tB"), new XElement(A + "br"), Run(run, "C")),
                new XElement(A + "p", Run(run, "D")));
        });

        var result = new XlsxReader().Read(new MemoryStream(package));
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.Equal("A\tB C D", Assert.IsType<FontworkObject>(Assert.Single(result.Value!.Sheets[0].Objects).Content).Text);
    }

    [Fact]
    public void Pptx_equation_source_keeps_its_lines_as_paragraphs()
    {
        const string expression = "a over b\nnewline  c";
        var package = Pptx(new FormulaObject { Id = "f1", Expression = expression });

        var shape = Slide(package).Descendants().Single(element => element.Name.LocalName == "sp");
        Assert.Equal(2, shape.Descendants(A + "p").Count());
        Assert.DoesNotContain(shape.Descendants(A + "t"), text => text.Value.Contains('\n'));
        Assert.Equal(expression, Assert.IsType<FormulaObject>(Read(package).Content).Expression);
    }

    [Fact]
    public void Pptx_inline_chart_names_and_categories_keep_spaces_tabs_and_breaks()
    {
        var data = new ChartData(["Big  sale\tnow", "Two\nlines", " lead"], [new ChartSeries("Net  sales\tEU", [1, 2, 3])]);
        var package = Pptx(new ChartDefinition { Id = "ch1", Type = ChartType.Column, SourceSheet = "", SourceRange = "", InlineData = data });

        var read = Assert.IsType<ChartDefinition>(Read(package).Content).InlineData!;
        Assert.Equal(data.Categories, read.Categories);
        Assert.Equal("Net  sales\tEU", Assert.Single(read.Series).Name);
    }

    [Fact]
    public void Pptx_drawn_shape_plain_text_keeps_its_lines()
    {
        const string text = "One  line\nTwo";
        var package = Pptx(new PulseOffice.Drawing.Shapes.ShapeObject { Id = "s1", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle, Text = text });

        Assert.DoesNotContain(Slide(package).Descendants(A + "t"), run => run.Value.Contains('\n'));
        Assert.Equal(text, Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(Read(package).Content).Text);
    }

    private static XElement Run(XElement template, string text) =>
        new(A + "r", template.Element(A + "rPr"), new XElement(A + "t", text));

    private byte[] Pptx(EmbeddedObject content)
    {
        var theme = _themes.Find("Slate")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        slide.Add(new ObjectElement
        {
            Id = content.Id,
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(14), Twip.FromCentimeters(9)),
            Content = content,
        });
        deck.AddExisting(slide);
        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        return stream.ToArray();
    }

    private ObjectElement Read(byte[] package)
    {
        var result = new PptxReader(_themes, _masters).Read(new MemoryStream(package));
        Assert.True(result.Succeeded);
        return Assert.Single(result.Value!.Slides[0].Elements.OfType<ObjectElement>());
    }

    private static XDocument Slide(byte[] package)
    {
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("ppt/slides/slide1.xml")!.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static byte[] Patch(byte[] package, string name, Action<XDocument> change)
    {
        var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName);
                using var from = entry.Open();
                using var to = copy.Open();
                if (entry.FullName != name)
                {
                    from.CopyTo(to);
                    continue;
                }
                var document = XDocument.Load(from, LoadOptions.PreserveWhitespace);
                change(document);
                document.Save(to, SaveOptions.DisableFormatting);
            }
        }
        return output.ToArray();
    }
}
