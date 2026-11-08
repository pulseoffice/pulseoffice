using PulseOffice.Formats.Intake;
using PulseOffice.Documents;
using PulseOffice.Foundation.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PulseOffice.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using PulseOffice.Sheets;
using PulseOffice.Settings;
using PulseOffice.Foundation.Windowing;
using PulseOffice.Shell;
using PulseOffice.Shell.Surfaces;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Formats.Pdf;
using PulseOffice.Printing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Document.Forms;
using PulseOffice.Security;
using PulseOffice.Text.Scripts;
using PulseOffice.Text.Spelling;
using PulseOffice.Text.Spelling.Hunspell;
using PulseOffice.Sheets.UI;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shortcuts;
using PulseOffice.UI.Theming;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.Text.Rich;
using Avalonia;
using PulseOffice.Formats.Interchange.Text;

namespace PulseOffice.Desktop.Composition;

internal static class DocumentFormatsComposition
{
    public static IServiceCollection AddDocumentFormats(this IServiceCollection services)
    {
        services.AddDocumentIntake();
        services.AddSingleton<ISpreadsheetReader, OdsSpreadsheetReader>();
        services.AddSingleton<PulseOffice.Formats.ISvgSanitizer, PulseOffice.Formats.SvgSanitizer>();
        services.AddSingleton<PulseOffice.Drawing.Assets.IVectorRasterizer, PulseOffice.Formats.Rasterizing.SkiaVectorRasterizer>();
        services.AddSingleton(provider => new PulseOffice.Formats.SignatureLineImage(provider.GetRequiredService<PulseOffice.Drawing.Assets.IVectorRasterizer>()));
        services.AddSingleton(provider => new PulseOffice.Formats.VectorFallback(provider.GetRequiredService<PulseOffice.Drawing.Assets.IVectorRasterizer>()));
        services.AddSingleton<ISpreadsheetWriter>(provider => new OdsSpreadsheetWriter(false, provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>(), provider.GetRequiredService<PulseOffice.Formats.VectorFallback>()));
        services.AddSingleton<ISpreadsheetWriter>(provider => new OdsSpreadsheetWriter(true, provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>(), provider.GetRequiredService<PulseOffice.Formats.VectorFallback>()));
        services.AddSingleton<ISpreadsheetWriter>(provider => new FlatOdsSpreadsheetWriter(new OdsSpreadsheetWriter(false, provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>(), provider.GetRequiredService<PulseOffice.Formats.VectorFallback>())));
        services.AddSingleton<ISpreadsheetReader, XlsxSpreadsheetReader>();
        services.AddSingleton<ISpreadsheetReader>(_ => new XlsxSpreadsheetReader(XlsxFormat.Template));
        services.AddSingleton<ISpreadsheetWriter>(provider => new XlsxSpreadsheetWriter(provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>(), provider.GetRequiredService<PulseOffice.Formats.VectorFallback>(), false));
        services.AddSingleton<ISpreadsheetWriter>(provider => new XlsxSpreadsheetWriter(provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>(), provider.GetRequiredService<PulseOffice.Formats.VectorFallback>(), true));
        services.AddSingleton<ISpreadsheetReader>(provider =>
            new PulseOffice.Formats.Interchange.CsvSpreadsheetReader(
                provider.GetRequiredService<PulseOffice.Foundation.Text.IDelimitedText>(),
                new PulseOffice.Formats.Interchange.CsvValueReader(),
                new PulseOffice.Formats.Interchange.CsvSniffer(
                    provider.GetRequiredService<PulseOffice.Foundation.Text.IDelimitedText>())));
        services.AddSingleton<ISpreadsheetWriter>(provider =>
            new PulseOffice.Formats.Interchange.CsvSpreadsheetWriter(
                provider.GetRequiredService<PulseOffice.Foundation.Text.IDelimitedText>()));
        services.AddSingleton<ISpreadsheetFormatRegistry, SpreadsheetFormatRegistry>();

        services.AddSingleton<PulseOffice.UI.Controls.Services.IFileTypeNames, PulseOffice.UI.Controls.Services.FileTypeNames>();
        services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();
        services.AddSingleton<IFileSystemLinks, FileSystemLinks>();
        services.AddSingleton<IConfinedFileWriter, ConfinedFileWriter>();
        services.AddSingleton<ISaveNameCorrection, SaveNameCorrection>();
        services.AddSingleton<PulseOffice.UI.Controls.Printing.IPageImageCapture, PulseOffice.UI.Controls.Printing.PageImageCapture>();
        services.AddSingleton<IDocumentFormatFamily, DocumentFormatFamily>();
        services.AddSingleton<IExecutableLocator, PathExecutableLocator>();
        services.AddSingleton<IFileDialogService>(s => new PulseOffice.UI.Controls.Services.ReportingFileDialogService(
            new PulseOffice.UI.Controls.Services.LocalizedFileDialogService(
                new AvaloniaFileDialogService(() => s.GetRequiredService<ShellWindow>(), s.GetRequiredService<ISaveNameCorrection>(),
                    () => s.GetRequiredService<IDocumentWorkspace>().Active?.Path is { } path ? System.IO.Path.GetDirectoryName(path) : null),
                s.GetRequiredService<PulseOffice.UI.Controls.Services.IFileTypeNames>()),
            s.GetRequiredService<PulseOffice.UI.Controls.Services.IStatusBarService>(),
            s.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PulseOffice.UI.Controls.Services.ReportingFileDialogService>>()));
        services.AddSingleton<PulseOffice.UI.Controls.Services.IInterfaceLock, PulseOffice.UI.Controls.Services.InterfaceLock>();
        services.AddSingleton<PulseOffice.Shell.DocumentServiceContext>();
        services.AddSingleton<SpreadsheetModuleFormats>();
        services.AddSingleton<PulseOffice.Presentations.UI.DeckModuleFormats>();
        services.AddSingleton<PulseOffice.Formats.Interchange.Text.Plain.ICodePages, PulseOffice.Formats.Interchange.Text.Plain.CodePages>();
        services.AddSingleton<PulseOffice.Sheets.UI.Dialogs.ICsvImportPrompt, PulseOffice.Sheets.UI.Dialogs.CsvImportPrompt>();
        services.AddSingleton<PulseOffice.Sheets.UI.Dialogs.ICsvUnattendedOptions, PulseOffice.Sheets.UI.Dialogs.CsvUnattendedOptions>();
        services.AddSingleton<ISpreadsheetDocumentService, SpreadsheetDocumentService>();

        services.AddSingleton<PulseOffice.Formats.IPresentationReader, PulseOffice.Formats.Odf.OdpReader>();
        services.AddSingleton<PulseOffice.Formats.IPresentationWriter>(provider => new PulseOffice.Formats.Odf.OdpWriter(false, provider.GetRequiredService<PulseOffice.Drawing.Assets.IVectorRasterizer>()));
        services.AddSingleton<PulseOffice.Formats.IPresentationWriter>(provider => new PulseOffice.Formats.Odf.OdpWriter(true, provider.GetRequiredService<PulseOffice.Drawing.Assets.IVectorRasterizer>()));
        services.AddSingleton<PulseOffice.Formats.IPresentationWriter>(provider => new PulseOffice.Formats.Odf.FlatOdpWriter(new PulseOffice.Formats.Odf.OdpWriter(false, provider.GetRequiredService<PulseOffice.Drawing.Assets.IVectorRasterizer>())));
        services.AddSingleton<PulseOffice.Formats.IPresentationReader, PulseOffice.Formats.OpenXml.PptxReader>();
        services.AddSingleton<PulseOffice.Formats.IPresentationWriter>(provider => new PulseOffice.Formats.OpenXml.PptxWriter(provider.GetRequiredService<PulseOffice.Drawing.Assets.IVectorRasterizer>()));
        services.AddSingleton<PulseOffice.Formats.IPresentationReader>(provider => new PulseOffice.Formats.OpenXml.PptxReader(
            provider.GetRequiredService<PulseOffice.Presentations.IThemeCatalog>(), provider.GetRequiredService<PulseOffice.Presentations.IMasterFactory>(),
            PulseOffice.Formats.OpenXml.PptxFlavour.Template));
        services.AddSingleton<PulseOffice.Formats.IPresentationWriter>(provider => new PulseOffice.Formats.OpenXml.PptxWriter(PulseOffice.Formats.OpenXml.PptxFlavour.Template, provider.GetRequiredService<PulseOffice.Drawing.Assets.IVectorRasterizer>()));
        services.AddSingleton<PulseOffice.Formats.IPresentationFormatRegistry, PulseOffice.Formats.PresentationFormatRegistry>();
        services.AddSingleton<PulseOffice.Formats.ITextDocumentReader, PulseOffice.Formats.Odf.Odt.OdtReader>();
        services.AddSingleton<PulseOffice.Formats.ITextDocumentWriter>(provider => new PulseOffice.Formats.Odf.Odt.OdtWriter(null, provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>()));
        services.AddSingleton<PulseOffice.Formats.ITextDocumentWriter>(provider => new PulseOffice.Formats.Odf.Odt.OdtWriter(PulseOffice.Formats.Odf.Odt.OdtFlavour.Template, provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>()));
        services.AddSingleton<PulseOffice.Formats.ITextDocumentWriter>(provider => new PulseOffice.Formats.Odf.Odt.OdtWriter(PulseOffice.Formats.Odf.Odt.OdtFlavour.Master, provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>()));
        services.AddSingleton<PulseOffice.Formats.ITextDocumentWriter>(provider => new PulseOffice.Formats.Odf.Odt.FlatOdtWriter(new PulseOffice.Formats.Odf.Odt.OdtWriter(null, provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>())));
        services.AddSingleton(provider => new PulseOffice.Formats.OpenXml.Wordprocessing.DocxComposer(
            new PulseOffice.Documents.Styles.DirectFormatting(), provider.GetRequiredService<PulseOffice.Documents.Styles.IDefaultStyleSheetFactory>(),
            provider.GetRequiredService<PulseOffice.Formats.SignatureLineImage>(),
            provider.GetRequiredService<PulseOffice.Formats.VectorFallback>()));
        foreach (var flavour in new[] { PulseOffice.Formats.OpenXml.Wordprocessing.DocxFlavour.Document, PulseOffice.Formats.OpenXml.Wordprocessing.DocxFlavour.Template, PulseOffice.Formats.OpenXml.Wordprocessing.DocxFlavour.MacroEnabled })
        {
            services.AddSingleton<PulseOffice.Formats.ITextDocumentReader>(provider => provider.GetRequiredService<PulseOffice.Formats.OpenXml.Wordprocessing.DocxComposer>().Reader(flavour));
            services.AddSingleton<PulseOffice.Formats.ITextDocumentWriter>(provider => provider.GetRequiredService<PulseOffice.Formats.OpenXml.Wordprocessing.DocxComposer>().Writer(flavour));
        }
        services.AddTextInterchange();
        services.AddSingleton<PulseOffice.Formats.ITextDocumentFormatRegistry, PulseOffice.Formats.TextDocumentFormatRegistry>();

        return services;
    }
}
