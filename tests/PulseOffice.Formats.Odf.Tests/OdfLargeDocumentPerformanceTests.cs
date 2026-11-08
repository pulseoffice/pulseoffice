using System.Diagnostics;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

[CollectionDefinition(nameof(OdfLargeDocumentPerformanceTests), DisableParallelization = true)]
[Collection(nameof(OdfLargeDocumentPerformanceTests))]
public sealed class OdfLargeDocumentPerformanceTests(ITestOutputHelper output)
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

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

        var write = Timed("ODS write", () => new OdsWriter().Write(workbook, stream));
        stream.Position = 0;
        Workbook? read = null;
        var load = Timed("ODS read", () => read = new OdsReader().Read(stream).Value);

        Assert.True(write < 30_000, $"write {write} ms");
        Assert.True(load < 30_000, $"read {load} ms");
        Assert.Equal("r99999", read!.Sheets[0].GetValue(new CellAddress(99_999, 1)).AsText());
    }

    [Fact]
    public void A_wide_sheet_with_ten_thousand_columns_reads_in_linear_time()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Wide");
        for (var row = 0; row < 40; row++)
            for (var column = 0; column < 10_000; column++)
                sheet.SetValue(new CellAddress(row, column), CellValue.Number(column));
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        Workbook? read = null;
        var load = Timed("ODS wide read", () => read = new OdsReader().Read(stream).Value);

        Assert.True(load < 4_000, $"read {load} ms");
        Assert.Equal(9_999, read!.Sheets[0].GetValue(new CellAddress(39, 9_999)).AsNumber());
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

        var write = Timed("ODS write " + kind, () => new OdsWriter().Write(workbook, stream));
        stream.Position = 0;
        Workbook? read = null;
        var load = Timed("ODS read " + kind, () => read = new OdsReader().Read(stream).Value);

        Assert.True(write < 8_000, $"write {write} ms");
        Assert.True(load < 4_000, $"read {load} ms");
        Assert.Equal(kind == "merge" ? 40_000 : 0, read!.Sheets[0].Merges.Count);
        Assert.Equal(kind == "comment" ? 40_000 : 0, read.Sheets[0].Comments.Count);
        Assert.Equal(kind == "link" ? "https://example.com/39999" : null, read.Sheets[0][new CellAddress(79_998, 0)].Hyperlink);
    }

    [Fact]
    public void A_sheet_with_every_column_styled_writes_in_linear_time()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        var style = workbook.Styles.Intern(new PulseOffice.Sheets.Styling.CellStyle { BackgroundHex = "#FFEE00" });
        sheet.Layout.SetColumnStyles(0, CellAddress.MaxColumns - 1, style);
        for (var row = 0; row < 120_000; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        using var stream = new MemoryStream();

        var write = Timed("ODS write styled columns", () => new OdsWriter().Write(workbook, stream));
        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!;

        Assert.True(write < 4_000, $"write {write} ms");
        Assert.Equal(119_999, read.Sheets[0].GetValue(new CellAddress(119_999, 0)).AsNumber());
        Assert.NotNull(read.Sheets[0].Layout.ColumnStyle(CellAddress.MaxColumns - 1));
    }

    [Fact]
    public void A_sheet_with_many_linked_and_described_shapes_writes_in_linear_time()
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

        var write = Timed("ODS write shapes", () => new OdsWriter().Write(workbook, stream));
        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!;

        Assert.True(write < 6_000, $"write {write} ms");
        var last = read.Sheets[0].Objects.Single(placed => placed.Id == "shape5999");
        Assert.Equal("https://example.com/5999", last.Hyperlink);
        Assert.Equal("d5999", last.Content.Description);
    }

    [Fact]
    public void A_text_with_ten_thousand_formatted_paragraphs_writes_and_reads_in_linear_time()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([.. Enumerable.Range(0, 10_000).Select(index => new ParagraphBlock("p" + index,
            new PulseOffice.Text.Rich.TextParagraph(string.Empty).WithRuns([
                new PulseOffice.Text.Rich.TextRun("Plain " + index),
                new PulseOffice.Text.Rich.TextRun("bold") { Bold = true, FontSize = 8 + index % 500 / 10.0 },
            ])))]);
        using var stream = new MemoryStream();

        var write = Timed("ODT rich write", () => Assert.True(new OdtWriter().Write(document, stream).Succeeded));
        stream.Position = 0;
        TextDocument? read = null;
        var load = Timed("ODT rich read", () => read = new OdtReader().Read(stream).Value);

        Assert.True(write < 30_000, $"write {write} ms");
        Assert.True(load < 30_000, $"read {load} ms");
        Assert.Equal(10_000, read!.Body.Blocks.Count);
    }

    [Fact]
    public void A_text_with_ten_thousand_paragraphs_writes_and_reads_in_linear_time()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([.. Enumerable.Range(0, 10_000).Select(index => new ParagraphBlock("p" + index, "Paragraph number " + index))]);
        using var stream = new MemoryStream();

        var write = Timed("ODT write", () => Assert.True(new OdtWriter().Write(document, stream).Succeeded));
        stream.Position = 0;
        TextDocument? read = null;
        var load = Timed("ODT read", () => read = new OdtReader().Read(stream).Value);

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
    public void A_slide_with_twenty_thousand_shapes_without_z_index_reads_in_linear_time()
    {
        var deck = Deck();
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        for (var index = 0; index < 20_000; index++) slide.Add(Shape("s" + index, index));
        deck.AddExisting(slide);

        var (write, load, read) = RoundTrip(deck);

        Assert.True(write < 30_000, $"write {write} ms");
        Assert.True(load < 4_000, $"read {load} ms");
        Assert.Equal(20_000, read.Slides[0].Elements.OfType<ObjectElement>().Count());
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

    [Fact]
    public void A_frame_with_twelve_thousand_animated_paragraphs_reads_in_linear_time()
    {
        var deck = Deck();
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        slide.Add(new TextElement
        {
            Id = "box",
            Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(10), Twip.FromCentimeters(4)),
            Text = new PulseOffice.Text.Rich.RichText(PulseOffice.Text.Rich.PlaceholderRole.Free, [.. Enumerable.Range(0, 12_000).Select(index => new PulseOffice.Text.Rich.TextParagraph("line " + index))]),
        });
        for (var index = 0; index < 12_000; index++)
            slide.AddAnimation(new AnimationEffect { Id = "a" + index, TargetId = "box", Paragraph = index, Trigger = AnimationTrigger.WithPrevious });
        deck.AddExisting(slide);

        var (_, load, read) = RoundTrip(deck);

        Assert.True(load < 2_000, $"read {load} ms");
        Assert.Equal(11_999, read.Slides[0].Animations.Max(effect => effect.Paragraph));
    }

    [Fact]
    public void Ten_thousand_unnamed_pages_read_in_linear_time()
    {
        var deck = Deck();
        for (var index = 0; index < 10_000; index++) deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        using var written = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, written).Succeeded);
        written.Position = 0;
        using var unnamed = new MemoryStream();
        using (var source = new System.IO.Compression.ZipArchive(written, System.IO.Compression.ZipArchiveMode.Read))
        using (var target = new System.IO.Compression.ZipArchive(unnamed, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, entry.FullName == "mimetype" ? System.IO.Compression.CompressionLevel.NoCompression : System.IO.Compression.CompressionLevel.Fastest);
                using var input = entry.Open();
                using var output = copy.Open();
                if (entry.FullName != "content.xml")
                {
                    input.CopyTo(output);
                    continue;
                }
                var content = System.Xml.Linq.XDocument.Load(input);
                foreach (var page in content.Descendants(OdfPackage.Namespaces.Draw + "page")) page.Attribute(OdfPackage.Namespaces.Draw + "name")?.Remove();
                content.Save(output);
            }
        unnamed.Position = 0;

        Presentation? read = null;
        var load = Timed("ODP unnamed read", () => read = new OdpReader(_themes, _masters).Read(unnamed).Value);

        Assert.True(load < 1_000, $"read {load} ms");
        Assert.Equal(10_000, read!.Slides.Select(slide => slide.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("slide (10000)", read.Slides[^1].Id);
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
        var write = Timed("ODP write", () => Assert.True(new OdpWriter().Write(deck, stream).Succeeded));
        stream.Position = 0;
        Presentation? read = null;
        var load = Timed("ODP read", () => read = new OdpReader(_themes, _masters).Read(stream).Value);
        return (write, load, read!);
    }

    [Fact]
    public void A_whole_sheet_merge_with_a_whole_sheet_validation_saves_and_reads_quickly()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("All");
        sheet.SetValue(CellAddress.First, CellValue.Text("everything"));
        var whole = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1));
        sheet.Merges.Add(whole);
        sheet.Validations.Add(new DataValidation { Range = whole, Kind = ValidationKind.List, ListValues = ["everything"] });
        using var stream = new MemoryStream();

        var write = Timed("ODS write whole-sheet merge", () => new OdsWriter().Write(workbook, stream));
        output.WriteLine($"ODS package: {stream.Length} bytes");
        stream.Position = 0;
        Workbook? read = null;
        var load = Timed("ODS read whole-sheet merge", () => read = new OdsReader().Read(stream).Value);

        Assert.True(write < 10_000, $"write {write} ms");
        Assert.True(load < 10_000, $"read {load} ms");
        Assert.True(stream.Length < 100_000, $"package {stream.Length} bytes");
        Assert.Equal(whole, Assert.Single(read!.Sheets[0].Merges.All));
        Assert.Equal(whole, Assert.Single(read.Sheets[0].Validations).Range);
        Assert.Equal("everything", read.Sheets[0].GetValue(CellAddress.First).AsText());
    }

    [Fact]
    public void A_validation_across_all_columns_of_a_thousand_used_rows_saves_quickly()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Wide");
        for (var row = 0; row < 1_000; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        var range = new CellRange(CellAddress.First, new CellAddress(999, CellAddress.MaxColumns - 1));
        sheet.Validations.Add(new DataValidation { Range = range, Kind = ValidationKind.WholeNumber });
        using var stream = new MemoryStream();

        var write = Timed("ODS write wide validation", () => new OdsWriter().Write(workbook, stream));
        output.WriteLine($"ODS package: {stream.Length} bytes");
        stream.Position = 0;
        Workbook? read = null;
        var load = Timed("ODS read wide validation", () => read = new OdsReader().Read(stream).Value);

        Assert.True(write < 10_000, $"write {write} ms");
        Assert.True(load < 10_000, $"read {load} ms");
        Assert.Equal(range, Assert.Single(read!.Sheets[0].Validations).Range);
        Assert.Equal(999, read.Sheets[0].GetValue(new CellAddress(999, 0)).AsNumber());
    }
}
