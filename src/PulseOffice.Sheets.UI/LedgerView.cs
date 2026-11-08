using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using PulseOffice.Document.Forms;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Drawing;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerView : UserControl, IShellViewOptions, IPendingEditsOwner, PulseOffice.UI.Controls.Services.IDocumentFocusTarget, PulseOffice.UI.Controls.Navigation.IDocumentRegions
{
    public void CommitPendingEdits()
    {
        _editing.CommitEditor();
        _editing.CommitFormulaBar();
    }

    private readonly Workbook _workbook;
    private readonly CalculationEngine _engine;

    public CalculationEngine Engine => _engine;
    private readonly IStatusBarService _status;
    private readonly IContextMenuFactory? _contextMenus;
    private readonly IContextMenuKey _menuKey;
    private readonly DesignTokens.Metrics _metrics;

    private readonly SheetSelection _selection = new();
    private readonly SheetGridView _grid;
    private readonly ITextCatalog _texts;

    private readonly SheetTabStrip _sheetTabs;
    private readonly SheetTabSurface _tabSurface;

    private readonly LedgerStatusSegments _segments;
    private readonly Canvas _overlay;
    private readonly List<(Action Link, Action Unlink)> _globalLinks = [];

    private readonly LedgerCellWriter _writer;
    private readonly LedgerCellEditing _editing;
    private readonly LedgerObjectEditing _objects;
    private readonly LedgerCellOperations _operations;
    private readonly LedgerShapeTextSession _shapeText;

    private readonly IActiveSheetContext _sheetContext;
    private readonly IDocumentWorkspace _workspace;

    private readonly PulseOffice.Document.Commands.UndoStack _history;

    public Engine.Operations.ISheetFill Fill
    {
        get => _operations.Fill;
        init => _operations.Fill = value;
    }

    public IOptionGroups OptionGroups
    {
        get => _objects.OptionGroups;
        init => _objects.OptionGroups = value;
    }

    public PulseOffice.UI.Controls.Links.ILinkFollower? Links { get; init; }

    public bool GoToPlace(string place)
    {
        var reference = place.TrimStart('#');
        if (reference.Length == 0) return false;
        if (_editing.GoTo(reference)) return true;
        var dot = reference.LastIndexOf('.');
        return dot > 0 && !reference.Contains('!', StringComparison.Ordinal) && _editing.GoTo(reference[..dot] + "!" + reference[(dot + 1)..]);
    }

    public INameBoxResolver NameBox
    {
        get => _editing.Resolver;
        init => _editing.Resolver = value;
    }

    public Engine.Operations.IReferenceCycler References
    {
        get => _editing.References;
        init => _editing.References = value;
    }

    public Engine.Localization.IFormulaLocalization? Formulas
    {
        get => _writer.Formulas;
        init => _writer.Formulas = value;
    }

    public Engine.Operations.ICellEditText EditTexts
    {
        get => _writer.EditTexts;
        init => _writer.EditTexts = value;
    }

    public Engine.Operations.ICellInputRecognizer Inputs
    {
        get => _writer.Inputs;
        init => _writer.Inputs = value;
    }

    public PulseOffice.UI.Controls.Lists.IChoiceListPopup Choices
    {
        get => _editing.Choices;
        init => _editing.Choices = value;
    }

    public IFormEventRunner? FormEvents
    {
        get => _objects.FormEvents;
        set => _objects.FormEvents = value;
    }

    public IRibbonStateNotifier? RibbonState { get; init; }

    public PulseOffice.Sheets.Formatting.IRowAutoHeight? RowHeights
    {
        get => _rowHeights;
        init
        {
            _rowHeights = value;
            _rowTracker = value is null ? null : new PulseOffice.Sheets.Formatting.RowAutoHeightTracker(value);
            _rowTracker?.Attach(_workbook);
        }
    }

    private readonly PulseOffice.Sheets.Formatting.IRowAutoHeight? _rowHeights;
    private readonly PulseOffice.Sheets.Formatting.RowAutoHeightTracker? _rowTracker;

    public LedgerView(
        Workbook workbook, CalculationEngine engine, ICellPresenter presenter,
        ConditionalFormattingEvaluator conditional,
        IActiveSheetContext sheetContext, IDocumentWorkspace workspace,
        PulseOffice.Document.Commands.UndoStack history, IStatusBarService status,
        ITextCatalog texts, Engine.Operations.IDataValidator? validator = null,
        IContextMenuFactory? contextMenus = null, IFormDesignMode? design = null,
        IDrawingToolState? tools = null, ISheetSpelling? spelling = null,
        Text.Spelling.IAutoCorrect? autoCorrect = null,
        Text.Spelling.IAutoCorrectSettings? autoCorrectSettings = null,
        Engine.Operations.IAutoInput? autoInput = null,
        ISheetConnectors? connectors = null,
        IFormControlBinding? formBinding = null,
        ShapeText.IShapeTextCellStyle? shapeTextStyle = null,
        Drawing.Shapes.IShapeTextEditing? shapeTextEditing = null,
        IContextMenuKey? menuKey = null,
        Drawing.Shapes.IShapeTextParagraphs? shapeParagraphs = null)
    {
        _menuKey = menuKey ?? new ContextMenuKey();
        _contextMenus = contextMenus;

        _history = history ?? throw new ArgumentNullException(nameof(history));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _sheetContext = sheetContext ?? throw new ArgumentNullException(nameof(sheetContext));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

        _workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _status = status ?? throw new ArgumentNullException(nameof(status));

        _metrics = DesignTokens.For(InterfaceDensity.Compact);

        _history.Changed += (_, _) => RefreshEverything();

        var binding = formBinding ?? new FormControlBinding(new PulseOffice.Sheets.Formatting.SystemSpreadsheetLocale());
        _grid = new SheetGridView(_engine, presenter, conditional, _selection, formBinding: binding)
        {
            ChartRenderer = new PulseOffice.UI.Controls.Charts.ChartRenderer(_texts),
            ChartData = new Engine.Operations.ChartDataExtractor(new LocalizedSeriesNames(_texts), new Engine.Operations.PresentedChartCellText(presenter)),
        };

        _overlay = new Canvas { IsHitTestVisible = false };

        _writer = new LedgerCellWriter(_workbook, _engine, _history, _status, _texts, _workspace, _grid,
            validator, autoCorrect, autoCorrectSettings, spelling);
        _editing = new LedgerCellEditing(_workbook, _engine, _grid, _selection, _overlay, _status, _texts, _writer,
            validator, autoInput ?? new Engine.Operations.AutoInput(), _metrics);
        _objects = new LedgerObjectEditing(_workbook, _grid, _history, _status, _texts,
            connectors ?? new SheetConnectors(), binding, _writer);
        _operations = new LedgerCellOperations(_workbook, _engine, _grid, _selection, _history, _status, _texts, _writer);
        var textEditing = shapeTextEditing ?? new Drawing.Shapes.ShapeTextEditing();
        _shapeText = new LedgerShapeTextSession(_workbook, _grid, _overlay, this, _texts,
            shapeTextStyle ?? new ShapeText.ShapeTextCellStyle(textEditing), textEditing,
            _writer, _editing, _objects, shapeParagraphs ?? new Drawing.Shapes.ShapeTextParagraphs());

        _writer.SelectionStale += (_, _) => OnSelectionChanged();
        _editing.SelectionStale += (_, _) => OnSelectionChanged();
        _editing.SheetRequested += (_, index) => SelectSheet(index);
        _operations.SelectionStale += (_, _) => OnSelectionChanged();
        _shapeText.SelectionStale += (_, _) => OnSelectionChanged();
        _shapeText.PendingChanged += (_, _) => RibbonState?.Notify();

        _sheetTabs = new SheetTabStrip(_metrics)
        {
            Margin = new Thickness(_metrics.SpacingSmall, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
        };

        _tabSurface = new SheetTabSurface(_sheetTabs, new PulseOffice.UI.Controls.Tabs.TabOverlays(_metrics));
        _sheetTabs.SheetChosen += (_, index) => SelectSheet(index);
        _sheetTabs.RenameRequested += (_, index) =>
        {
            SelectSheet(index);
            if (_referencePick is not null) return;
            SheetRenameRequested?.Invoke(this, EventArgs.Empty);
        };

        _sheetTabs.MenuRequested += (_, _) => ShowContextMenu("sheet");
        _sheetTabs.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (!_menuKey.Matches(args)) return;
            args.Handled = true;
            ShowContextMenu("sheet", new Rect(_sheetTabs.Bounds.Width / 2, 0, 1, _sheetTabs.Bounds.Height));
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        _segments = new LedgerStatusSegments(_status, _texts, _selection, new SelectionSummary());

        _overlay.Children.Add(_editing.CellEditor);
        _overlay.Children.Add(_shapeText.Editor);

        _grid.ZoomChanged += (_, zoom) => ShowZoom(zoom);
        ShowZoom(_grid.Zoom);

        Content = new LedgerViewLayout(_metrics).Build(_editing.NameBox, _editing.FormulaBox, _sheetTabs, _grid, _overlay);

        _selection.Changed += (_, _) =>
        {
            if (_editing.IsEditing) _editing.CommitEditor();
            _editing.CommitFormulaBar();
            OnSelectionChanged();
            PublishSelectionContext();
            _referencePick?.SelectionChanged();
        };

        _grid.ReferenceCommitRequested += (_, _) => _referencePick?.Commit();
        _grid.ReferenceCancelRequested += (_, _) => _referencePick?.Cancel();

        _grid.FillRequested += (_, request) => _operations.ApplyFill(request);
        _grid.ClearRequested += (_, range) => _operations.ClearContents(range);
        _grid.ObjectsDeleteRequested += (_, objects) => _objects.DeleteObjects(objects);
        _grid.ResizeRequested += (_, request) => _operations.ApplyResize(request);
        _grid.FormControlChanged += (_, change) => _objects.ApplyFormControl(change);
        _grid.FreeformCompleted += (_, completed) => _objects.InsertFreeform(completed);
        _grid.FreeformEdited += (_, edited) => _objects.ApplyFreeform(edited);
        _grid.ShapeAdjusted += (_, adjusted) => _objects.ApplyAdjustment(adjusted);

        if (design is not null)
        {
            _grid.DesignMode = design.IsDesigning;
            EventHandler handler = (_, _) => _grid.DesignMode = design.IsDesigning;
            _globalLinks.Add((() => { design.Changed += handler; _grid.DesignMode = design.IsDesigning; }, () => design.Changed -= handler));
        }

        if (design?.IsDesigning != true && !_workbook.FormSettings.OpenInDesignMode && _workbook.FormSettings.AutomaticFocus
            && _workbook.Sheets.ElementAtOrDefault(_grid.SheetIndex)?.Objects
                .Where(placed => placed.Content is FormControlObject)
                .OrderBy(placed => ((FormControlObject)placed.Content).Properties.TabIndex)
                .FirstOrDefault() is { } first)
            _grid.SelectObject(first);

        if (tools is not null)
        {
            _grid.Tools = tools;
            EventHandler handler = (_, _) => _grid.Refresh();
            _globalLinks.Add((() => tools.Changed += handler, () => tools.Changed -= handler));
        }

        _grid.Spelling = spelling;
        _grid.ObjectChangeRequested += (_, changes) => _objects.ApplyObjectChanges(changes);
        _grid.ContextMenuRequested += (_, root) => ShowContextMenu(root);
        _grid.FilterRequested += (_, column) => FilterMenuRequested?.Invoke(this, column);
        _grid.ObjectActivated += (_, _) => ObjectEditRequested?.Invoke(this, EventArgs.Empty);
        _grid.ObjectLinkFollowed += (_, link) => Links?.Follow(link, GoToPlace);

        _grid.SelectedObjectChanged += (_, placed) => _workspace.UpdateContext(context => context with
        {
            State = _objects.ObjectFacts(context.State, placed),
        });
        _grid.EditRequested += (_, _) => _editing.BeginEdit(replaceContent: null);
        _grid.ChoicesAvailable = _editing.HasChoices;
        _grid.ChoicesRequested += (_, address) => _editing.ShowChoices(address);
        AddHandler(KeyDownEvent, (_, args) =>
        {
            if (Choices.IsOpen && args.KeyModifiers == KeyModifiers.None && Choices.HandleKey(args.Key)) args.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _grid.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (!_menuKey.Matches(args)) return;
            args.Handled = true;
            ShowContextMenu("cell", _grid.ActiveCellBounds());
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _grid.TypingStarted += (_, text) => _editing.BeginEdit(replaceContent: text);
        _grid.ShapeTextRequested += (_, request) => _shapeText.Begin(request);
        _grid.SelectedObjectChanged += (_, _) => _shapeText.FollowSelectedObject();

        BuildSheetTabs();
        OnSelectionChanged();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        foreach (var (link, _) in _globalLinks) link();
        _sheetContext.Attach(CurrentTarget());

        PublishSelectionContext();
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        foreach (var (_, unlink) in _globalLinks) unlink();
        _sheetContext.Detach(_workbook);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _texts.LanguageChanged += OnLanguageChanged;
        ShowStatus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _texts.LanguageChanged -= OnLanguageChanged;
        _segments.Hide();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ShowStatus();
        if (_editing.IsIdle) OnSelectionChanged();
    }

    private void ShowStatus()
    {
        var sheet = _grid.SheetIndex >= 0 && _grid.SheetIndex < _workbook.Sheets.Count ? _workbook.Sheets[_grid.SheetIndex] : null;
        _segments.Show(sheet, _grid.Zoom);
        _editing.ApplyZoom(_grid.Zoom);
    }

    private void ShowZoom(double zoom)
    {
        _segments.ShowZoom(zoom);

        _editing.ApplyZoom(zoom);
    }

    public event EventHandler? ObjectEditRequested;

    public event EventHandler? SheetRenameRequested;

    public SheetTabSurface TabSurface => _tabSurface;

    public event EventHandler<int>? FilterMenuRequested;

    private void ShowContextMenu(string root, Rect? anchor = null)
    {
        if (_contextMenus is null || _referencePick is not null) return;

        var host = root == "sheet" ? (Control)_sheetTabs : _grid;
        var opened = anchor is { } rect ? _contextMenus.OpenAt(root, host, rect, () => _grid.Focus()) : _contextMenus.Open(root, host);
        if (!opened) _status.Report(_texts["status.context.empty"]);
    }

    private void PublishSelectionContext()
    {
        var kind = _selection.IsSingleCell ? SelectionKind.Cell : SelectionKind.CellRange;

        _workspace.UpdateContext(context => context with
        {
            Selection = kind,
            State = context.State | DocumentState.HasSelection,
        });

        RibbonState?.Notify();
    }

    private SheetTarget CurrentTarget() => new SheetTarget
        {
            Tabs = _tabSurface,
            Workbook = _workbook,
            SheetIndex = _grid.SheetIndex,
            History = _history,
            Engine = _engine,
            Selection = () => _workbook.Sheets[_grid.SheetIndex].Merges.Expand(_selection.Range),
            WriteSelection = range =>
            {
                _selection.MoveTo(range.Start);
                _selection.ExtendTo(range.End);
            },
            ReadViewOption = key => key switch
            {
                "formulas" => _grid.ShowFormulas,
                "gridLines" => _grid.ShowGridLines,
                "values" => _grid.HighlightValues,
                "rowColumnHighlight" => _grid.HighlightRowAndColumn,
                "hiddenMarkers" => _grid.ShowHiddenMarkers,
                "pageBreaks" => _grid.ShowPageBreaks,
                "snapToGrid" => _grid.SnapToGrid,

                _ => false,
            },
            WriteViewOption = (key, value) =>
            {
                switch (key)
                {
                    case "formulas": _grid.ShowFormulas = value; break;
                    case "gridLines": _grid.ShowGridLines = value; break;
                    case "values": _grid.HighlightValues = value; break;
                    case "rowColumnHighlight": _grid.HighlightRowAndColumn = value; break;
                    case "hiddenMarkers": _grid.ShowHiddenMarkers = value; break;
                    case "pageBreaks": _grid.ShowPageBreaks = value; break;
                    case "snapToGrid": _grid.SnapToGrid = value; break;
                }
            },
            Refresh = RefreshEverything,
            FreezePanes = (rows, columns) => _grid.FreezePanes(rows, columns),
            SplitPanes = (rows, columns) => _grid.SplitPanes(rows, columns),
            ReadPanesSplit = () => _grid.Geometry.PanesAreSplit,
            ApplyRowFilter = (sheet, hidden) => _grid.ApplyRowFilter(sheet, hidden),
            ReadZoom = () => _grid.Zoom,
            WriteZoom = zoom => _grid.Zoom = zoom,
            ReadSelectedObject = () => _grid.SelectedObject,
            ReadSelectedObjects = () => _grid.SelectedObjects,
            WriteSelectedObjects = objects => _grid.SelectObjects(objects),
            SelectSheet = index => SelectSheet(index),
            WriteSelectedObject = placed => _grid.SelectObject(placed),
            BeginReferencePick = BeginReferencePick,
            ShapeText = _shapeText.Facade(),
            FocusArea = _editing.FocusArea,
        };

    private ISheetReferencePickSink? _referencePick;

    private IDisposable BeginReferencePick(ISheetReferencePickSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        if (_editing.IsEditing) _editing.CommitEditor();
        _editing.CommitFormulaBar();

        _referencePick = sink;
        _grid.SelectObject(null);
        _grid.PickingReference = true;
        _editing.FormulaBox.IsEnabled = false;

        return new ReferencePickMode(() =>
        {
            if (!ReferenceEquals(_referencePick, sink)) return;

            _referencePick = null;
            _grid.PickingReference = false;
            _editing.FormulaBox.IsEnabled = true;
        });
    }

    private sealed class ReferencePickMode(Action end) : IDisposable
    {
        private bool _ended;

        public void Dispose()
        {
            if (_ended) return;
            _ended = true;
            end();
        }
    }

    private void BuildSheetTabs() => _sheetTabs.Show(_workbook.Sheets, _grid.SheetIndex);

    public bool FormulaBarVisible
    {
        get => _editing.FormulaBox.IsVisible;
        set
        {
            _editing.FormulaBox.IsVisible = value;
            _editing.NameBox.IsVisible = value;
        }
    }

    public bool SheetTabsVisible
    {
        get => _sheetTabs.IsVisible;
        set => _sheetTabs.IsVisible = value;
    }

    public bool HeadersVisible
    {
        get => _grid.ShowHeaders;
        set => _grid.ShowHeaders = value;
    }

    private void RefreshEverything()
    {
        if (_grid.SheetIndex >= _workbook.Sheets.Count)
        {
            _grid.SheetIndex = Math.Max(0, _workbook.Sheets.Count - 1);
            _sheetContext.Attach(CurrentTarget());
        }

        BuildSheetTabs();

        _rowTracker?.Flush();
        _operations.ApplyRequestedStyles();
        OnSelectionChanged();
        _grid.Refresh();
    }

    private void SelectSheet(int index)
    {
        if (index < 0 || index >= _workbook.Sheets.Count) return;
        _editing.CommitEditor();
        _editing.CommitFormulaBar();

        _grid.SheetIndex = index;
        _sheetTabs.Show(_workbook.Sheets, index);

        _sheetContext.Attach(CurrentTarget());
        OnSelectionChanged();
        PublishSelectionContext();
        _referencePick?.SelectionChanged();
    }

    private void OnSelectionChanged()
    {
        var sheet = _workbook.Sheets[_grid.SheetIndex];
        _editing.ShowSelection(sheet);
        _segments.UpdateSummary(sheet);
    }

    public void FocusDocument() => _grid.Focus();

    public IReadOnlyList<PulseOffice.UI.Controls.Navigation.IWindowRegion> Regions =>
    [
        new PulseOffice.UI.Controls.Navigation.DelegateWindowRegion(
            () => _editing.NameBox.IsEffectivelyVisible,
            () => _editing.NameBox.IsKeyboardFocusWithin || _editing.FormulaBox.IsKeyboardFocusWithin,
            () =>
            {
                _editing.FocusArea(SheetInputArea.NameBox);
                return true;
            }),
        new PulseOffice.UI.Controls.Navigation.DelegateWindowRegion(
            () => _grid.IsEffectivelyVisible,
            () => _grid.IsKeyboardFocusWithin || _editing.CellEditor.IsKeyboardFocusWithin || _shapeText.Editor.IsKeyboardFocusWithin,
            () => _grid.Focus(NavigationMethod.Tab)),
        new PulseOffice.UI.Controls.Navigation.DelegateWindowRegion(
            () => _sheetTabs.IsEffectivelyVisible, () => _sheetTabs.IsKeyboardFocusWithin, _sheetTabs.FocusFromKeyboard),
    ];
}
