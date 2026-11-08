using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Formats.Interchange.Text.Outline;
using PulseOffice.Formats.Interchange.Text.Plain;
using PulseOffice.Formats.Interchange.Text.Rtf;

namespace PulseOffice.Formats.Interchange.Text;

public static class TextInterchangeServiceCollectionExtensions
{
    public static IServiceCollection AddTextInterchange(this IServiceCollection services)
    {
        services.TryAddSingleton<PulseOffice.Documents.Blocks.IBlockTree, PulseOffice.Documents.Blocks.BlockTree>();
        services.TryAddSingleton<PulseOffice.Documents.Editing.IStoryCatalog, PulseOffice.Documents.Editing.StoryCatalog>();
        services.TryAddSingleton<PulseOffice.Documents.Editing.IListThreads, PulseOffice.Documents.Editing.ListThreads>();
        services.TryAddSingleton<ImageHeader>();
        services.TryAddSingleton<ICodePages, CodePages>();
        services.TryAddSingleton<ITextEncodingDetector, TextEncodingDetector>();
        services.TryAddSingleton<IParagraphSemantics, ParagraphSemantics>();
        services.TryAddSingleton<IHtmlTokenizer, HtmlTokenizer>();
        services.TryAddSingleton<ICssDeclarations, CssDeclarations>();
        services.TryAddSingleton<IHtmlCellBorders, HtmlCellBorders>();
        services.TryAddSingleton<IHtmlTableReader, HtmlTableReader>();
        services.TryAddSingleton<IRtfTokenizer, RtfTokenizer>();
        services.TryAddSingleton<IRtfVocabulary, RtfVocabulary>();
        services.AddSingleton<ITextDocumentReader, RtfDocumentReader>();
        services.AddSingleton<ITextDocumentWriter, RtfDocumentWriter>();
        services.TryAddSingleton<HtmlDocumentReader>();
        services.TryAddSingleton<IHtmlPageRenderer, HtmlPageRenderer>();
        services.TryAddSingleton<IHtmlFidelity, HtmlFidelity>();
        services.AddSingleton<ITextDocumentReader>(provider => provider.GetRequiredService<HtmlDocumentReader>());
        services.AddSingleton<ITextDocumentReader, XhtmlDocumentReader>();
        services.AddSingleton<ITextDocumentWriter, HtmlDocumentWriter>();
        services.AddSingleton<ITextDocumentWriter, XhtmlDocumentWriter>();
        services.TryAddSingleton<PulseOffice.Formats.Interchange.Text.Epub.IEpubPackageWriter, PulseOffice.Formats.Interchange.Text.Epub.EpubPackageWriter>();
        services.AddSingleton<ITextDocumentWriter, PulseOffice.Formats.Interchange.Text.Epub.EpubDocumentWriter>();
        services.AddSingleton<ITextDocumentReader, PlainTextReader>();
        services.AddSingleton<ITextDocumentWriter>(provider => new PlainTextWriter(provider.GetRequiredService<IParagraphSemantics>()));
        return services;
    }
}
