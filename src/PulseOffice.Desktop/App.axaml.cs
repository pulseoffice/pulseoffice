using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;
using PulseOffice.Sheets.UI;
using PulseOffice.Localization;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Theming;

namespace PulseOffice.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;

    private bool _closing;

    public ILoggerFactory? LoggerFactory { get; set; }

    public PulseOffice.UI.Controls.Fonts.FontFamilyMappingTable FontMappings { get; set; } = new();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            Compose();
        }
        catch (Exception ex)
        {
            PulseOffice.Diagnostics.LogConfiguration.WriteCrash(
                ex, "App.OnFrameworkInitializationCompleted", terminating: true);
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Compose()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = ServiceConfiguration.Build(
                this, LoggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, FontMappings);

            ApplyThemeFromSettings(_services);

            _ = _services.GetRequiredService<LanguageCoordinator>();
            _ = _services.GetRequiredService<PulseOffice.UI.Controls.Services.IAutoCorrectPreferences>();

            var registry = new ShellComposition(_services).Compose();
            _services.GetRequiredService<PulseOffice.Shell.SignatureStatusIndicator>().Start();
            _services.GetRequiredService<PulseOffice.Shell.FormDesignModeOnOpen>().Start();
            _ = _services.GetRequiredService<DocumentFontRegistrar>();
            _services.GetRequiredService<PulseOffice.Sheets.UI.ExternalData.ExternalLinkAutoRefresh>().Start();
            _ = _services.GetRequiredService<PulseOffice.UI.Controls.Fonts.IFontActivation>().WarmUpAsync();

            var customized = _services.GetRequiredService<PulseOffice.UI.Commands.Shortcuts.IShortcutCustomization>().ApplySaved();
            if (customized > 0)
                _services.GetRequiredService<ILoggerFactory>().CreateLogger<App>().LogInformation("Custom shortcuts applied: {Count}", customized);

            ReportMissingTexts(registry);

            var recent = new RecentDocumentsCoordinator(
                _services.GetRequiredService<IRecentDocuments>(),
                registry,
                path => _services.GetRequiredService<PulseOffice.Shell.IDocumentOpenRouter>().OpenAsync(path));

            var window = _services.GetRequiredService<ShellWindow>();

            recent.CommandsChanged += (_, _) => { if (!_closing) window.RefreshSurfaces(); };
            registry.BindingsChanged += (_, _) => { if (!_closing) window.RefreshSurfaces(); };
            _services.GetRequiredService<PulseOffice.Shell.CommandExecutor>().Executed += (_, _) =>
            {
                if (_closing) return;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!_closing) window.RefreshSurfaces();
                });
            };

            _services.GetRequiredService<PulseOffice.Presentations.UI.IActiveDeckContext>().Changed +=
                (_, _) =>
                {
                    if (_closing) return;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (!_closing) window.RefreshSurfaces();
                    });
                };

            window.UpdateForContext(new CommandContext(
                ModuleKind.Shell, SelectionKind.None, DocumentState.None));

            var logger = _services.GetRequiredService<ILoggerFactory>().CreateLogger<App>();

            _ = _services.GetRequiredService<AutosaveCoordinator>();
            _services.GetRequiredService<PulseOffice.Agent.UI.IAgentStatusIndicator>().Start();
            _services.GetRequiredService<PulseOffice.Agent.Transport.IAgentServerController>().Start();
            _services.GetRequiredService<IInterfaceFaultGuard>().Attach(Avalonia.Threading.Dispatcher.UIThread);
            var recovery = _services.GetRequiredService<RecoveryCoordinator>();

            logger.LogInformation("Registered commands: {Count}", registry.All.Count);
            var arrivals = _services.GetRequiredService<PulseOffice.Shell.IDocumentArrivals>();
            arrivals.AcceptDrops(window);
            _services.GetRequiredService<PulseOffice.Shell.IExchangeDragDrop>().AcceptDrops(window);
            arrivals.ExpectStartup(desktop.Args ?? []);
            if (this.TryGetFeature<IActivatableLifetime>() is { } activation)
                activation.Activated += async (_, args) =>
                {
                    if (args is FileActivatedEventArgs files)
                        await arrivals.OpenAsync(files.Files.Select(file => file.TryGetLocalPath()));
                };
            window.Opened += async (_, _) =>
            {
                logger.LogInformation("Main window opened");
                try
                {
                    await recovery.OfferPendingAsync();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    logger.LogError(ex, "Recovery offer failed");
                }
                await arrivals.OpenStartupAsync();
                try
                {
                    await _services.GetRequiredService<PulseOffice.Shell.Desktop.IDesktopIntegrationCoordinator>().CheckAsync();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    logger.LogWarning(ex, "Desktop integration check failed");
                }
                try
                {
                    await _services.GetRequiredService<PulseOffice.Shell.Updates.IUpdateCoordinator>().CheckInBackgroundAsync();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    logger.LogWarning(ex, "Background update check failed");
                }
            };

            _services.GetRequiredService<WindowPlacementCoordinator>().Attach(window);

            var quit = _services.GetRequiredService<QuitCoordinator>();
            var closeWindow = new WindowQuitTarget(window);
            var shutDown = new LifetimeQuitTarget(desktop);

            window.Closing += (_, args) => quit.Intercept(args, closeWindow);
            desktop.ShutdownRequested += (_, args) => quit.Intercept(args, shutDown);

            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                _closing = true;
                FlushSettings();
                _services.GetRequiredService<IDocumentRecovery>().DiscardAll();
                DisposeServices();
            };
        }
    }

    private void DisposeServices()
    {
        if (_services is not { } services) return;

        services.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void FlushSettings()
    {
        if (_services is null) return;

        foreach (var store in _services.GetServices<ISettingsStore>().OfType<JsonSettingsStore>())
            store.Flush();
    }

    private void ReportMissingTexts(CommandRegistry registry)
    {
        var texts = _services!.GetRequiredService<ITextCatalog>();

        var logger = _services!.GetRequiredService<ILoggerFactory>().CreateLogger<App>();

        var missingTitles = registry.All
            .Where(command => !texts.Contains(command.TitleKey))
            .Select(command => command.Id.Value)
            .Order()
            .ToList();

        if (missingTitles.Count > 0)
            logger.LogWarning(
                "Commands without a translated title ({Count}): {Commands}",
                missingTitles.Count, string.Join(", ", missingTitles));

        var unplanned = registry.UnplannedCommands(CommandSurface.Menu);

        if (unplanned.Count > 0)
            logger.LogWarning(
                "Menu commands with no layout rule ({Count}): {Commands}",
                unplanned.Count, string.Join(", ", unplanned));

        var deadMenuRules = registry.DeadLayoutRules(CommandSurface.Menu);

        if (deadMenuRules.Count > 0)
            logger.LogWarning(
                "Menu layout rules matching no command ({Count}): {Rules}",
                deadMenuRules.Count, string.Join(", ", deadMenuRules));

        var deadContextRules = registry.DeadLayoutRules(CommandSurface.ContextMenu);

        if (deadContextRules.Count > 0)
            logger.LogWarning(
                "Context menu layout rules matching no command ({Count}): {Rules}",
                deadContextRules.Count, string.Join(", ", deadContextRules));

        var deadRibbonRules = registry.DeadLayoutRules(CommandSurface.Ribbon);

        if (deadRibbonRules.Count > 0)
            logger.LogWarning(
                "Ribbon layout rules matching no command ({Count}): {Rules}",
                deadRibbonRules.Count, string.Join(", ", deadRibbonRules));

        var icons = _services!.GetRequiredService<PulseOffice.UI.Icons.IIconCatalog>();

        var withoutIcon = registry.All
            .Where(command => command.Surface.HasFlag(CommandSurface.Ribbon))
            .Where(command => !icons.Contains(command.Id.Value))
            .Select(command => command.Id.Value)
            .Order()
            .ToList();

        if (withoutIcon.Count > 0)
            logger.LogWarning(
                "Ribbon commands without an icon ({Count}): {Commands}",
                withoutIcon.Count, string.Join(", ", withoutIcon));

        var conflicts = registry.ConflictingShortcuts();

        if (conflicts.Count > 0)
            logger.LogWarning(
                "Commands active together that share a shortcut ({Count}): {Conflicts}",
                conflicts.Count, string.Join("; ", conflicts));

        var missingStructure = registry.StructureKeys()
            .Where(key => !texts.Contains(key))
            .ToList();

        if (missingStructure.Count > 0)
            logger.LogWarning(
                "Menu, ribbon or context groups without a translated title ({Count}): {Keys}",
                missingStructure.Count, string.Join(", ", missingStructure));
    }

    private void ApplyThemeFromSettings(IServiceProvider services)
    {
        var theme = services.GetRequiredService<IThemeService>();
        var settings = services.GetRequiredService<ISettingsService>();

        theme.ThemeChanged += (_, _) => Palette.Apply(this, theme.IsDark);

        void Apply() => theme.Apply(settings.Get<string>(ShellSettingKeys.Theme) switch
        {
            "light" => ThemePreference.Light,
            "dark" => ThemePreference.Dark,

            _ => ThemePreference.System,
        });

        settings.Changed += (_, e) =>
        {
            if (e.Key == ShellSettingKeys.Theme) Apply();
        };

        Apply();
        Palette.Apply(this, theme.IsDark);
    }
}
