using System.Diagnostics;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

[CollectionDefinition(nameof(OpenXmlLargeDocumentPerformanceTests), DisableParallelization = true)]
[Collection(nameof(OpenXmlLargeDocumentPerformanceTests))]
public sealed class OpenXmlLargeDocumentPerformanceTests(ITestOutputHelper output)
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private long Timed(string label, Action action)
    {
        var watch = Stopwatch.StartNew();
        action();
        output.WriteLine($"{label}: {watch.ElapsedMilliseconds} ms");
        return watch.ElapsedMilliseconds;
    }

    [Fact]
    public void A_sheet_with_one_hundred_thousand_rows_writes_and_reads_in_linear_time()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        for (var row = 0; row < 100_000; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Text("r" + row));
        }
        using var stream = new MemoryStream();

        var write = Timed("XLSX write", () => new XlsxWriter().Write(workbook, stream));
        stream.Position = 0;
        Workbook? read = null;
        var load = Timed("XLSX read", () => read = new XlsxReader().Read(stream).Value);

        Assert.True(write < 30_000, $"write {write} ms");
        Assert.True(load < 30_000, $"read {load} ms");
        Assert.Equal("r99999", read!.Sheets[0].GetValue(new CellAddress(99_999, 1)).AsText());
    }

    [Theory]
    [InlineData("style")]
    [InlineData("merge")]
    [InlineData("comment")]
    [InlineData("link")]
    [InlineData("format")]
    public void A_sheet_with_many_merges_comments_and_styles_writes_and_reads_in_linear_time(string kind)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        for (var row = 0; row < 40_000; row++)
        {
            var address = new CellAddress(row * 2, 0);
            sheet.SetValue(address, CellValue.Number(row));
            if (kind == "style") sheet.SetStyle(address, workbook.Styles.Intern(new PulseOffice.Sheets.Styling.CellStyle { BackgroundHex = "#" + (row % 3_000).ToString("X6") }));
            if (kind == "merge") sheet.Merges.Add(new CellRange(address, new CellAddress(row * 2 + 1, 1)));
            if (kind == "format") sheet.SetStyle(address, workbook.Styles.Intern(new PulseOffice.Sheets.Styling.CellStyle { NumberFormatId = workbook.NumberFormats.Register("0.00\" u" + row + "\"") }));
            if (kind == "link") sheet[address] = sheet[address] with { Hyperlink = "https://example.com/" + row };
            if (kind == "comment") sheet.Comments[new CellAddress(row * 2, 3)] = new PulseOffice.Sheets.CellComment { Text = "c" + row };
        }
        using var stream = new MemoryStream();

        var write = Timed("XLSX write " + kind, () => new XlsxWriter().Write(workbook, stream));
        stream.Position = 0;
        Workbook? read = null;
        var load = Timed("XLSX read " + kind, () => read = new XlsxReader().Read(stream).Value);

        Assert.True(write < 8_000, $"write {write} ms");
        Assert.True(load < 4_000, $"read {load} ms");
        Assert.Equal(kind == "merge" ? 40_000 : 0, read!.Sheets[0].Merges.Count);
        Assert.Equal(kind == "comment" ? 40_000 : 0, read.Sheets[0].Comments.Count);
        Assert.Equal(kind == "link" ? "https://example.com/39999" : null, read.Sheets[0][new CellAddress(79_998, 0)].Hyperlink);
    }

    [Fact]
    public void A_sheet_with_many_linked_and_described_shapes_writes_and_reads_in_linear_time()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        for (var index = 0; index < 12_000; index++)
            sheet.Objects.Add(new PlacedObject
            {
                Id = "shape" + index,
                Anchor = new CellAddress(index, 0),
                Hyperlink = "https://example.com/" + index,
                Content = new ShapeObject { Id = "shape" + index, Shape = ShapeKind.Rectangle, Title = "t" + index, Description = "d" + index },
            });
        using var stream = new MemoryStream();

        var write = Timed("XLSX write shapes", () => new XlsxWriter().Write(workbook, stream));
        stream.Position = 0;
        Workbook? read = null;
        var load = Timed("XLSX read shapes", () => read = new XlsxReader().Read(stream).Value);

        Assert.True(write < 6_000, $"write {write} ms");
        Assert.True(load < 6_000, $"read {load} ms");
        var last = read!.Sheets[0].Objects.Single(placed => placed.Content.Description == "d11999");
        Assert.Equal("https://example.com/11999", last.Hyperlink);
    }

    [Fact]
    public void A_text_with_many_linked_text_boxes_reads_in_linear_time()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([.. Enumerable.Range(0, 6_000).Select(index => new ParagraphBlock("p" + index, "Paragraph " + index))]);
        for (var index = 0; index < 6_000; index++)
        {
            var size = new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2));
            var story = new BlockContainer([new ParagraphBlock("s" + index, "words " + index)]);
            document.Objects.Add(new PulseOffice.Documents.Objects.AnchoredObject("head" + index, new PulseOffice.Documents.Objects.ParagraphAnchor("p" + index),
                new PulseOffice.Documents.Objects.TextBoxContent(story) { AutoGrowHeight = false, NextFrameName = "Tail" + index }, size) { Name = "Head" + index });
            document.Objects.Add(new PulseOffice.Documents.Objects.AnchoredObject("tail" + index, new PulseOffice.Documents.Objects.ParagraphAnchor("p" + index),
                new PulseOffice.Documents.Objects.TextBoxContent(new BlockContainer([new ParagraphBlock("e" + index, string.Empty)])), size) { Name = "Tail" + index });
        }
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;

        TextDocument? read = null;
        var load = Timed("DOCX linked boxes read", () => read = _composer.Reader(DocxFlavour.Document).Read(stream).Value);

        Assert.True(load < 4_000, $"read {load} ms");
        Assert.Equal(6_000, read!.Objects.All.Count(item => item.Content is PulseOffice.Documents.Objects.TextBoxContent { NextFrameName: not null }));
    }

    [Fact]
    public void A_text_with_ten_thousand_formatted_paragraphs_writes_and_reads_in_linear_time()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([.. Enumerable.Range(0, 10_000).Select(index => new ParagraphBlock("p" + index,
            new PulseOffice.Text.Rich.TextParagraph(string.Empty).WithRuns([
                new PulseOffice.Text.Rich.TextRun("Plain " + index),
                new PulseOffice.Text.Rich.TextRun("bold") { Bold = true, FontSize = 8 + index % 500 / 10.0 },
                new PulseOffice.Text.Rich.TextRun("link") { Hyperlink = "https://example.com/" + index },
            ])))]);
        using var stream = new MemoryStream();

        var write = Timed("DOCX rich write", () => Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded));
        stream.Position = 0;
        TextDocument? read = null;
        var load = Timed("DOCX rich read", () => read = _composer.Reader(DocxFlavour.Document).Read(stream).Value);

        Assert.True(write < 30_000, $"write {write} ms");
        Assert.True(load < 4_000, $"read {load} ms");
        Assert.Equal(10_000, read!.Body.Blocks.Count);
    }

    [Fact]
    public void A_text_with_ten_thousand_paragraphs_writes_and_reads_in_linear_time()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([.. Enumerable.Range(0, 10_000).Select(index => new ParagraphBlock("p" + index, "Paragraph number " + index))]);
        using var stream = new MemoryStream();

        var write = Timed("DOCX write", () => Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded));
        stream.Position = 0;
        TextDocument? read = null;
        var load = Timed("DOCX read", () => read = _composer.Reader(DocxFlavour.Document).Read(stream).Value);

        Assert.True(write < 30_000, $"write {write} ms");
        Assert.True(load < 30_000, $"read {load} ms");
        Assert.Equal(10_000, read!.Body.Blocks.Count);
    }

    [Fact]
    public void A_deck_with_two_thousand_slides_writes_and_reads_in_linear_time()
    {
        var deck = Deck();
        for (var index = 0; index < 2_000; index++)
        {
            var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
            slide.Add(Shape("s" + index, index));
            deck.AddExisting(slide);
        }

        var (write, load, read) = RoundTrip(deck);

        Assert.True(write < 60_000, $"write {write} ms");
        Assert.True(load < 60_000, $"read {load} ms");
        Assert.Equal(2_000, read.Slides.Count);
    }

    [Fact]
    public void A_slide_with_twenty_thousand_shapes_reads_in_linear_time()
    {
        var deck = Deck();
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        for (var index = 0; index < 20_000; index++) slide.Add(Shape("s" + index, index));
        deck.AddExisting(slide);

        var (write, load, read) = RoundTrip(deck);

        Assert.True(write < 30_000, $"write {write} ms");
        Assert.True(load < 30_000, $"read {load} ms");
        Assert.Equal(20_000, read.Slides[0].Elements.OfType<ObjectElement>().Count());
    }

    [Fact]
    public void A_slide_with_thirty_thousand_linked_shapes_writes_and_reads_in_linear_time()
    {
        var deck = Deck();
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        for (var index = 0; index < 30_000; index++)
        {
            slide.Add(Shape("s" + index, index));
            slide.AddInteraction(new InteractiveAction { TargetId = "s" + index, Kind = InteractionKind.Hyperlink, Reference = "https://example.com/" + index });
        }
        deck.AddExisting(slide);

        var (write, load, read) = RoundTrip(deck);

        Assert.True(write < 2_500, $"write {write} ms");
        Assert.True(load < 6_000, $"read {load} ms");
        Assert.Equal(30_000, read.Slides[0].Interactions.Count(action => action.Kind == InteractionKind.Hyperlink));
        Assert.Contains(read.Slides[0].Interactions, action => action.Reference == "https://example.com/29999");
    }

    [Fact]
    public void A_group_of_twenty_thousand_shapes_reads_in_linear_time()
    {
        var deck = Deck();
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        for (var index = 0; index < 20_000; index++) slide.Add(Shape("s" + index, index));
        slide.AddGroup(new SlideGroup("g", [.. Enumerable.Range(0, 20_000).Select(index => "s" + index)]));
        deck.AddExisting(slide);

        var (write, load, read) = RoundTrip(deck);

        Assert.True(write < 30_000, $"write {write} ms");
        Assert.True(load < 4_000, $"read {load} ms");
        Assert.Equal(20_000, read.Slides[0].Elements.OfType<ObjectElement>().Count());
    }

    private Presentation Deck()
    {
        var theme = _themes.Find("Slate")!;
        return new Presentation { Theme = theme, Master = _masters.CreateStandard(theme), Size = SlideSize.Widescreen };
    }

    private static ObjectElement Shape(string id, int index) => new()
    {
        Id = id,
        Bounds = new DocumentRect(new Twip(100 + index % 500), new Twip(100), new Twip(1000), new Twip(500)),
        Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
    };

    private (long Write, long Read, Presentation Deck) RoundTrip(Presentation deck)
    {
        using var stream = new MemoryStream();
        var write = Timed("PPTX write", () => Assert.True(new PptxWriter().Write(deck, stream).Succeeded));
        stream.Position = 0;
        Presentation? read = null;
        var load = Timed("PPTX read", () => read = new PptxReader(_themes, _masters).Read(stream).Value);
        return (write, load, read!);
    }

    [Fact]
    public void A_whole_sheet_merge_with_a_whole_sheet_validation_saves_and_reads_quickly_in_xlsx()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("All");
        sheet.SetValue(CellAddress.First, CellValue.Text("everything"));
        var whole = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1));
        sheet.Merges.Add(whole);
        sheet.Validations.Add(new DataValidation { Range = whole, Kind = ValidationKind.List, ListValues = ["everything"] });
        using var stream = new MemoryStream();

        var write = Timed("XLSX write whole-sheet merge", () => new XlsxWriter().Write(workbook, stream));
        output.WriteLine($"XLSX package: {stream.Length} bytes");
        stream.Position = 0;
        Workbook? read = null;
        var load = Timed("XLSX read whole-sheet merge", () => read = new XlsxReader().Read(stream).Value);

        Assert.True(write < 10_000, $"write {write} ms");
        Assert.True(load < 10_000, $"read {load} ms");
        Assert.True(stream.Length < 100_000, $"package {stream.Length} bytes");
        Assert.Equal(whole, Assert.Single(read!.Sheets[0].Merges.All));
        Assert.Equal(whole, Assert.Single(read.Sheets[0].Validations).Range);
    }

    [Fact]
    public void A_whole_column_merge_and_whole_column_validation_cross_both_formats_quickly()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Columns");
        sheet.SetValue(CellAddress.First, CellValue.Text("top"));
        sheet.SetValue(new CellAddress(70_000, 5), CellValue.Number(7));
        var merge = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, 1));
        var validated = new CellRange(new CellAddress(0, 3), new CellAddress(CellAddress.MaxRows - 1, 3));
        sheet.Merges.Add(merge);
        sheet.Validations.Add(new DataValidation { Range = validated, Kind = ValidationKind.WholeNumber });

        var xlsx = new MemoryStream();
        var ods = new MemoryStream();
        var xlsxWrite = Timed("XLSX write whole columns", () => new XlsxWriter().Write(workbook, xlsx));
        var odsWrite = Timed("ODS write whole columns", () => new PulseOffice.Formats.Odf.OdsWriter().Write(workbook, ods));
        output.WriteLine($"XLSX {xlsx.Length} bytes, ODS {ods.Length} bytes");
        xlsx.Position = ods.Position = 0;
        var fromXlsx = new XlsxReader().Read(xlsx).Value!.Sheets[0];
        var fromOds = new PulseOffice.Formats.Odf.OdsReader().Read(ods).Value!.Sheets[0];

        Assert.True(xlsxWrite < 10_000 && odsWrite < 10_000, $"xlsx {xlsxWrite} ms, ods {odsWrite} ms");
        foreach (var read in new[] { fromXlsx, fromOds })
        {
            Assert.Equal(merge, Assert.Single(read.Merges.All));
            Assert.Equal(validated, Assert.Single(read.Validations).Range);
            Assert.Equal(7, read.GetValue(new CellAddress(70_000, 5)).AsNumber());
        }
    }
}
