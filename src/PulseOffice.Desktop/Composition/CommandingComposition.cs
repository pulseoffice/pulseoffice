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

internal static class CommandingComposition
{
    public static IServiceCollection AddCommanding(this IServiceCollection services, IUserDataPaths paths)
    {
        services.AddSingleton<LanguageCoordinator>();
        services.AddSingleton<IRecentDocuments>(provider => new RecentDocuments(
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<RecentDocuments>(), paths.Configuration("recent.json"),
            () => (int)Math.Clamp(provider.GetRequiredService<ISettingsService>().Get<long>(ShellSettingKeys.RecentDocuments), 0L, 100L)));

        services.AddSingleton<IWindowPlacementStore>(provider => new WindowPlacementStore(
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<WindowPlacementStore>(), paths.Configuration("window-pixels.json")));

        services.AddSingleton<IPlacementPolicy, PlacementPolicy>();
        services.AddSingleton<IPlacementPixelMapper, PlacementPixelMapper>();
        services.AddSingleton<WindowPlacementCoordinator>();
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<ICommandEffectChecks, CommandEffectChecks>();
        services.AddSingleton<PulseOffice.UI.Controls.Services.IDialogResultAdmission, PulseOffice.Shell.WorkspaceDialogAdmission>();
        services.AddSingleton<CommandExecutor>();
        services.AddSingleton<RibbonProjection>(provider => new RibbonProjection(
            provider.GetRequiredService<CommandRegistry>(), provider.GetRequiredService<IRibbonDisplayMap>()));
        services.AddSingleton<IRibbonDisplayMap>(_ => PulseRibbonLayout.Displays());
        services.AddSingleton<IRibbonPaletteBuilder, RibbonPaletteBuilder>();
        services.AddSingleton<IRibbonReductionPlanner, RibbonReductionPlanner>();
        services.AddSingleton<IRibbonStripLayout, RibbonStripLayout>();
        services.AddSingleton<IRibbonBandLayout, RibbonBandLayout>();
        services.AddSingleton<PulseOffice.UI.Controls.IColorDialog, PulseOffice.UI.Controls.ColorDialog>();
        services.AddSingleton<PulseOffice.Charts.IChartFamilyCatalog, PulseOffice.Charts.ChartFamilyCatalog>();
        services.AddSingleton<PulseOffice.Charts.IChartRecommender>(_ => new PulseOffice.Charts.ChartRecommender());
        services.AddSingleton(provider => new PulseOffice.UI.Controls.Charts.ChartRenderer(provider.GetRequiredService<ITextCatalog>()));
        services.AddSingleton<PulseOffice.UI.Controls.Charts.IChartSampleData, PulseOffice.UI.Controls.Charts.ChartSampleData>();
        services.AddSingleton<PulseOffice.UI.Controls.Charts.IChartPicker, PulseOffice.UI.Controls.Charts.ChartPicker>();
        services.AddSingleton<PulseOffice.UI.Controls.Charts.IEmbeddedChartPrompt, PulseOffice.UI.Controls.Charts.EmbeddedChartPrompt>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IPackagedFilePrompt, PulseOffice.UI.Controls.Objects.PackagedFilePrompt>();
        services.AddSingleton<PulseOffice.Drawing.Assets.IRasterMediaTypes, PulseOffice.Drawing.Assets.RasterMediaTypes>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IRasterImageOperations, PulseOffice.UI.Controls.Objects.RasterImageOperations>();
        services.TryAddSingleton<PulseOffice.Drawing.Assets.Filters.IPixelFilterCatalog, PulseOffice.Drawing.Assets.Filters.PixelFilterCatalog>();
        services.AddSingleton<PulseOffice.UI.Controls.Objects.IImageContourTracer, PulseOffice.UI.Controls.Objects.ImageContourTracer>();
        services.AddSingleton<PulseOffice.Charts.IChartSettingsMapper, PulseOffice.Charts.ChartSettingsMapper>();
        services.AddSingleton<PulseOffice.UI.Controls.Charts.IChartEditor, PulseOffice.UI.Controls.Charts.ChartEditor>();
        services.AddSingleton<PulseOffice.UI.Controls.Charts.IInlineChartData, PulseOffice.UI.Controls.Charts.InlineChartData>();
        services.AddSingleton<PulseOffice.Sheets.Engine.Operations.IChartSeriesNames, PulseOffice.Sheets.UI.LocalizedSeriesNames>();
        services.AddSingleton<PulseOffice.Sheets.Engine.Operations.IChartDataExtractor>(provider =>
            new PulseOffice.Sheets.Engine.Operations.ChartDataExtractor(provider.GetRequiredService<PulseOffice.Sheets.Engine.Operations.IChartSeriesNames>(),
                new PulseOffice.Sheets.Engine.Operations.PresentedChartCellText(provider.GetRequiredService<ICellPresenter>()),
                provider.GetRequiredService<PulseOffice.Sheets.Addressing.ISelectionWorkArea>()));
        services.AddSingleton<PulseOffice.UI.Controls.Services.IRibbonStateNotifier, PulseOffice.UI.Controls.Services.RibbonStateNotifier>();
        services.AddSingleton<PulseOffice.UI.Controls.Services.IRibbonMemory>(_ => new PulseOffice.UI.Controls.Services.RibbonMemory(paths.Configuration("ribbon.json")));
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.MenuSlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.ColorSplitSlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.FontFamilySlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.NumberBoxSlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.SplitGallerySlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.BandGallerySlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.ChoiceSlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.BorderGallerySlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.TableGridSlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory, PulseOffice.UI.Controls.Ribbon.CommandSlotFactory>();
        services.AddSingleton<PulseOffice.UI.Controls.Ribbon.IRibbonSlotCatalog>(provider =>
            new PulseOffice.UI.Controls.Ribbon.RibbonSlotCatalog(
                [.. provider.GetServices<PulseOffice.UI.Controls.Ribbon.IRibbonSlotFactory>()]));
        services.AddSingleton(provider => new PulseOffice.UI.Controls.Ribbon.RibbonServices(
            provider.GetRequiredService<IIconCatalog>(),
            provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            provider.GetService<PulseOffice.UI.Controls.Fonts.IFontPickerFactory>(),
            provider.GetRequiredService<IRibbonStripLayout>(),
            provider.GetRequiredService<IRibbonReductionPlanner>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.Ribbon.IRibbonSlotCatalog>(),
            [.. provider.GetServices<PulseOffice.UI.Controls.Ribbon.IRibbonThumbnailPainter>()],
            provider.GetService<PulseOffice.UI.Controls.Fonts.IDisplayFonts>(),
            provider.GetRequiredService<PulseOffice.UI.Controls.IFlyoutDismissal>()));
        services.AddSingleton<PulseOffice.UI.Controls.IFlyoutDismissal, PulseOffice.UI.Controls.FlyoutDismissal>();
        services.AddSingleton<IMenuPlanner>(_ => new MenuPlanner());
        services.AddSingleton<CommandSurfaceFactory>();
        services.AddSingleton<IContextMenuFactory>(s => s.GetRequiredService<CommandSurfaceFactory>());
        services.TryAddSingleton<IContextMenuKey, ContextMenuKey>();
        services.AddSingleton<ShellCommands>();
        services.AddSingleton<ShellInterfaceCommands>();
        services.AddSingleton<ShellPrintCommands>();
        services.AddSingleton<ShellDocumentCommands>();
        services.AddSingleton<ShellPasswordCommands>();
        services.AddSingleton<ShellSendCommands>();
        services.AddSingleton<ShellRemoteCommands>();
        services.AddSingleton<ShellSignatureCommands>();
        services.AddSingleton<ShellFormCommands>();
        services.AddSingleton<ShellMacroRecorderCommands>();
        services.AddSingleton<PulseOffice.Shell.IScriptableCommandPolicy, PulseOffice.Shell.ScriptableCommandPolicy>();
        services.AddSingleton<PulseOffice.Shell.ICommandRecorder, PulseOffice.Shell.CommandRecorder>();
        services.AddSingleton<PulseOffice.Shell.IMacroScriptWriter, PulseOffice.Shell.JavaScriptMacroWriter>();
        services.AddSingleton<PulseOffice.Scripting.Editor.Facades.IScriptCommands, ScriptCommands>();
        services.AddSingleton<ShellTemplateCommands>();
        services.AddSingleton<PulseOffice.Shell.IPrintPreferences, PulseOffice.Shell.PrintPreferences>();
        services.AddSingleton<PulseOffice.Shell.IDocumentOperationsCatalog, PulseOffice.Shell.DocumentOperationsCatalog>();
        services.AddSingleton<PulseOffice.Shell.IDocumentOperations, LedgerDocumentOperations>();
        services.AddSingleton<PulseOffice.Shell.IDocumentOperations, PulseOffice.Presentations.UI.DeckDocumentOperations>();
        services.AddSingleton<PulseOffice.Presentations.UI.IDeckPublisher, PulseOffice.Presentations.UI.DeckPublisher>();
        services.AddSingleton<IDocumentWorkspace, DocumentWorkspace>();

        return services;
    }
}
