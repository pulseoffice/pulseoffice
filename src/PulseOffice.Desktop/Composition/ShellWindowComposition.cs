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

internal static class ShellWindowComposition
{
    public static IServiceCollection AddShellWindow(this IServiceCollection services, IUserDataPaths paths)
    {
        services.AddSingleton<PulseOffice.Sheets.IWorkbookCopier, PulseOffice.Sheets.WorkbookCopier>();
        services.AddSingleton<IDocumentSnapshotProvider, SpreadsheetSnapshotProvider>();
        services.AddSingleton<IDocumentSnapshotProvider, PulseOffice.Presentations.UI.DeckSnapshotProvider>();
        services.AddSingleton<IDocumentRecovery>(provider => new DocumentRecovery(
            provider.GetServices<IDocumentSnapshotProvider>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<DocumentRecovery>(),
            paths.RecoveryDirectory, provider.GetRequiredService<PulseOffice.Shell.IDocumentPasswordGate>(),
            provider.GetRequiredService<PulseOffice.Platform.IPrivateFiles>()));
        services.AddSingleton<AutosaveCoordinator>();
        services.AddSingleton<IInterfaceFaultGuard, InterfaceFaultGuard>();
        services.AddSingleton<RecoveryCoordinator>();
        services.AddSingleton<QuitCoordinator>();

        services.AddSingleton<SidePanelView>(provider => new SidePanelView(
            provider.GetRequiredService<ITextCatalog>()));
        services.AddSingleton<ISidePanelHost>(provider => provider.GetRequiredService<SidePanelView>());
        services.AddSingleton<IRecentCommands>(provider => new RecentCommands(
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<RecentCommands>(), paths.Configuration("recent-commands.json")));
        services.AddSingleton<ICommandPaletteSource, CommandPaletteSource>();
        services.AddSingleton<ITemplateLibrary, TemplateLibrary>();
        services.AddSingleton<IStatusZoomSource>(provider => new StatusZoomSource(
            provider.GetRequiredService<PulseOffice.UI.Commands.CommandRegistry>(),
            new Dictionary<PulseOffice.Foundation.Modules.ModuleKind, string>
            {
                [PulseOffice.Foundation.Modules.ModuleKind.Sheets] = "ledger.view.zoomTo",
                [PulseOffice.Foundation.Modules.ModuleKind.Documents] = "folio.zoom.set",
                [PulseOffice.Foundation.Modules.ModuleKind.Presentations] = "deck.zoom.set",
            }));
        services.AddSingleton<PulseOffice.UI.Controls.Navigation.IWindowRegionCycle, PulseOffice.UI.Controls.Navigation.WindowRegionCycle>();
        services.AddSingleton<ShellWindow>();
        services.AddSingleton<Func<IShellPresenter>>(provider => provider.GetRequiredService<ShellWindow>);
        services.AddSingleton<Func<PulseOffice.Shell.CommandExecutor>>(
            provider => provider.GetRequiredService<PulseOffice.Shell.CommandExecutor>);
        services.AddSingleton<IShortcutDispatcher, ShortcutDispatcher>();
        services.AddSingleton<PulseOffice.UI.Commands.Shortcuts.IGestureNotation>(
            provider => PulseOffice.UI.Commands.Shortcuts.GestureNotation.ForSystem(provider.GetRequiredService<PulseOffice.Platform.IHostSystem>().IsMacOS));
        services.AddSingleton<PulseOffice.UI.Commands.Shortcuts.IReservedShortcuts>(
            provider => PulseOffice.UI.Commands.Shortcuts.ReservedShortcutTable.ForSystem(provider.GetRequiredService<PulseOffice.Platform.IHostSystem>().IsMacOS));
        services.AddSingleton<IShortcutCapture>(provider => new ShortcutCapture(provider.GetRequiredService<PulseOffice.Platform.IHostSystem>().IsMacOS));
        services.AddSingleton<PulseOffice.UI.Commands.Shortcuts.IShortcutCatalog, PulseOffice.UI.Commands.Shortcuts.ShortcutCatalog>();
        services.AddSingleton<PulseOffice.UI.Commands.Shortcuts.IShortcutCategories, PulseOffice.UI.Commands.Shortcuts.ShortcutCategories>();
        services.AddSingleton<PulseOffice.UI.Commands.Shortcuts.IShortcutSearch, PulseOffice.UI.Commands.Shortcuts.ShortcutSearch>();
        services.AddSingleton<PulseOffice.UI.Commands.Shortcuts.IShortcutDraftFactory, PulseOffice.UI.Commands.Shortcuts.ShortcutDraftFactory>();
        services.AddSingleton<PulseOffice.UI.Commands.Shortcuts.IShortcutBindingStore>(_ => new ShortcutBindingStore(paths.Configuration("shortcuts.json")));
        services.AddSingleton<PulseOffice.UI.Commands.Shortcuts.IShortcutCustomization, PulseOffice.UI.Commands.Shortcuts.ShortcutCustomization>();
        services.AddSingleton<ShortcutEditorServices>();
        services.AddSingleton<PulseOffice.UI.Controls.Text.ITextEditingRouter, PulseOffice.UI.Controls.Text.TextEditingRouter>();
        services.AddSingleton<PulseOffice.UI.Controls.Services.IDocumentKeyScope, PulseOffice.UI.Controls.Services.DocumentKeyScope>();

        return services;
    }
}
