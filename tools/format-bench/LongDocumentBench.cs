using System.Diagnostics;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

public sealed class LongDocumentBench
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();
    private long _baseline;
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    public void Run(string mode, string format, string path, int size)
    {
        var clock = Stopwatch.StartNew();
        if (format is "odp" or "pptx")
        {
            IPresentationWriter writer = format == "odp" ? new OdpWriter() : new PptxWriter();
            IPresentationReader reader = format == "odp" ? new OdpReader(_themes, _masters) : new PptxReader(_themes, _masters);
            if (mode == "write")
            {
                var deck = Deck(size);
                _baseline = GC.GetTotalAllocatedBytes(true);
                clock.Restart();
                using var stream = File.Create(path);
                writer.Write(deck, stream).Require();
                Report($"write {format} slides={size}", clock, path);
            }
            else
            {
                using var stream = File.OpenRead(path);
                var deck = reader.Read(stream).Require();
                Report($"read {format} slides={deck.Slides.Count}", clock, path);
            }
            return;
        }
        ITextDocumentWriter textWriter = format == "odt" ? new OdtWriter() : _composer.Writer(DocxFlavour.Document);
        ITextDocumentReader textReader = format == "odt" ? new OdtReader() : _composer.Reader(DocxFlavour.Document);
        if (mode == "write")
        {
            var document = Document(size);
            _baseline = GC.GetTotalAllocatedBytes(true);
            clock.Restart();
            using var stream = File.Create(path);
            textWriter.Write(document, stream).Require();
            Report($"write {format} paragraphs={size}", clock, path);
        }
        else
        {
            using var stream = File.OpenRead(path);
            var document = textReader.Read(stream).Require();
            Report($"read {format} blocks={document.Body.Count}", clock, path);
        }
    }

    private Presentation Deck(int slides)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        for (var index = 0; index < slides; index++)
        {
            var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
            slide.Notes = $"Note {index}";
            slide.Add(new TextElement
            {
                Id = $"t{index}",
                Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(4), Twip.FromCentimeters(20), Twip.FromCentimeters(8)),
                Text = new RichText(PlaceholderRole.Free, [.. Enumerable.Range(0, 6).Select(line => new TextParagraph($"Diapositiva {index}, punto {line}: testo di prova abbastanza lungo"))]),
            });
            slide.Add(new ObjectElement
            {
                Id = $"s{index}",
                Bounds = new DocumentRect(Twip.FromCentimeters(1), Twip.FromCentimeters(1), Twip.FromCentimeters(3), Twip.FromCentimeters(2)),
                Content = new ShapeObject { Id = $"s{index}", Shape = ShapeKind.Ellipse },
            });
            deck.AddExisting(slide);
        }
        return deck;
    }

    private TextDocument Document(int paragraphs)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var formatting = new DirectFormatting();
        for (var index = 0; index < paragraphs; index++)
        {
            var bold = formatting.Set(new TextRun($"Paragrafo {index}. "), new CharacterProperties { Bold = true });
            var plain = new TextRun("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.");
            document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Standard }.WithRuns([bold, plain])));
        }
        return document;
    }

    private void Report(string label, Stopwatch clock, string path)
    {
        var process = Process.GetCurrentProcess();
        process.Refresh();
        Console.WriteLine($"{label} time={clock.Elapsed.TotalSeconds:F2}s peak={process.PeakWorkingSet64 / 1048576}MB allocated={(GC.GetTotalAllocatedBytes(true) - _baseline) / 1024}KB file={new FileInfo(path).Length / 1024}KB");
    }
}
