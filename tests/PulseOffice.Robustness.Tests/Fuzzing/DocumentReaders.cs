using PulseOffice.Drawing.Assets;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats;
using PulseOffice.Formats.Intake;
using PulseOffice.Formats.Interchange;
using PulseOffice.Formats.Interchange.Text;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Text;
using PulseOffice.Presentations;

namespace PulseOffice.Robustness.Tests.Fuzzing;

public enum DocumentFamily
{
    Spreadsheet,
    Text,
    Presentation,
}

public sealed class DocumentReaders : IDisposable
{
    private readonly ServiceProvider _provider;

    public DocumentReaders()
    {
        var services = new ServiceCollection();
        services.AddDocumentsModel();
        services.AddSingleton<IncomingDocumentLimits>();
        services.AddSingleton<IIncomingDocumentInspector, IncomingDocumentInspector>();
        services.AddSingleton<IDocumentIntake, DocumentIntake>();
        services.AddSingleton<IDelimitedText, DelimitedText>();
        services.AddSingleton<IThemeCatalog, ThemeCatalog>();
        services.AddSingleton<IMasterFactory>(_ => new MasterFactory());
        services.AddSingleton<ISvgSanitizer, SvgSanitizer>();
        services.AddSingleton<IVectorRasterizer, PulseOffice.Formats.Rasterizing.SkiaVectorRasterizer>();
        services.AddSingleton(provider => new SignatureLineImage(provider.GetRequiredService<IVectorRasterizer>()));

        services.AddSingleton<ISpreadsheetReader, OdsSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetReader, XlsxSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetReader>(provider => new CsvSpreadsheetReader(
            provider.GetRequiredService<IDelimitedText>(), new CsvValueReader(), new CsvSniffer(provider.GetRequiredService<IDelimitedText>())));
        services.AddSingleton<ISpreadsheetWriter>(provider => new OdsSpreadsheetWriter(false, provider.GetRequiredService<SignatureLineImage>()));
        services.AddSingleton<ISpreadsheetFormatRegistry, SpreadsheetFormatRegistry>();

        services.AddSingleton<IPresentationReader, OdpReader>();
        services.AddSingleton<IPresentationReader, PptxReader>();
        services.AddSingleton<IPresentationWriter, OdpWriter>();
        services.AddSingleton<IPresentationFormatRegistry, PresentationFormatRegistry>();

        services.AddSingleton<ITextDocumentReader, OdtReader>();
        services.AddSingleton<ITextDocumentWriter>(provider => new OdtWriter(null, provider.GetRequiredService<SignatureLineImage>()));
        services.AddSingleton(provider => new DocxComposer(new DirectFormatting(), provider.GetRequiredService<IDefaultStyleSheetFactory>(), provider.GetRequiredService<SignatureLineImage>()));
        foreach (var flavour in new[] { DocxFlavour.Document, DocxFlavour.Template, DocxFlavour.MacroEnabled })
            services.AddSingleton<ITextDocumentReader>(provider => provider.GetRequiredService<DocxComposer>().Reader(flavour));
        services.AddTextInterchange();
        services.AddSingleton<ITextDocumentFormatRegistry, TextDocumentFormatRegistry>();
        services.AddSingleton<ITextDocumentInterchange, TextDocumentInterchange>();

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public IReadOnlyList<Diagnostic> Read(DocumentFamily family, string path, Stream stream) => family switch
    {
        DocumentFamily.Spreadsheet => _provider.GetRequiredService<ISpreadsheetFormatRegistry>().Read(path, stream).Diagnostics,
        DocumentFamily.Text => _provider.GetRequiredService<ITextDocumentFormatRegistry>().Read(path, stream).Diagnostics,
        _ => _provider.GetRequiredService<IPresentationFormatRegistry>().Read(path, stream).Diagnostics,
    };

    public ITextDocumentInterchange Interchange => _provider.GetRequiredService<ITextDocumentInterchange>();

    public void Dispose() => _provider.Dispose();
}
