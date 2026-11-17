using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round155PlainTextTests
{
    private const string Spaced = "Big  sale\tnow";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    [Fact]
    public void Fontwork_text_is_written_with_odf_space_and_tab_markup()
    {
        var package = Ods(Spaced);
        var paragraph = Assert.Single(Entry(package, "content.xml").Descendants(Draw + "custom-shape").Elements(Text + "p"));
        AssertMarkup(paragraph);
        Assert.Equal(Spaced, Assert.IsType<FontworkObject>(Assert.Single(ReadOds(package).Sheets[0].Objects).Content).Text);
    }

    [Fact]
    public void Fontwork_text_from_another_suite_keeps_spaces_tabs_and_breaks()
    {
        var package = Patch(Ods("x"), "content.xml", content =>
        {
            var shape = content.Descendants(Draw + "custom-shape").Single();
            shape.Elements(Text + "p").Remove();
            shape.AddFirst(
                new XElement(Text + "p", "A", new XElement(Text + "s", new XAttribute(Text + "c", "2")), "B", new XElement(Text + "tab"), "C", new XElement(Text + "line-break"), "D"),
                new XElement(Text + "p", "E"));
        });

        Assert.Equal("A  B\tC D E", Assert.IsType<FontworkObject>(Assert.Single(ReadOds(package).Sheets[0].Objects).Content).Text);
    }

    [Fact]
    public void Inline_chart_names_and_categories_keep_spaces_tabs_and_breaks()
    {
        var data = new ChartData([Spaced, "Two\nlines"], [new ChartSeries("Net  sales\tEU", [1, 2])]);
        var package = Odp(new ChartDefinition { Id = "ch1", Type = ChartType.Column, SourceSheet = "", SourceRange = "", InlineData = data });
        var cells = Entry(package, "Object", "/content.xml").Descendants(Table + "table-cell")
            .Where(cell => (string?)cell.Attribute(Office + "value-type") == "string" && cell.Elements(Text + "p").Any(p => p.Value.Length > 0)).ToList();
        Assert.Equal(3, cells.Count);
        Assert.All(cells, cell => Assert.DoesNotContain(cell.DescendantNodes().OfType<XText>(), text => text.Value.Contains('\n') || text.Value.Contains('\t') || text.Value.Contains("  ")));

        var read = Assert.IsType<ChartDefinition>(Assert.Single(ReadOdp(package).Slides[0].Elements.OfType<ObjectElement>()).Content).InlineData!;
        Assert.Equal(data.Categories, read.Categories);
        Assert.Equal("Net  sales\tEU", Assert.Single(read.Series).Name);
    }

    [Fact]
    public void Odp_equation_source_keeps_its_lines_and_spaces_as_odf_markup()
    {
        const string expression = "a over b\nnewline  c";
        var package = Odp(new FormulaObject { Id = "f1", Expression = expression });

        var image = Entry(package, "content.xml").Descendants(Draw + "image")
            .Single(element => (string?)element.Attribute(Draw + "mime-type") == "application/vnd.oasis.opendocument.formula");
        Assert.Equal(2, image.Elements(Text + "p").Count());
        Assert.Single(image.Descendants(Text + "s"));
        Assert.Equal(expression, Assert.IsType<FormulaObject>(Assert.Single(ReadOdp(package).Slides[0].Elements.OfType<ObjectElement>()).Content).Expression);
    }

    [Fact]
    public void Odt_watermark_text_is_written_with_odf_markup_and_read_back()
    {
        var document = Folio();
        document.Styles.Page.Find(DefaultStyleNames.Standard)!.Watermark = new PageWatermark(Spaced);
        var package = Odt(document);

        var shape = Entry(package, "styles.xml").Descendants(Draw + "custom-shape").Single();
        AssertMarkup(Assert.Single(shape.Elements(Text + "p")));
        Assert.Equal(Spaced, ReadOdt(package).Styles.Page.Find(DefaultStyleNames.Standard)!.Watermark!.Text);
    }

    [Fact]
    public void Odt_tracked_change_note_keeps_lines_spaces_and_tabs()
    {
        const string note = "First  line\tend\nSecond";
        var document = Folio();
        document.Body.Replace([new ParagraphBlock("p1", "Hello world") { Marks = [new ChangeMark(0, "ct1", MarkEdge.Start), new ChangeMark(5, "ct1", MarkEdge.End)] }]);
        document.Changes.Add(new Insertion("ct1", "Ada", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)) { Comment = note });
        var package = Odt(document);

        var info = Entry(package, "content.xml").Descendants(Office + "change-info").Single();
        Assert.Equal(2, info.Elements(Text + "p").Count());
        Assert.DoesNotContain(info.DescendantNodes().OfType<XText>(), text => text.Value.Contains('\n') || text.Value.Contains('\t') || text.Value.Contains("  "));
        Assert.Equal(note, Assert.Single(ReadOdt(package).Changes.All).Comment);
    }

    [Fact]
    public void Dimension_label_keeps_spaces_and_tabs_both_ways()
    {
        const string body = "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
            + "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\"><office:body><office:text>"
            + "<text:p>a<draw:measure text:anchor-type=\"paragraph\" svg:x1=\"1cm\" svg:y1=\"1cm\" svg:x2=\"4cm\" svg:y2=\"1cm\"><text:p>A<text:s text:c=\"2\"/>B<text:tab/>C</text:p></draw:measure></text:p>"
            + "</office:text></office:body></office:document-content>";
        var source = new MemoryStream();
        using (var archive = new ZipArchive(source, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.text");
            using var content = new StreamWriter(archive.CreateEntry("content.xml").Open());
            content.Write(body);
        }
        source.Position = 0;

        var document = ReadOdt(source.ToArray());
        var label = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(Assert.IsType<PulseOffice.Documents.Objects.EmbeddedContent>(Assert.Single(document.Objects.All).Content).Value).Dimension!.Label;
        Assert.Equal("A  B\tC", label);

        var measure = Entry(Odt(document), "content.xml").Descendants(Draw + "measure").Single();
        AssertMarkup(Assert.Single(measure.Elements(Text + "p")));
    }

    [Fact]
    public void Ods_validation_messages_keep_spaces_and_tabs_as_odf_markup()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        sheet.Validations.Add(new DataValidation
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)),
            Kind = ValidationKind.WholeNumber,
            Operator = ValidationOperator.Between,
            FirstFormula = "1",
            SecondFormula = "10",
            InputMessage = Spaced,
            ErrorMessage = Spaced,
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        var package = stream.ToArray();

        var messages = Entry(package, "content.xml").Descendants().Where(element => element.Name.LocalName is "help-message" or "error-message").ToList();
        Assert.Equal(2, messages.Count);
        Assert.All(messages, message => AssertMarkup(Assert.Single(message.Elements(Text + "p"))));
        var validation = Assert.Single(ReadOds(package).Sheets[0].Validations);
        Assert.Equal(Spaced, validation.InputMessage);
        Assert.Equal(Spaced, validation.ErrorMessage);
    }

    private static void AssertMarkup(XElement paragraph)
    {
        Assert.Single(paragraph.Descendants(Text + "tab"));
        Assert.Single(paragraph.Descendants(Text + "s"));
        Assert.DoesNotContain(paragraph.DescendantNodes().OfType<XText>(), text => text.Value.Contains('\t') || text.Value.Contains("  "));
    }

    private static byte[] Ods(string text)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Data").Objects.Add(new PlacedObject
        {
            Id = "art1",
            Content = new FontworkObject { Id = "art1", Text = text },
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(3)),
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        return stream.ToArray();
    }

    private static Workbook ReadOds(byte[] package)
    {
        var result = new OdsReader().Read(new MemoryStream(package));
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static byte[] Odp(EmbeddedObject content)
    {
        var theme = new ThemeCatalog().Find("Slate")!;
        var master = new MasterFactory().CreateStandard(theme);
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
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        return stream.ToArray();
    }

    private static Presentation ReadOdp(byte[] package)
    {
        var result = new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(new MemoryStream(package));
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static TextDocument Folio()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([new ParagraphBlock("p1", "Hello")]);
        return document;
    }

    private static byte[] Odt(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        return stream.ToArray();
    }

    private static TextDocument ReadOdt(byte[] package)
    {
        var result = new OdtReader().Read(new MemoryStream(package));
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static XDocument Entry(byte[] package, string prefix, string suffix = "")
    {
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        var entry = suffix.Length == 0
            ? zip.GetEntry(prefix)!
            : zip.Entries.First(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal) && e.FullName.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = entry.Open();
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
                var copy = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
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
