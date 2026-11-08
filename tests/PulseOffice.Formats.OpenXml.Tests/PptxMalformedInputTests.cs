using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxMalformedInputTests
{
    private static readonly string[] Garbage = ["", "abc", "-99999999999999999999", "1e309", "-5"];

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private byte[] RichPackage()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation
        {
            Theme = theme, Master = master,
            HeaderFooter = new HeaderFooterSettings { ShowDate = true, ShowFooter = true, FooterText = "Piede", ShowSlideNumber = true },
        };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        slide.Notes = "Note";
        slide.Transition = new SlideTransition { Kind = TransitionKind.Push, Direction = TransitionDirection.Up, DurationMs = 700 };
        slide.Add(new ObjectElement
        {
            Id = "s1",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(3000), new Twip(2000)),
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Ellipse },
        });
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = "s1", DurationMs = 600, DelayMs = 100, Repeat = 2, Category = AnimationCategory.MotionPath, Preset = "line", MotionPath = "M 0 0 L 0.25 0.1 E" });
        slide.AddInteraction(new InteractiveAction { TargetId = "s1", Kind = InteractionKind.NextSlide });
        slide.AddComment(new SlideComment { Id = "c1", Author = "Ada", Text = "Testo" });
        deck.AddExisting(slide);

        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private byte[] Replace(byte[] package, string entryName, XDocument document)
    {
        using var source = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, CompressionLevel.NoCompression);
                using var to = copy.Open();
                if (entry.FullName == entryName) document.Save(to);
                else
                {
                    using var from = entry.Open();
                    from.CopyTo(to);
                }
            }
        }

        return output.ToArray();
    }

    [Theory]
    [InlineData("ppt/slides/slide1.xml")]
    [InlineData("ppt/presentation.xml")]
    [InlineData("ppt/slideMasters/slideMaster1.xml")]
    [InlineData("ppt/slideLayouts/slideLayout1.xml")]
    [InlineData("ppt/theme/theme1.xml")]
    [InlineData("ppt/notesSlides/notesSlide1.xml")]
    [InlineData("ppt/comments/comment1.xml")]
    public void GarbageAttributesNeverCrashTheReader(string entryName)
    {
        var package = RichPackage();
        XDocument original;
        using (var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read))
        using (var stream = (archive.GetEntry(entryName) ?? throw new InvalidOperationException(string.Join(",", archive.Entries.Select(e => e.FullName)))).Open())
            original = XDocument.Load(stream);

        var count = original.Descendants().SelectMany(e => e.Attributes()).Count(a => !a.IsNamespaceDeclaration);
        var failures = new List<string>();
        var reader = new PptxReader(_themes, _masters);

        for (var index = 0; index < count; index++)
            foreach (var value in Garbage)
            {
                var document = new XDocument(original);
                var attribute = document.Descendants().SelectMany(e => e.Attributes()).Where(a => !a.IsNamespaceDeclaration).ElementAt(index);
                attribute.Value = value;

                using var stream = new MemoryStream(Replace(package, entryName, document));
                var exception = Record.Exception(() =>
                {
                    var result = reader.Read(stream);
                    if (!result.Succeeded) throw new InvalidDataException(string.Join("; ", result.Diagnostics.Select(d => d.Message)));
                });
                if (exception is not null) failures.Add($"{attribute.Name}={value}: {exception.GetType().Name} {exception.Message} {exception.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("PulseOffice"))}");
            }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Distinct()));
    }
    [Theory]
    [InlineData("ppt/slides/slide1.xml")]
    [InlineData("ppt/presentation.xml")]
    [InlineData("ppt/slideMasters/slideMaster1.xml")]
    [InlineData("ppt/slideLayouts/slideLayout1.xml")]
    [InlineData("ppt/theme/theme1.xml")]
    [InlineData("ppt/notesSlides/notesSlide1.xml")]
    [InlineData("ppt/comments/comment1.xml")]
    public void MissingElementsNeverCrashTheReader(string entryName)
    {
        var package = RichPackage();
        XDocument original;
        using (var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read))
        using (var stream = archive.GetEntry(entryName)!.Open())
            original = XDocument.Load(stream);

        var count = original.Descendants().Count();
        var failures = new List<string>();
        var reader = new PptxReader(_themes, _masters);

        for (var index = 1; index < count; index++)
        {
            var document = new XDocument(original);
            var element = document.Descendants().ElementAt(index);
            var name = element.Name;
            element.Remove();

            using var stream = new MemoryStream(Replace(package, entryName, document));
            var exception = Record.Exception(() =>
            {
                var result = reader.Read(stream);
                if (!result.Succeeded && name.LocalName != "presentation") throw new InvalidDataException(string.Join("; ", result.Diagnostics.Select(d => d.Message)));
            });
            if (exception is not null) failures.Add($"{name}: {exception.GetType().Name} {exception.Message} {exception.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains("PulseOffice"))}");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Distinct()));
    }
}
