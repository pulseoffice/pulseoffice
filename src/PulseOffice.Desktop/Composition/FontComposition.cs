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

internal static class FontComposition
{
    public static IServiceCollection AddFonts(this IServiceCollection services, IUserDataPaths paths)
    {
        var fonts = paths.Configuration("fonts");

        services.AddSingleton(_ => new PulseOffice.Typography.Sources.FileFontServiceOptionsStore(
            paths.Configuration("fonts.json")).Load());
        services.AddSingleton<ISystemFontDirectories>(provider => new SystemFontDirectories(provider.GetRequiredService<IEnvironmentVariables>(), provider.GetRequiredService<IHostSystem>().Family));
        services.AddSingleton<PulseOffice.Typography.OpenType.OpenTypeReader>();
        services.AddSingleton<PulseOffice.Typography.OpenType.IOpenTypeReader>(provider => provider.GetRequiredService<PulseOffice.Typography.OpenType.OpenTypeReader>());
        services.AddSingleton<PulseOffice.Typography.OpenType.IFontFileExtractor, PulseOffice.Typography.OpenType.FontFileExtractor>();
        services.AddSingleton<PulseOffice.Typography.Licensing.IFontLicenseClassifier, PulseOffice.Typography.Licensing.FontLicenseClassifier>();
        services.AddSingleton<PulseOffice.Typography.Catalog.IFontCategoryClassifier, PulseOffice.Typography.Catalog.FontCategoryClassifier>();
        services.AddSingleton<PulseOffice.Typography.Catalog.IFontScriptCoverage, PulseOffice.Typography.Catalog.FontScriptCoverage>();
        services.AddSingleton<PulseOffice.Typography.Catalog.IClassicFaceSelector, PulseOffice.Typography.Catalog.ClassicFaceSelector>();
        services.AddSingleton<PulseOffice.Typography.Sources.GoogleFontsMetadataParser>();
        services.AddSingleton<PulseOffice.Typography.Sources.IInstalledFontNames, PulseOffice.UI.Controls.Fonts.AvaloniaInstalledFontNames>();
        services.AddSingleton<PulseOffice.Typography.Sources.IFontCacheStore>(_ => new PulseOffice.Typography.Sources.FileFontCacheStore(fonts));
        services.AddSingleton<PulseOffice.Typography.Catalog.IRecentFonts>(_ => new PulseOffice.Typography.Catalog.FileRecentFonts(
            paths.Configuration("recent-fonts.json")));
        services.AddSingleton<PulseOffice.Typography.Sources.SystemFontSource>();
        services.AddSingleton<PulseOffice.Typography.Sources.GoogleFontsSource>();
        services.AddSingleton<PulseOffice.Typography.Sources.FontsourceSource>();
        services.AddSingleton<PulseOffice.Typography.Sources.IActiveFontDocument, ActiveFontDocument>();
        services.AddSingleton<PulseOffice.Typography.Sources.DocumentFontSource>();
        services.AddSingleton<PulseOffice.Typography.Catalog.IFontCatalog>(provider =>
        {
            PulseOffice.Typography.Sources.IFontSource Cached(PulseOffice.Typography.Sources.IFontSource inner) =>
                new PulseOffice.Typography.Sources.CachedFontSource(inner,
                    provider.GetRequiredService<PulseOffice.Typography.Sources.IFontCacheStore>(),
                    provider.GetRequiredService<PulseOffice.Typography.Sources.FontServiceOptions>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<PulseOffice.Typography.OpenType.IOpenTypeReader>());

            return new PulseOffice.Typography.Catalog.CompositeFontCatalog(
            [
                provider.GetRequiredService<PulseOffice.Typography.Sources.DocumentFontSource>(),
                provider.GetRequiredService<PulseOffice.Typography.Sources.SystemFontSource>(),
                Cached(provider.GetRequiredService<PulseOffice.Typography.Sources.GoogleFontsSource>()),
                Cached(provider.GetRequiredService<PulseOffice.Typography.Sources.FontsourceSource>()),
            ]);
        });
        services.AddSingleton<IWorkbookFontNames, WorkbookFontNames>();
        services.AddSingleton<PulseOffice.Presentations.IPresentationFontNames, PulseOffice.Presentations.PresentationFontNames>();
        services.AddSingleton<PulseOffice.Drawing.Text.IObjectFontNames, PulseOffice.Drawing.Text.ObjectFontNames>();
        services.AddSingleton<PulseOffice.Typography.Embedding.IUsedFontCollector, PulseOffice.Sheets.UI.Fonts.LedgerFontCollector>();
        services.AddSingleton<PulseOffice.Typography.Embedding.IUsedFontCollector, PulseOffice.Presentations.UI.Fonts.DeckFontCollector>();
        services.AddSingleton<PulseOffice.Typography.Embedding.IUsedFonts, PulseOffice.Typography.Embedding.UsedFonts>();
        services.AddSingleton<PulseOffice.Typography.Embedding.IDocumentFontEmbedder, PulseOffice.Typography.Embedding.DocumentFontEmbedder>();
        services.AddSingleton<PulseOffice.UI.Controls.Fonts.IRuntimeFonts, PulseOffice.UI.Controls.Fonts.RuntimeFonts>();
        services.AddSingleton<PulseOffice.UI.Controls.Fonts.IFontActivation, PulseOffice.UI.Controls.Fonts.FontActivation>();
        services.AddSingleton<PulseOffice.UI.Controls.Fonts.FontDescriptions>();
        services.TryAddSingleton<PulseOffice.UI.Controls.Fonts.IFontSubstitutes, PulseOffice.UI.Controls.Fonts.MetricCompatibleSubstitutes>();
        services.AddSingleton<PulseOffice.UI.Controls.Fonts.IFontAvailability, PulseOffice.UI.Controls.Fonts.AvaloniaFontAvailability>();
        services.AddSingleton<PulseOffice.UI.Controls.Fonts.IDisplayFonts, PulseOffice.UI.Controls.Fonts.DisplayFonts>();
        services.AddSingleton<PulseOffice.UI.Controls.Printing.IPageImageBitmaps, PulseOffice.UI.Controls.Printing.PageImageBitmaps>();
        services.AddSingleton<PulseOffice.UI.Controls.Printing.IPageCanvasFactory, PulseOffice.UI.Controls.Printing.AvaloniaPageCanvasFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Fonts.FontPickerServices>();
        services.AddSingleton<PulseOffice.UI.Controls.Fonts.IFontPickerFactory, PulseOffice.UI.Controls.Fonts.FontPickerFactory>();
        services.AddSingleton<ShellFontCommands>();
        services.AddSingleton<DocumentFontRegistrar>();

        return services;
    }
}
