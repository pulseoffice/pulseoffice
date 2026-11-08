using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Localization;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class DialogHost : ContentControl, IDialogService, PulseOffice.UI.Controls.References.IDialogCollapse
{
    private readonly List<DialogEntry> _dialogs = [];
    private Border? _backdrop;
    private ContentPresenter? _dialogPresenter;
    private Panel? _layer;
    private IDisposable? _sizeSubscription;
    private Func<IDialogResultAdmission?>? _admission;

    public DialogHost()
    {
        BuildTemplate();

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _dialogs.Count == 0 || _collapsed is not null) return;
        if (!_dialogs[^1].Options.CanCancelWithEscape) return;
        if (HasOpenDropDown(e.Source)) return;

        Close(null);
        e.Handled = true;
    }

    public bool IsDialogOpen => _dialogs.Count > 0;

    private TopLevel? _topLevel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        _topLevel?.AddHandler(GotFocusEvent, OnWindowGotFocus, RoutingStrategies.Bubble);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnWindowKeyDown);
        _topLevel?.RemoveHandler(GotFocusEvent, OnWindowGotFocus);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private bool InsideDialog(object? source)
    {
        if (_dialogs.Count == 0 || _dialogs[^1].Shell is not { } shell || source is not Visual visual) return false;
        if (ReferenceEquals(visual, shell) || shell.IsVisualAncestorOf(visual)) return true;
        if (visual is Avalonia.LogicalTree.ILogical logical && Avalonia.LogicalTree.LogicalExtensions.GetLogicalAncestors(logical).Contains(shell)) return true;
        return TopLevel.GetTopLevel(visual) is { } owner && !ReferenceEquals(owner, _topLevel);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dialogs.Count == 0 || _collapsed is not null || InsideDialog(e.Source)) return;
        if (e.Key == Key.Escape && _dialogs[^1].Options.CanCancelWithEscape)
        {
            Close(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            FocusInside(_dialogs[^1]);
            e.Handled = true;
        }
    }

    private void OnWindowGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_dialogs.Count == 0 || _collapsed is not null || InsideDialog(e.Source)) return;
        var entry = _dialogs[^1];
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_dialogs.Count > 0 && ReferenceEquals(_dialogs[^1], entry) && _collapsed is null
                && !InsideDialog(TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement())) FocusInside(entry);
        });
    }

    private bool HasOpenDropDown(object? source) =>
        source is Visual visual && visual.GetSelfAndVisualAncestors().Any(element => element switch
        {
            ComboBox combo => combo.IsDropDownOpen,
            AutoCompleteBox complete => complete.IsDropDownOpen,
            _ => false,
        });

    private sealed class DialogEntry(
        Control content,
        DialogOptions options,
        TaskCompletionSource<object?> completion,
        IInputElement? previousFocus,
        IDisposable? subscription)
    {
        public Control Content { get; } = content;

        public DialogOptions Options { get; } = options;

        public TaskCompletionSource<object?> Completion { get; } = completion;

        public IInputElement? PreviousFocus { get; } = previousFocus;

        public IDisposable? Subscription { get; } = subscription;

        public Control? Shell { get; set; }

        public Border? Frame { get; set; }

        public TextBlock? Title { get; set; }
    }

    private void BuildTemplate()
    {
        Template = new FuncControlTemplate((host, scope) =>
        {
            var content = new ContentPresenter
            {
                Name = "PART_ContentPresenter",
            };

            content.Bind(ContentPresenter.ContentProperty, host.GetObservable(ContentProperty));
            content.RegisterInNameScope(scope);

            _backdrop = new Border
            {
                IsVisible = false,
                Opacity = 0,
                Transitions =
                [
                    new Avalonia.Animation.DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(160),
                    },
                ],
            };
            _backdrop.Bind(Border.BackgroundProperty, _backdrop.GetResourceObservable(Palette.Keys.Scrim));
            _backdrop.PointerPressed += OnBackdropPressed;

            _dialogPresenter = new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false,
            };

            _layer = new Panel { Children = { content, _backdrop, _dialogPresenter } };
            return _layer;
        });
    }

    private void EnsureLayer()
    {
        if (_dialogPresenter is null) ApplyTemplate();
    }

    public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) =>
        ShowAsync<object>(content, options, ct);

    public async Task<TResult?> ShowAsync<TResult>(
        Control content, DialogOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);

        EnsureLayer();

        if (content is IDialogSizeHint { PreferredWidth: { } hinted } && hinted > 0)
            options = options with { PreferredWidth = hinted };

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

        DialogEntry? entry = null;
        IDisposable? subscription = null;
        if (content is IDialogContent dialogContent)
        {
            void Handler(object? _, object? result)
            {
                if (entry is not null && !(content is IDialogContent { CanClose: false })) Remove(entry, result);
            }

            dialogContent.Completed += Handler;
            subscription = new Unsubscriber(() => dialogContent.Completed -= Handler);
        }

        var ticket = _admission?.Invoke()?.Open();
        entry = new DialogEntry(content, options, completion, previousFocus, subscription);
        _dialogs.Add(entry);
        Present(entry);

        await using (ct.Register(() => Dispatcher.UIThread.Post(() => Remove(entry, null))))
        {
            var result = await completion.Task.ConfigureAwait(true);
            if (result is not null && ticket is not null && !ticket.Admits()) return default;
            return result is TResult typed ? typed : default;
        }
    }

    public void Close(object? result)
    {
        if (_dialogs.Count == 0) return;

        var entry = _dialogs[^1];
        if (entry.Content is IDialogContent { CanClose: false }) return;

        Remove(entry, result);
    }

    private void Remove(DialogEntry entry, object? result)
    {
        var index = _dialogs.IndexOf(entry);
        if (index < 0) return;

        var wasTop = index == _dialogs.Count - 1;
        if (_collapsed?.Entry == entry) EndCollapse(_collapsed, interrupted: true);

        _dialogs.RemoveAt(index);
        entry.Subscription?.Dispose();
        entry.Completion.TrySetResult(result);

        if (!wasTop) return;

        if (_dialogs.Count > 0)
        {
            Present(_dialogs[^1]);
        }
        else
        {
            _title = null;
            _sizeSubscription?.Dispose();
            _sizeSubscription = null;
            if (_backdrop is not null)
            {
                _backdrop.Opacity = 0;
                _backdrop.IsVisible = false;
            }
            if (_dialogPresenter is not null)
            {
                _dialogPresenter.IsVisible = false;
                _dialogPresenter.Content = null;
            }
        }

        if (entry.PreviousFocus is Visual previous && TopLevel.GetTopLevel(previous) is not null && previous is IInputElement input)
            input.Focus();
        else
            FocusDocument();
    }

    private void FocusDocument()
    {
        var target = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(this)
            .OfType<IDocumentFocusTarget>()
            .FirstOrDefault(candidate => candidate is Visual { IsEffectivelyVisible: true });

        target?.FocusDocument();
    }

    private void Present(DialogEntry entry)
    {
        if (_backdrop is null || _dialogPresenter is null) return;

        var metrics = DesignTokens.For(InterfaceDensity.Compact);
        var content = entry.Content;
        var options = entry.Options;

        if (entry.Shell is null || entry.Frame is null) Build(entry, metrics);
        else
        {
            _title = entry.Title;
            if (_title is not null) _title.Text = Headline(TitleOf(options));
        }

        Describe(entry);

        FitToHost(entry.Frame!, options, metrics.SpacingLarge * 2);

        _dialogPresenter.Content = entry.Shell;

        _backdrop.IsVisible = true;
        _backdrop.Opacity = 1;
        _dialogPresenter.IsVisible = true;

        Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusInside(entry), Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void FocusInside(DialogEntry entry)
    {
        if (!ReferenceEquals(_dialogPresenter?.Content, entry.Shell)) return;

        if (entry.Content is not Visual root) return;

        var candidates = root.GetVisualDescendants()
            .OfType<InputElement>()
            .Where(element => element.Focusable && element.IsEffectivelyEnabled && element.IsEffectivelyVisible)
            .ToList();

        var target = candidates.FirstOrDefault(element => element is TextBox { IsReadOnly: false })
            ?? candidates.FirstOrDefault(element => element is SelectingItemsControl or ToggleButton or NumericUpDown or Slider);

        if (target is not null) target.Focus(NavigationMethod.Tab);
        else if (entry.Frame is { } frame)
        {
            frame.Focusable = true;
            frame.Focus(NavigationMethod.Unspecified);
        }
    }

    private void Build(DialogEntry entry, DesignTokens.Metrics metrics)
    {
        var content = entry.Content;
        var options = entry.Options;

        var frame = new Border
        {
            Child = BuildFrameContent(content, options, metrics),

            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            Width = options.PreferredWidth,
            BorderThickness = new Thickness(0),
            BoxShadow = BoxShadows.Parse("0 24 64 -12 #3D000000"),
            ClipToBounds = true,
        };

        frame.Bind(BackgroundProperty, frame.GetResourceObservable(Palette.Keys.SurfaceOverlay));

        if (!double.IsNaN(options.PreferredHeight)) frame.Height = options.PreferredHeight;

        var shell = new ScrollViewer
        {
            Content = frame,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Margin = new Thickness(metrics.SpacingLarge),
        };

        KeyboardNavigation.SetTabNavigation(shell, KeyboardNavigationMode.Cycle);

        frame.Loaded += (_, _) => Fit(frame, options, metrics.SpacingLarge * 2, Bounds);

        entry.Shell = shell;
        entry.Frame = frame;
        entry.Title = _title;
    }

    private void Describe(DialogEntry entry)
    {
        if (entry.Frame is not { } frame) return;

        Avalonia.Automation.AutomationProperties.SetName(frame, Headline(TitleOf(entry.Options)));
        Avalonia.Automation.AutomationProperties.SetHelpText(frame, entry.Options.AccessibleDescription);
    }

    private void FitToHost(Border frame, DialogOptions options, double margin)
    {
        Fit(frame, options, margin, Bounds);
        _sizeSubscription?.Dispose();
        _sizeSubscription = this.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver<Rect>(bounds => Fit(frame, options, margin, bounds)));
    }

    private static void Fit(Border frame, DialogOptions options, double margin, Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var width = Math.Max(240, bounds.Width - margin);
        var height = Math.Max(200, bounds.Height - margin);

        frame.Width = Math.Min(options.PreferredWidth, width);

        if (!double.IsNaN(options.PreferredHeight))
            frame.Height = Math.Min(options.PreferredHeight, height);
        else
            frame.MaxHeight = height;
    }

    private Control BuildFrameContent(Control content, DialogOptions options, DesignTokens.Metrics metrics)
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };

        var accent = new Border { Height = 3 };

        accent.Bind(BackgroundProperty, accent.GetResourceObservable("PulseAccent"));

        var title = new TextBlock
        {
            Text = Headline(TitleOf(options)),
            FontSize = metrics.FontSizeHeading,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        title.Bind(TextBlock.ForegroundProperty, title.GetResourceObservable(Palette.Keys.TextPrimary));

        var close = new Button
        {
            Classes = { "dialogClose" },
            Width = metrics.ControlHeight,
            Height = metrics.ControlHeight,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = _icons?.Find("ui.close") is { } geometry
                ? new IconView(geometry, metrics.IconSize)
                : new TextBlock { Text = "✕" },
        };

        Avalonia.Automation.AutomationProperties.SetName(close, _texts?["prompt.close"] ?? "Close");
        close.Click += (_, _) => Close(null);

        _title = title;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(
                metrics.SpacingLarge, metrics.SpacingMedium, metrics.SpacingMedium, metrics.SpacingMedium),
        };

        Grid.SetColumn(title, 0);
        Grid.SetColumn(close, 1);

        row.Children.Add(title);
        row.Children.Add(close);

        var band = new Border
        {
            Child = row,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };

        band.Bind(BackgroundProperty, band.GetResourceObservable(Palette.Keys.SurfaceRaised));
        band.Bind(Border.BorderBrushProperty, band.GetResourceObservable(Palette.Keys.Border));

        var footer = (content as IDialogFooterOwner)?.Footer;

        var body = new Border
        {
            Child = content,
            Padding = new Thickness(
                metrics.SpacingLarge,
                metrics.SpacingLarge,
                metrics.SpacingLarge,
                footer is null ? metrics.SpacingLarge : metrics.SpacingMedium),
        };

        Grid.SetRow(accent, 0);
        Grid.SetRow(band, 1);
        Grid.SetRow(body, 2);

        layout.Children.Add(accent);
        layout.Children.Add(band);
        layout.Children.Add(body);

        if (footer is not null)
        {
            var tray = new Border
            {
                Child = footer,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(
                    metrics.SpacingLarge, metrics.SpacingMedium, metrics.SpacingLarge, metrics.SpacingMedium),
            };

            tray.Bind(BackgroundProperty, tray.GetResourceObservable(Palette.Keys.SurfaceRaised));
            tray.Bind(Border.BorderBrushProperty, tray.GetResourceObservable(Palette.Keys.Border));

            Grid.SetRow(tray, 3);
            layout.Children.Add(tray);
        }

        return layout;
    }

    private static string Headline(string title) =>
        title.TrimEnd('.', '\u2026', ' ');

    private IIconCatalog? _icons;

    private ITextCatalog? _texts;

    public void AdmitResultsThrough(Func<IDialogResultAdmission?> admission) => _admission = admission;

    public void Use(IIconCatalog icons, ITextCatalog texts)
    {
        if (_texts is not null) _texts.LanguageChanged -= OnLanguageChanged;

        _icons = icons;
        _texts = texts;
        _texts.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_texts is null || _dialogs.Count == 0) return;

        if (_title is not null) _title.Text = Headline(TitleOf(_dialogs[^1].Options));
        Describe(_dialogs[^1]);
    }

    private TextBlock? _title;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _dialogs.Count > 0 && _collapsed is null && _dialogs[^1].Options.CanCancelWithEscape)
        {
            Close(null);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_dialogs.Count > 0 && _dialogs[^1].Options.CanCancelWithBackdrop) Close(null);
        e.Handled = true;
    }

    private Collapsed? _collapsed;

    public bool IsCollapsed => _collapsed is not null;

    public event EventHandler? CollapsedChanged;

    public string? TopDialogTitle => _dialogs.Count == 0
        ? null
        : Headline(TitleOf(_dialogs[^1].Options));

    private string TitleOf(DialogOptions options) =>
        _texts is null ? options.TitleKey
        : options.TitleArguments.Count == 0 ? _texts[options.TitleKey]
        : _texts.Format(options.TitleKey, [.. options.TitleArguments]);

    public IDisposable Collapse(Control bar, Action interrupted)
    {
        ArgumentNullException.ThrowIfNull(bar);
        ArgumentNullException.ThrowIfNull(interrupted);

        if (_dialogs.Count == 0) throw new InvalidOperationException("No dialog is open.");
        if (_collapsed is not null) throw new InvalidOperationException("A dialog is already collapsed.");

        EnsureLayer();

        var accent = new Border
        {
            BorderThickness = new Thickness(2),
            IsHitTestVisible = false,
        };
        accent.Bind(Border.BorderBrushProperty, accent.GetResourceObservable("PulseAccent"));

        bar.HorizontalAlignment = HorizontalAlignment.Center;
        bar.VerticalAlignment = VerticalAlignment.Top;
        bar.Margin = new Thickness(DesignTokens.For(InterfaceDensity.Compact).SpacingLarge);

        var collapsed = new Collapsed(this, _dialogs[^1], bar, accent, interrupted);
        _collapsed = collapsed;

        if (_backdrop is not null) _backdrop.IsVisible = false;
        if (_dialogPresenter is not null) _dialogPresenter.IsVisible = false;

        _layer?.Children.Add(accent);
        _layer?.Children.Add(bar);

        CollapsedChanged?.Invoke(this, EventArgs.Empty);
        return collapsed;
    }

    private void EndCollapse(Collapsed collapsed, bool interrupted)
    {
        if (!ReferenceEquals(_collapsed, collapsed)) return;

        _collapsed = null;
        _layer?.Children.Remove(collapsed.Bar);
        _layer?.Children.Remove(collapsed.Accent);
        CollapsedChanged?.Invoke(this, EventArgs.Empty);

        if (interrupted)
        {
            collapsed.Interrupted();
            return;
        }

        if (_dialogs.Count == 0 || _dialogs[^1] != collapsed.Entry) return;

        if (_backdrop is not null)
        {
            _backdrop.IsVisible = true;
            _backdrop.Opacity = 1;
        }
        if (_dialogPresenter is not null) _dialogPresenter.IsVisible = true;
    }

    private sealed class Collapsed(DialogHost host, DialogEntry entry, Control bar, Border accent, Action interrupted) : IDisposable
    {
        public DialogEntry Entry { get; } = entry;

        public Control Bar { get; } = bar;

        public Border Accent { get; } = accent;

        public Action Interrupted { get; } = interrupted;

        public void Dispose() => host.EndCollapse(this, interrupted: false);
    }

    private sealed class Unsubscriber(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
