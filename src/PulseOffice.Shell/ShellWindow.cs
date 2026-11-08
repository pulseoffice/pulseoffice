using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Foundation.Modules;
using PulseOffice.Platform;
using PulseOffice.Shell.Surfaces;
using PulseOffice.Localization;
using PulseOffice.Settings;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Icons;
using Avalonia.Input;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shortcuts;
using PulseOffice.UI.Theming;

namespace PulseOffice.Shell;

public sealed class ShellWindow : Window, IShellPresenter, INotificationActionRunner
{
    private readonly IWindowChrome _chrome;
    private readonly PulseOffice.UI.Controls.Services.IInterfaceLock _interfaceLock;
    private readonly PulseOffice.UI.Controls.Navigation.IWindowRegionCycle _regionCycle;
    private readonly CommandSurfaceFactory _surfaces;
    private readonly IStatusBarService _status;
    private readonly IAccentService _accent;
    private readonly DesignTokens.Metrics _metrics;

    private readonly TitleBar _titleBar;
    private readonly StatusBarView _statusBar;
    private readonly RibbonView _ribbon;
    private readonly DocumentTabsView _documentTabs;
    private readonly Panel _workspaceHost;
    private readonly SidePanelView _sidePanel;

    private readonly DialogHost _dialogHost;
    private readonly IShortcutDispatcher _shortcuts;
    private readonly PulseOffice.UI.Controls.Text.ITextEditingRouter _textEditing;
    private readonly PulseOffice.UI.Controls.Services.IDocumentKeyScope _keys;

    private readonly IDocumentWorkspace _workspace;
    private readonly IRecentDocuments _recent;
    private readonly Control _ribbonLayer;
    private readonly ISettingsService _settings;
    private readonly IApplicationVisibility _applicationVisibility;

    public ShellWindow(
        IWindowChromeProvider chromeProvider,
        CommandSurfaceFactory surfaces,
        IStatusBarService status,
        IAccentService accent,
        IDocumentWorkspace workspace,
        DialogHost dialogHost,
        IShortcutDispatcher shortcuts,
        PulseOffice.UI.Controls.Text.ITextEditingRouter textEditing,
        Func<IUnsavedChangesGuard> guard,
        ITextCatalog texts,
        IIconCatalog icons,
        SidePanelView sidePanel,
        ISettingsService settings,
        IRecentDocuments recent,
        PulseOffice.UI.Controls.Ribbon.RibbonServices ribbonServices,
        PulseOffice.UI.Controls.Services.IRibbonStateNotifier ribbonState,
        INotificationCenter notifications,
        INotificationHistory history,
        PulseOffice.UI.Controls.Services.IDocumentKeyScope keys,
        IApplicationVisibility applicationVisibility,
        IDocumentFormatFamily formatFamily,
        ICommandPaletteSource paletteSource,
        PulseOffice.UI.Commands.Shortcuts.IGestureNotation notation,
        IStatusZoomSource statusZoom,
        PulseOffice.UI.Controls.Navigation.IWindowRegionCycle regionCycle,
        PulseOffice.UI.Controls.Services.IInterfaceLock interfaceLock)
    {
        _interfaceLock = interfaceLock ?? throw new ArgumentNullException(nameof(interfaceLock));
        _regionCycle = regionCycle ?? throw new ArgumentNullException(nameof(regionCycle));
        _statusZoom = statusZoom ?? throw new ArgumentNullException(nameof(statusZoom));
        _paletteSource = paletteSource ?? throw new ArgumentNullException(nameof(paletteSource));
        _notation = notation ?? throw new ArgumentNullException(nameof(notation));
        _formatFamily = formatFamily ?? throw new ArgumentNullException(nameof(formatFamily));
        _applicationVisibility = applicationVisibility ?? throw new ArgumentNullException(nameof(applicationVisibility));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _workspace = workspace;
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _recent = recent ?? throw new ArgumentNullException(nameof(recent));
        _settings = settings;
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));

        _dialogHost = dialogHost;
        _shortcuts = shortcuts ?? throw new ArgumentNullException(nameof(shortcuts));
        _textEditing = textEditing ?? throw new ArgumentNullException(nameof(textEditing));

        ArgumentNullException.ThrowIfNull(chromeProvider);

        _chrome = chromeProvider.Current;
        _surfaces = surfaces;
        _surfaces.Blocked = () => _dialogHost.IsDialogOpen || _interfaceLock.IsLocked;
        _surfaces.Suspended = () => _dialogHost.IsCollapsed;
        _dialogHost.CollapsedChanged += (_, _) => OnPickingChanged();
        _status = status;
        _accent = accent;
        _metrics = DesignTokens.For(InterfaceDensity.Compact);

        Title = "PulseOffice";
        this.Themed(BackgroundProperty, Palette.Keys.Canvas);
        Width = 1280;
        Height = 800;
        MinWidth = 720;
        MinHeight = 480;

        ExtendClientAreaToDecorationsHint = true;

        ExtendClientAreaTitleBarHeightHint = _metrics.TitleBarHeight;

        WindowDecorations = WindowDecorations.Full;

        if (_chrome.DrawsCaptionButtons) UseOwnCaptionButtons();

        _titleBar = new TitleBar(_chrome, _texts);

        _titleBar.UseCommandSearch(
            _icons.Find("shell.view.commandPalette"),
            "titlebar.search",
            ShortcutGesture.Parse("Ctrl+Shift+P")?.Display() ?? string.Empty,
            () => _ = ShowCommandPaletteAsync());
        _statusBar = new StatusBarView(_status);
        _statusBar.UseHistory(new NotificationHistoryButton(history, _icons, _texts, this));
        _zoomButton = new PulseOffice.UI.Controls.Zoom.StatusZoomButton(_icons, _texts["zoom.title"], _metrics);
        _zoomButton.Click += (_, _) =>
        {
            if (_statusZoom.Command(_context) is { } zoomCommand) _surfaces.Execute(zoomCommand, _context);
        };
        _statusBar.UseZoom(_zoomButton);
        _statusBar.CommandRequested += (_, commandId) =>
        {
            if (_surfaces.FindCommand(commandId) is { } command) _surfaces.Execute(command, _context);
        };
        _ribbon = new RibbonView(ribbonServices);
        _ribbon.CommandInvoked += (_, invocation) =>
        {
            if (_workspace.Active is { } active) FocusShownDocument(active);
            if (_surfaces.FindCommand(invocation.CommandId) is { } command)
                _surfaces.Execute(command, _context with { Argument = invocation.Argument });
        };
        ArgumentNullException.ThrowIfNull(ribbonState);
        ribbonState.StateChanged += (_, _) => RefreshRibbon();

        _documentTabs = new DocumentTabsView(workspace, texts);
        _documentTabs.CloseRequested += (_, document) => _ = CloseAsync(_guard(), document);
        _workspaceHost = new Panel();
        _sidePanel = sidePanel;
        _workspaceHost.Themed(Panel.BackgroundProperty, Palette.Keys.Surface);

        var workArea = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        Grid.SetColumn(_workspaceHost, 0);
        Grid.SetColumn(_sidePanel, 1);

        workArea.Children.Add(_workspaceHost);
        workArea.Children.Add(_sidePanel);

        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto") };

        Grid.SetRow(_titleBar, 0);

        _ribbonLayer = new Border
        {
            Child = _ribbon,
            BoxShadow = BoxShadows.Parse("0 2 10 -4 #26000000"),
        };

        Grid.SetRow(_ribbonLayer, 1);
        Grid.SetRow(_documentTabs, 2);
        Grid.SetRow(workArea, 3);
        Grid.SetRow(_statusBar, 4);

        layout.Children.Add(_titleBar);
        layout.Children.Add(_ribbonLayer);
        layout.Children.Add(_documentTabs);
        layout.Children.Add(workArea);
        layout.Children.Add(_statusBar);

        dialogHost.Content = layout;
        Content = new Panel
        {
            Children =
            {
                dialogHost,
                new NotificationToastHost(notifications, _icons, _texts, this),
            },
        };

        ApplyVisibility();

        settings.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(ApplyVisibility);

        _accent.SetActiveModule(ModuleKind.Shell);

        if (_chrome.MenuPlacement == MenuPlacement.SystemMenuBar)
        {
            NativeMenu.SetMenu(this, _nativeMenu);
            if (Application.Current is { } application)
            {
                var existing = NativeMenu.GetMenu(application);
                var menu = new ApplicationMenu(_texts, ExecuteById, _applicationVisibility, existing).Menu;
                if (existing is null) NativeMenu.SetMenu(application, menu);
            }
        }

        ShowWelcome();

        AddHandler(KeyDownEvent, OnPreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, (_, e) => e.Handled |= _interfaceLock.IsLocked, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _interfaceLock.Changed += (_, _) =>
        {
            workArea.IsHitTestVisible = !_interfaceLock.IsLocked;
            _ribbonLayer.IsHitTestVisible = !_interfaceLock.IsLocked;
            Cursor = _interfaceLock.IsLocked ? new Cursor(StandardCursorType.Wait) : Cursor.Default;
        };
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Bubble);

        _texts.LanguageChanged += (_, _) =>
        {
            _status.DismissTransient();
            UpdateForContext(_context);
            if (_workspace.Active is null) ShowWelcome();
        };

        _workspace.DocumentClosed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_workspace.Active is null) ShowWelcome();
        });

        _workspace.ContextChanged += (_, _) => UpdateForContext(_workspace.Context);
        _workspace.ActiveChanged += (_, document) => ShowDocument(document);
        _workspace.ActiveUpdated += (_, document) =>
        {
            ShowDocumentName(document);
            RefreshRibbon();
        };
    }

    private void ShowDocument(OpenDocument document)
    {
        if (_workspaceHost.Children.Count != 1
            || !ReferenceEquals(_workspaceHost.Children[0], document.View))
        {
            _workspaceHost.Children.Clear();
            _workspaceHost.Children.Add(document.View);
        }

        ShowDocumentName(document);
        ApplyVisibility();
        FocusShownDocument(document);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_workspace.Active, document)) FocusShownDocument(document);
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void FocusShownDocument(OpenDocument document)
    {
        if (_dialogHost.IsDialogOpen) return;
        if (document.View is IDocumentFocusTarget target) target.FocusDocument();
        else document.View.Focus();
    }

    private readonly IDocumentFormatFamily _formatFamily;

    private void ShowDocumentName(OpenDocument document)
    {
        _documentTabs.Refresh();
        _status.Register("shell.format", StatusSegmentKind.Permanent).Text = _formatFamily.Of(document.Path);

        Title = $"{document.DisplayName} — PulseOffice";
        ShowTitleInChrome(document.DisplayName);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_interfaceLock.IsLocked)
        {
            e.Handled = true;
            return;
        }
        if (IsEditingText()) return;

        OnKeyDown(sender, e);
    }

    private bool IsEditingText() => _textEditing.IsEditingText(this);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_interfaceLock.IsLocked)
        {
            e.Handled = true;
            return;
        }
        if (e.Handled || _dialogHost.IsDialogOpen) return;
        if (MovesBetweenRibbonAndDocument(e))
        {
            e.Handled = true;
            return;
        }
        if (!_keys.Allows(e, this)) return;

        if (_shortcuts.Resolve(e, _context) is not { } command) return;

        e.Handled = true;
        _surfaces.Execute(command, _context);
    }

    private bool MovesBetweenRibbonAndDocument(KeyEventArgs e)
    {
        var inRibbon = _ribbon.IsKeyboardFocusWithin;
        if (e.Key == Key.F6 && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            _regionCycle.Move(Regions(), e.KeyModifiers == KeyModifiers.Shift);
            return true;
        }
        if (e.Key != Key.Escape || !inRibbon || _workspace.Active is not { } active) return false;
        FocusShownDocument(active);
        return true;
    }

    private IReadOnlyList<PulseOffice.UI.Controls.Navigation.IWindowRegion> Regions() =>
    [
        new PulseOffice.UI.Controls.Navigation.DelegateWindowRegion(
            () => _ribbon.IsEffectivelyVisible, () => _ribbon.IsKeyboardFocusWithin, _ribbon.FocusTabs),
        .. DocumentRegions(),
        new PulseOffice.UI.Controls.Navigation.ControlWindowRegion(_sidePanel),
        new PulseOffice.UI.Controls.Navigation.ControlWindowRegion(_statusBar),
    ];

    private IReadOnlyList<PulseOffice.UI.Controls.Navigation.IWindowRegion> DocumentRegions() =>
        _workspace.Active?.View is PulseOffice.UI.Controls.Navigation.IDocumentRegions regions
            ? regions.Regions
            :
            [
                new PulseOffice.UI.Controls.Navigation.DelegateWindowRegion(
                    () => _workspace.Active is not null,
                    () => _workspaceHost.IsKeyboardFocusWithin,
                    () =>
                    {
                        if (_workspace.Active is not { } shown) return false;
                        FocusShownDocument(shown);
                        return true;
                    }),
            ];

    public async Task ShowShortcutsAsync()
    {
        if (_dialogHost.IsDialogOpen) return;

        await _dialogHost.ShowAsync(
            new ShortcutsView(_shortcuts.Known(_context), _texts),
            new DialogOptions
            {
                TitleKey = "dialog.shortcuts.title",
                PreferredWidth = 560,
                CanCancelWithBackdrop = true,
            });
    }

    public async Task ShowCommandPaletteAsync()
    {
        if (_dialogHost.IsDialogOpen) return;

        var palette = new CommandPaletteView(
            query => _paletteSource.Sections(query, _context),
            _icons,
            _notation,
            _texts["palette.placeholder"],
            _texts["palette.empty"]);

        var chosen = await _dialogHost.ShowAsync<CommandDefinition>(palette, new DialogOptions
        {
            TitleKey = "palette.title",
            PreferredWidth = 640,
            CanCancelWithBackdrop = true,
            AccessibleDescription = _texts["palette.description"],
        });

        if (chosen is null) return;

        _paletteSource.Chosen(chosen);
        _surfaces.Execute(chosen, _context);
    }

    private void ShowTitleInChrome(string? title)
    {
        _titleBar.TitleHost.Children.Clear();

        if (string.IsNullOrEmpty(title)) return;

        _titleBar.TitleHost.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = _metrics.FontSize,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
    }

    public Panel WorkspaceHost => _workspaceHost;

    private CommandContext _context = CommandContext.Empty;

    private readonly NativeMenu _nativeMenu = new();

    private readonly ITextCatalog _texts;

    private readonly ICommandPaletteSource _paletteSource;

    private readonly IStatusZoomSource _statusZoom;

    private readonly PulseOffice.UI.Controls.Zoom.StatusZoomButton _zoomButton;

    private readonly PulseOffice.UI.Commands.Shortcuts.IGestureNotation _notation;

    private readonly IIconCatalog _icons;

    private readonly Func<IUnsavedChangesGuard> _guard;

    private readonly HashSet<OpenDocument> _closing = [];

    private async Task CloseAsync(IUnsavedChangesGuard guard, OpenDocument document)
    {
        if (!_closing.Add(document)) return;

        try
        {
            if (!await guard.ConfirmCloseAsync([document]).ConfigureAwait(true)) return;
            _workspace.Close(document);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _status.Report(ex.Message, NotificationSeverity.Error);
        }
        finally
        {
            _closing.Remove(document);
        }
    }

    public Task CloseActiveDocumentAsync() =>
        _workspace.Active is { } active ? CloseAsync(_guard(), active) : Task.CompletedTask;

    public void RequestQuit() => Close();

    public void RefreshSurfaces() => UpdateForContext(_context);

    private void OnPickingChanged()
    {
        var picking = _dialogHost.IsCollapsed;
        _ribbon.IsEnabled = !picking;
        _ribbon.Opacity = picking ? 0.45 : 1;
        _documentTabs.Opacity = picking ? 0.45 : 1;
        _documentTabs.IsEnabled = !picking;
        if (_titleBar.CommandSearch is { } search) search.IsEnabled = !picking;
        _titleBar.MenuHost.IsEnabled = !picking;
        RefreshSurfaces();
    }

    private void RefreshRibbon()
    {
        RefreshZoom();
        if (_workspace.Active is null) return;
        _ribbon.Update(_surfaces.BuildRibbon(_context));
    }

    private void RefreshZoom() => _zoomButton.Show(_workspace.Active is null ? null : _statusZoom.Percent(_context));

    private void ExecuteById(string id)
    {
        if (_surfaces.FindCommand(id) is { } command) _surfaces.Execute(command, _context);
    }

    public void UpdateForContext(CommandContext context)
    {
        _context = context;
        RefreshZoom();

        if (_chrome.MenuPlacement == MenuPlacement.SystemMenuBar)
        {
            _surfaces.FillNativeMenu(_nativeMenu, context);
        }
        else
        {
            _titleBar.MenuHost.Children.Clear();
            _titleBar.MenuHost.Children.Add(_surfaces.BuildMenu(context));
        }

        _ribbon.Update(_surfaces.BuildRibbon(context));

        if (context.Module != ModuleKind.Shell) _accent.SetActiveModule(context.Module);
        else if (_workspace.Active is null) _accent.SetActiveModule(ModuleKind.Shell);

        _statusBar.ShowModule(
            context.Module == ModuleKind.Shell ? null : ModuleIdentity.For(context.Module).BrandName,
            context.Module == ModuleKind.Shell ? null : ModuleIdentity.For(context.Module).AccentHex);
        _statusBar.Refresh();
    }

    private void ShowWelcome()
    {
        var start = new StartView(_recent, _texts, _icons);

        start.CommandRequested += (_, id) =>
        {
            if (_surfaces.FindCommand(id) is { } command) _surfaces.Execute(command, _context);
        };

        _workspaceHost.Children.Clear();
        _workspaceHost.Children.Add(start);

        ApplyVisibility();

        _status.Register("shell.format", StatusSegmentKind.Permanent).Text = _formatFamily.Of(null);
        ShowTitleInChrome(null);
        Title = "PulseOffice";
        _status.Report(_texts["status.ready"]);
        _statusBar.Refresh();
    }

    private void UseOwnCaptionButtons()
    {
        if (Application.Current?.TryGetResource(typeof(Avalonia.Controls.Chrome.WindowDrawnDecorations), null, out var found) != true
            || found is not Avalonia.Styling.ControlTheme standard
            || standard.Setters.OfType<Avalonia.Styling.Setter>().FirstOrDefault(setter => setter.Property == Avalonia.Controls.Chrome.WindowDrawnDecorations.TemplateProperty)?.Value
                is not Avalonia.Controls.Chrome.IWindowDrawnDecorationsTemplate template)
            return;

        WindowDecorationsTheme = new Avalonia.Styling.ControlTheme(typeof(Avalonia.Controls.Chrome.WindowDrawnDecorations))
        {
            BasedOn = standard,
            Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Chrome.WindowDrawnDecorations.TemplateProperty, new BorderOnlyDecorationsTemplate(template)) },
        };
    }

    private void ApplyVisibility()
    {
        var mode = _settings.Get<string>(ShellSettingKeys.InterfaceMode);
        var working = _workspace.Active is not null;

        _ribbonLayer.IsVisible = working
            && _settings.Get<bool>(ShellSettingKeys.RibbonVisible);

        _ribbon.Compact = mode == "compact";
        _statusBar.IsVisible = _settings.Get<bool>(ShellSettingKeys.StatusBarVisible);
        _documentTabs.IsVisible = working && _settings.Get<bool>(ShellSettingKeys.DocumentTabsVisible);
    }

    public void Run(NotificationAction action)
    {
        if (_dialogHost.IsDialogOpen) return;

        try
        {
            switch (action)
            {
                case CallbackNotificationAction callback:
                    callback.Run();
                    break;
                case CommandNotificationAction command
                    when _surfaces.FindCommand(command.CommandId) is { } definition:
                    _surfaces.Execute(definition, _context);
                    break;
            }
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            _status.Report(_texts.Format("notification.action.failed", failure.Message), NotificationSeverity.Error);
        }
    }
}
