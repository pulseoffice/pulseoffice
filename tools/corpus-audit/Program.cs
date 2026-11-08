using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Formats.Rasterizing;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Tools.CorpusAudit;

var services = new ServiceCollection();
services.AddSingleton<ISvgSanitizer, SvgSanitizer>();
services.AddSingleton<IVectorRasterizer, SkiaVectorRasterizer>();
services.AddSingleton(provider => new SignatureLineImage(provider.GetRequiredService<IVectorRasterizer>()));
services.AddSingleton(provider => new VectorFallback(provider.GetRequiredService<IVectorRasterizer>()));
services.AddSingleton<IThemeCatalog, ThemeCatalog>();
services.AddSingleton<IMasterFactory>(_ => new MasterFactory());
services.AddSingleton(provider => new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory(),
    provider.GetRequiredService<SignatureLineImage>(), provider.GetRequiredService<VectorFallback>()));
services.AddSingleton<IAuditFamily>(provider => new AuditFamily<Workbook, SpreadsheetFormat>(
    new Dictionary<string, IDocumentReader<Workbook, SpreadsheetFormat>> { [".ods"] = new OdsSpreadsheetReader(), [".xlsx"] = new XlsxSpreadsheetReader() },
    new Dictionary<string, IDocumentWriter<Workbook, SpreadsheetFormat>>
    {
        [".ods"] = new OdsSpreadsheetWriter(false, provider.GetRequiredService<SignatureLineImage>(), provider.GetRequiredService<VectorFallback>()),
        [".xlsx"] = new XlsxSpreadsheetWriter(provider.GetRequiredService<SignatureLineImage>(), provider.GetRequiredService<VectorFallback>(), false),
    }));
services.AddSingleton<IAuditFamily>(provider =>
{
    var composer = provider.GetRequiredService<DocxComposer>();
    return new AuditFamily<TextDocument, TextDocumentFormat>(
        new Dictionary<string, IDocumentReader<TextDocument, TextDocumentFormat>> { [".odt"] = new OdtReader(), [".docx"] = composer.Reader(DocxFlavour.Document) },
        new Dictionary<string, IDocumentWriter<TextDocument, TextDocumentFormat>>
        {
            [".odt"] = new OdtWriter(null, provider.GetRequiredService<SignatureLineImage>()),
            [".docx"] = composer.Writer(DocxFlavour.Document),
        });
});
services.AddSingleton<IAuditFamily>(provider =>
{
    var themes = provider.GetRequiredService<IThemeCatalog>();
    var masters = provider.GetRequiredService<IMasterFactory>();
    var rasterizer = provider.GetRequiredService<IVectorRasterizer>();
    return new AuditFamily<Presentation, PresentationFormat>(
        new Dictionary<string, IDocumentReader<Presentation, PresentationFormat>> { [".odp"] = new OdpReader(themes, masters), [".pptx"] = new PptxReader(themes, masters) },
        new Dictionary<string, IDocumentWriter<Presentation, PresentationFormat>> { [".odp"] = new OdpWriter(false, rasterizer), [".pptx"] = new PptxWriter(rasterizer) });
});
services.AddSingleton<NamespaceKeys>();
services.AddSingleton<IFeatureCatalog, FeatureCatalog>();
services.AddSingleton<IRuleContextFactory, RuleContextFactory>();
services.AddSingleton<IFeatureCounter, FeatureCounter>();
services.AddSingleton<ICopyKeeper>(_ => new CopyKeeper(Environment.GetEnvironmentVariable("CORPUS_AUDIT_KEEP")));
services.AddSingleton<IDeclarationTable>(_ => new DeclarationTable(Path.Combine(AppContext.BaseDirectory, "declared-features.json")));
services.AddSingleton<IDeclarationMatcher, DeclarationMatcher>();
services.AddSingleton<IFileAuditor, FileAuditor>();
services.AddSingleton<IWorkerLauncher, WorkerLauncher>();
services.AddSingleton<ISummaryWriter, SummaryWriter>();
services.AddSingleton<IAuditCommand, ListCommand>();
services.AddSingleton<IAuditCommand, OneCommand>();
services.AddSingleton<IAuditCommand, RunCommand>();

using var provider = services.BuildServiceProvider();
var command = args.Length > 0 ? provider.GetServices<IAuditCommand>().FirstOrDefault(candidate => candidate.Name == args[0]) : null;
if (command is null)
{
    Console.Error.WriteLine("usage: corpus-audit list <root> <corpus.txt> | run <root> <corpus.txt> <results.tsv> <summary.md> [jobs] [timeout-seconds] | one <root> <file>");
    return 2;
}

return command.Run(args[1..]);
