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

namespace PulseOffice.Desktop.Composition;

internal static class PrintingAndCloudComposition
{
    public static IServiceCollection AddPrintingAndCloud(this IServiceCollection services, IUserDataPaths paths)
    {
        services.AddSingleton<IPdfFontMetrics, PdfFontMetrics>();
        services.AddSingleton(provider => new PulseOffice.Typography.OpenType.Metrics.IndexedFontLocator(
            provider.GetRequiredService<ISystemFontDirectories>(), new PulseOffice.Typography.OpenType.OpenTypeReader(),
            provider.GetRequiredService<PulseOffice.Typography.OpenType.Metrics.IFontMetricsReader>()));
        services.AddSingleton<PulseOffice.Formats.Pdf.Fonts.IPdfFaceLocator>(provider => new PulseOffice.Formats.Pdf.Fonts.PdfFaceLocator(
            provider.GetRequiredService<PulseOffice.Typography.OpenType.Metrics.IFontFileLocator>(),
            provider.GetRequiredService<PulseOffice.Typography.OpenType.Metrics.IndexedFontLocator>()));
        services.AddSingleton<PulseOffice.Formats.Pdf.Fonts.IPdfFallbackFonts, PulseOffice.Formats.Pdf.Fonts.PdfFallbackFonts>();
        services.AddSingleton<PulseOffice.Typography.Subsetting.ITrueTypeSubsetter, PulseOffice.Typography.Subsetting.TrueTypeSubsetter>();
        services.AddSingleton<PulseOffice.Formats.Pdf.Fonts.IToUnicodeWriter, PulseOffice.Formats.Pdf.Fonts.ToUnicodeWriter>();
        services.TryAddSingleton<PulseOffice.Typography.Shaping.TextShaperOptions>();
        services.TryAddSingleton<PulseOffice.Typography.Shaping.HarfBuzzTextShaper>();
        services.TryAddSingleton<PulseOffice.Typography.Shaping.ITextShaper>(provider => new PulseOffice.Typography.Shaping.CachedTextShaper(provider.GetRequiredService<PulseOffice.Typography.Shaping.HarfBuzzTextShaper>()));
        services.TryAddSingleton<PulseOffice.Typography.Shaping.IComplexScriptSegmenter, PulseOffice.Typography.Shaping.ComplexScriptSegmenter>();
        services.AddSingleton<PulseOffice.Formats.Pdf.Fonts.IPdfFontSetFactory, PulseOffice.Formats.Pdf.Fonts.PdfFontSetFactory>();
        services.AddSingleton<IColorProfile, SrgbProfile>();
        services.AddSingleton<IPdfArchiveParts, PdfArchiveParts>();
        services.AddSingleton<IPdfNavigationWriter, PdfNavigationWriter>();
        services.AddSingleton<PulseOffice.Formats.Imaging.IJpegEncoder, PulseOffice.Formats.Rasterizing.SkiaJpegEncoder>();
        services.AddSingleton<IImageResampler, BoxImageResampler>();
        services.AddSingleton<IJpegInspector, JpegInspector>();
        services.AddSingleton<IPdfImageWriter, PdfImageWriter>();
        services.AddSingleton<IPdfPasswordHash, Revision6PasswordHash>();
        services.AddSingleton<IPdfEncryptionFactory, Aes256EncryptionFactory>();
        services.AddSingleton<IPdfExporter, PdfExporter>();
        services.AddSingleton<PulseOffice.Platform.IVideoEncoderProvider, VideoEncoderProvider>();
        services.AddSingleton<IPaperCatalog, PaperCatalog>();
        services.AddSingleton<IHeaderFooterFields>(_ =>
            new HeaderFooterFields(System.Globalization.CultureInfo.CurrentCulture));
        services.AddSingleton<IPrintServiceProvider, PrintServiceProvider>();
        services.AddSingleton<IPageRangeParser, PageRangeParser>();
        services.AddSingleton<IPrintImposition, PrintImposition>();
        services.AddSingleton<PulseOffice.UI.Controls.Printing.IPdfExportPrompt, PulseOffice.UI.Controls.Printing.PdfExportPrompt>();
        services.TryAddSingleton<PulseOffice.Drawing.Printing.IObjectPathBuilder>(_ => new PulseOffice.Drawing.Printing.ObjectPathBuilder());
        services.TryAddSingleton<PulseOffice.Drawing.Printing.IOriginalImageSources, PulseOffice.Drawing.Printing.OriginalImageSources>();
        services.AddSingleton<PulseOffice.Sheets.Printing.ISheetPrintOptionsFactory, PulseOffice.Sheets.Printing.SheetPrintOptionsFactory>();
        services.AddSingleton<ISheetPrintDocumentBuilder, SheetPrintDocumentBuilder>();
        services.AddSingleton<LedgerPrintCommands>();
        services.AddSingleton<PulseOffice.UI.Controls.Printing.IHeaderFooterTokens, PulseOffice.UI.Controls.Printing.HeaderFooterTokens>();
        services.AddSingleton<Sheets.UI.PageLayout.SheetPageSetupServices>();
        services.AddSingleton<IWorkbookComparer, WorkbookComparer>();
        services.TryAddSingleton<PulseOffice.Document.Versions.IVersionTextDiff, PulseOffice.Document.Versions.VersionTextDiff>();
        services.TryAddSingleton<PulseOffice.Document.Versions.IVersionSequenceComparer, PulseOffice.Document.Versions.VersionSequenceComparer>();
        services.AddSingleton<IWorkbookMerger, WorkbookMerger>();
        services.AddSingleton<LedgerCompareCommands>();
        services.AddSingleton<WebDavResponseReader>();
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        services.AddSingleton<IRemoteStorage, WebDavStorage>();
        services.AddSingleton<ISyncedFolderLocator, SyncedFolderLocator>();
        services.AddSingleton<IProofKeys, ProofKeys>();
        services.AddSingleton<ILoopbackListener, LoopbackListener>();
        services.AddSingleton<IOAuthFlow, OAuthFlow>();
        services.AddSingleton<ICloudApplications>(_ => new CloudApplications(
            paths.Configuration("cloud.json")));
        services.AddSingleton<CloudSessions>();
        services.AddSingleton<ICloudSessions>(provider => provider.GetRequiredService<CloudSessions>());
        services.AddSingleton<IRemoteSignIn>(provider => provider.GetRequiredService<CloudSessions>());
        services.AddSingleton<OneDriveReader>();
        services.AddSingleton<DropboxReader>();
        services.AddSingleton<GoogleDriveReader>();
        services.AddSingleton<IRemoteStorage, OneDriveStorage>();
        services.AddSingleton<IRemoteStorage, DropboxStorage>();
        services.AddSingleton<IRemoteStorage, GoogleDriveStorage>();
        services.AddSingleton<IRemoteStorage, ICloudDriveStorage>();
        services.AddSingleton<IRemoteStorageProvider, RemoteStorageProvider>();
        services.AddSingleton<IRemoteDocuments, RemoteDocuments>();
        services.AddSingleton<IRemoteCredentials, RemoteCredentials>();
        services.AddSingleton<IRemoteFileNames, RemoteFileNames>();
        services.AddSingleton<PulseOffice.Remote.UI.IRemoteConflictPrompt, PulseOffice.Remote.UI.DialogRemoteConflictPrompt>();
        services.AddSingleton<PulseOffice.Remote.UI.IRemoteUploads, PulseOffice.Remote.UI.RemoteUploads>();
        services.AddSingleton<IRemoteConnectionDirectory>(_ => new FileRemoteConnectionDirectory(
            paths.Configuration("remote.json")));

        return services;
    }
}
