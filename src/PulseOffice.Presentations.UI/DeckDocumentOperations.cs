using PulseOffice.Foundation.Modules;
using PulseOffice.Printing;
using PulseOffice.Shell;

namespace PulseOffice.Presentations.UI;

public sealed class DeckDocumentOperations(
    IDeckPublisher publisher,
    PulseOffice.Formats.IPresentationFormatRegistry formats,
    IActiveDeckContext context,
    PulseOffice.Platform.IAtomicFileWriter atomic,
    PulseOffice.Localization.ITextCatalog texts,
    PulseOffice.Document.Versions.IVersionSequenceComparer comparer) : IDocumentOperations
{
    public PulseOffice.Document.Versions.VersionComparison? CompareVersion(OpenDocument document, string path)
    {
        if (document?.Model is not Presentation current) return null;
        using var stream = File.OpenRead(path);
        if (formats.Read(path, stream).Value is not { } saved) return null;
        return comparer.Compare(Items(saved), Items(current));
    }

    private IReadOnlyList<PulseOffice.Document.Versions.VersionItem> Items(Presentation deck) =>
        [.. deck.Slides.Select((slide, index) => new PulseOffice.Document.Versions.VersionItem(
            texts.Format("versions.compare.slide", index + 1),
            string.Join("\n", slide.Elements.OfType<TextElement>().OrderBy(element => element.ZOrder)
                .SelectMany(element => element.Text.Paragraphs)
                .Select(paragraph => paragraph.Runs.Count > 0 ? string.Concat(paragraph.Runs.Select(run => run.Text)) : paragraph.Text)
                .Where(line => line.Length > 0))))];

    public PdfExportProfile PdfExport(OpenDocument document)
    {
        if (document.Model is not Presentation { Slides.Count: > 0 } deck)
            return new(HasHeadings: true, LayoutLabel: string.Empty, Layouts: []);

        PdfExportLayout Layout(string label, DeckExportLayout layout) => new(label, () => publisher.BuildExportable(deck, layout));

        return new(HasHeadings: true, LayoutLabel: texts["deck.export.layout"], Layouts:
        [
            Layout(texts["deck.export.layout.slides"], DeckExportLayout.Slides),
            Layout(texts["deck.export.layout.notes"], DeckExportLayout.Notes),
            Layout(texts.Format("deck.export.layout.handoutMaster", deck.Handout.SlidesPerPage), DeckExportLayout.Handout),
            Layout(texts["deck.export.layout.handout2"], DeckExportLayout.Handout2),
            Layout(texts["deck.export.layout.handout3"], DeckExportLayout.Handout3),
            Layout(texts["deck.export.layout.handout4"], DeckExportLayout.Handout4),
            Layout(texts["deck.export.layout.handout6"], DeckExportLayout.Handout6),
            Layout(texts["deck.export.layout.handout9"], DeckExportLayout.Handout9),
        ]);
    }

    public bool InsertImage(OpenDocument document, byte[] data, string name) =>
        context.Target is { } target && ReferenceEquals(target.Deck, document.Model) && target.InsertImageBytes(data, name);

    public ModuleKind Module => ModuleKind.Presentations;

    public string SuggestedName(OpenDocument document) => System.IO.Path.GetFileNameWithoutExtension(document.Title);

    public IPrintableDocument? PreviewFile(string path)
    {
        using var stream = File.OpenRead(path);
        return formats.Read(path, stream).Value is { Slides.Count: > 0 } deck ? publisher.BuildPrintable(deck, DeckExportLayout.Slides) : null;
    }

    public IPrintableDocument? BuildPrintable(OpenDocument document) =>
        document.Model is Presentation { Slides.Count: > 0 } deck
            ? publisher.BuildPrintable(deck, DeckExportLayout.Slides)
            : null;

    public IPrintableDocument? BuildPages(OpenDocument document) =>
        document.Model is Presentation { Slides.Count: > 0 } deck ? publisher.BuildEverySlide(deck) : null;

    public IPrintableDocument? BuildExportable(OpenDocument document, bool comments) =>
        document.Model is Presentation { Slides.Count: > 0 } deck
            ? publisher.BuildExportable(deck, DeckExportLayout.Slides)
            : null;

    public PulseOffice.Document.Metadata.DocumentProperties? Properties(OpenDocument document) =>
        (document.Model as Presentation)?.Properties;

    public string NativeExtension => "odp";

    public string OfficeExtension => "pptx";

    public bool WriteTo(OpenDocument document, string path)
    {
        if (document.Model is not Presentation deck || formats.WriterFor(path) is not { } writer) return false;

        return atomic.Replace(path, stream => writer.Write(deck, stream).Succeeded, written => written);
    }

    public string? RenderHtml(OpenDocument document) =>
        document.Model is Presentation deck ? publisher.BuildHtml(deck) : null;

    public PulseOffice.Document.Versions.DocumentVersionCollection? Versions(OpenDocument document) =>
        (document.Model as Presentation)?.Versions;

    public string TemplateExtension => "otp";

    public string? OfficeTemplateExtension => "potx";

}
