using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;
using System;
using System.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckView : UserControl, PulseOffice.Shell.IPendingEditsOwner, PulseOffice.UI.Controls.Services.IDocumentFocusTarget, PulseOffice.UI.Controls.Navigation.IDocumentRegions, IDeckViewHost
{
    private readonly DeckViewState _state;
    private readonly Presentation _deck;
    private readonly DeckActions _actions;
    private readonly ITextCatalog _texts;
    private readonly SlideView _canvas;
    private readonly DeckRulers _rulers;
    private readonly DeckViewOptions _options = new();
    private readonly TextBox _notesPane = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, MaxHeight = 220, IsVisible = false };
    private readonly Panel _modeHost = new();
    private readonly IActiveDeckContext _deckContext;
    private readonly IMasterFactory _masters;
    private readonly IDeckConnectors _connectors;
    private readonly IDeckDrawingState _drawing;
    private readonly PulseOffice.UI.Controls.Forms.IFormDesignMode? _design;
    private readonly PulseOffice.Document.Forms.IFormControlInteraction? _forms;
    private readonly PulseOffice.UI.Controls.Shortcuts.TypingChordGuard _chords = new();
    private readonly ControlAncestry _ancestry = new();

    private readonly DeckTextCorrection _correction;
    private readonly DeckAnimationPlayer _player;
    private readonly DeckColorBar _colorBar;
    private readonly DeckProofing _proofing;
    private readonly DeckFilmstrip _filmstrip;
    private readonly DeckShapeTextEditing _shapes;
    private readonly DeckTextEditing _text;
    private readonly DeckStageNavigation _navigation;
    private readonly DeckCanvasMenus _menus;
    private readonly DeckCanvasEditing _canvasEditing;
    private readonly DeckStageKeys _stageKeys;
    private readonly DeckInspector _inspector;
    private readonly DeckModeViews _modes;
    private readonly DeckObjectInsertion _insertion;

    private Control? _railPanel;
    private Control? _normalLayout;
    private IReadOnlyList<PulseOffice.UI.Controls.Navigation.IWindowRegion> _regions = [];
    private string? _lastEditedId;
    private bool _formsStarted;

    public PulseOffice.UI.Controls.Services.IRibbonStateNotifier? RibbonState { get; init; }

    public ObjectDialogs.IDeckObjectEditing? ObjectEditing { get; init; }

    public PulseOffice.Text.Spelling.Dictionaries.IDictionaryChanges? DictionaryChanges { get; init; }

    public IRichTextInsertion? RichInsertion { get; init; }

    public PulseOffice.Presentations.UI.Motion.IDeckMotionEditors? MotionEditors
    {
        get => _motionEditors;
        init
        {
            _motionEditors = value;
            _inspector.BuildInspector();
        }
    }

    private readonly PulseOffice.Presentations.UI.Motion.IDeckMotionEditors? _motionEditors;

    public PulseOffice.Presentations.UI.Motion.TransitionEditor? TransitionPanelEditor => _inspector.TransitionPanelEditor;

    public PulseOffice.Presentations.UI.Motion.AnimationPane? AnimationPanelPane => _inspector.AnimationPanelPane;

    public DeckView(
        Presentation deck, DeckActions actions, ISlideRenderer renderer,
        IMasterFactory masters, IAnimationScheduler scheduler, ITextCatalog texts,
        PulseOffice.UI.Controls.Services.IContextMenuFactory contextMenus,
        IActiveDeckContext deckContext,
        IDeckConnectors connectors,
        IDeckDrawingState drawing,
        PulseOffice.UI.Controls.Objects.IRasterImageFiles images,
        ICanvasContextChooser contextChooser,
        PulseOffice.Text.Spelling.IAutoCorrect? autoCorrect = null,
        PulseOffice.Text.Spelling.IAutoCorrectSettings? autoCorrectSettings = null,
        IDeckSpelling? spelling = null,
        PulseOffice.Text.Spelling.IAutoSpellSettings? autoSpell = null,
        PulseOffice.Text.Spelling.IUserDictionary? userWords = null,
        PulseOffice.UI.Controls.Forms.IFormDesignMode? design = null,
        PulseOffice.Document.Forms.IFormControlInteraction? forms = null,
        PulseOffice.UI.Controls.Text.Editing.InlineEditingServices? editing = null,
        IRichTextFormatter? formatter = null,
        PulseOffice.UI.Controls.Zoom.IZoomLadder? zoomLadder = null,
        PulseOffice.UI.Controls.Text.Rich.ITextCaretGeometry? caretGeometry = null,
        ICaretWord? caretWord = null,
        PulseOffice.UI.Controls.Text.Rich.ITextFrameComposer? composer = null,
        PulseOffice.UI.Controls.Text.ITextEditingFocus? editingFocus = null,
        IHandoutLayout? handoutLayout = null,
        PulseOffice.UI.Controls.Services.IDocumentKeyScope? keys = null,
        ISlideFactory? masterSlides = null,
        IPlainTextPositions? plainPositions = null,
        ITextEditSplice? splice = null,
        PulseOffice.Drawing.Shapes.IShapeRichText? shapeRichText = null,
        ISlideKeyNavigation? slideKeys = null,
        PulseOffice.Drawing.Shapes.IShapeTextEditing? shapeTextEditing = null,
        PulseOffice.UI.Controls.Services.IContextMenuKey? menuKey = null,
        PulseOffice.UI.Controls.Rulers.IParagraphRuler? paragraphRuler = null,
        PulseOffice.UI.Controls.Shapes.IObjectNudgeKeys? nudges = null,
        PulseOffice.Drawing.Assets.IImageExtents? extents = null)
    {
        _masters = masters;
        _connectors = connectors;
        _chords.Attach(this);
        var keyNavigation = slideKeys ?? new SlideKeyNavigation();
        var focus = editingFocus ?? new PulseOffice.UI.Controls.Text.TextEditingFocus();
        var word = caretWord ?? new CaretWord();
        _deck = deck;
        _actions = actions;
        _texts = texts;
        _drawing = drawing;
        _design = design;
        _forms = forms;
        _deckContext = deckContext;
        _canvas = new SlideView(renderer) { Size = deck.Size, Editable = true, Grid = deck.Grid, Deck = deck, Focusable = true, FocusAdorner = null };
        _state = new DeckViewState(deck, actions, texts, _canvas, _options);

        _correction = new DeckTextCorrection(_state, autoCorrect, autoCorrectSettings, spelling);
        _player = new DeckAnimationPlayer(_state, scheduler);
        _colorBar = new DeckColorBar(_state);
        _proofing = new DeckProofing(_state, spelling, autoSpell, userWords);
        var stillImages = new PulseOffice.UI.Controls.Objects.StillImageProbe();
        _filmstrip = new DeckFilmstrip(_state, this, renderer, keyNavigation, contextMenus, stillImages);
        _shapes = new DeckShapeTextEditing(_state, this, _filmstrip,
            new PulseOffice.UI.Controls.Shapes.ShapeTextEditor(shapeTextEditing ?? new PulseOffice.Drawing.Shapes.ShapeTextEditing(), new PulseOffice.Drawing.Shapes.ShapeTextInk(), new PulseOffice.UI.Controls.Shapes.ShapeBrushes(), focus, word),
            shapeRichText ?? new PulseOffice.Drawing.Shapes.ShapeRichText());
        _rulers = new DeckRulers(new DeckRulerSources(_canvas, () => _state.Target, () => _deck.Grid.ShowRulers && _state.Mode == DeckViewMode.Normal, formatter ?? new RichTextFormatter(), paragraphRuler ?? new PulseOffice.UI.Controls.Rulers.ParagraphRuler()));
        _text = new DeckTextEditing(_state, this, _filmstrip, _shapes, _correction, _rulers, _ancestry,
            composer ?? new PulseOffice.UI.Controls.Text.Rich.TextFrameComposer(new ListLabels()),
            caretGeometry ?? new PulseOffice.UI.Controls.Text.Rich.TextCaretGeometry(),
            plainPositions ?? new PlainTextPositions(),
            editing ?? new PulseOffice.UI.Controls.Text.Editing.InlineEditingServices(new CaretRowNavigation(), new PulseOffice.Text.Input.PreeditComposer(), null),
            splice ?? new TextEditSplice(), word, focus);
        _navigation = new DeckStageNavigation(_state, this, zoomLadder ?? new PulseOffice.UI.Controls.Zoom.ZoomLadder());
        _menus = new DeckCanvasMenus(_state, this, _text, _shapes, _filmstrip, _ancestry, contextMenus, contextChooser,
            menuKey ?? new PulseOffice.UI.Controls.Services.ContextMenuKey(), drawing);
        _canvasEditing = new DeckCanvasEditing(_state, this, connectors);
        _stageKeys = new DeckStageKeys(_state, this, _text, _shapes, _canvasEditing, drawing, keyNavigation,
            keys ?? new PulseOffice.UI.Controls.Services.DocumentKeyScope(), _chords, nudges ?? new PulseOffice.UI.Controls.Shapes.ObjectNudgeKeys());
        _inspector = new DeckInspector(_state, this, _player, new PulseOffice.Presentations.UI.Motion.DeckElementNames(_state.Texts));
        _modes = new DeckModeViews(_state, this, renderer,
            new DeckMasterViews(_state, this, renderer, handoutLayout ?? new HandoutLayout(), masterSlides ?? new SlideFactory(), new DeckChoiceNames(_state.Texts),
                new Design.LayoutChoices(new DeckChoiceNames(_state.Texts), actions.Layouts.Catalog)), _correction, stillImages);
        _insertion = new DeckObjectInsertion(_state, this, images, extents ?? new PulseOffice.Drawing.Assets.ImageExtents(), _modeHost);

        AddHandler(KeyDownEvent, _menus.OnMenuKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _text.Attach();
        _shapes.Attach();
        _canvas.SelectionChanged += OnCanvasSelectionChanged;
        Avalonia.Automation.AutomationProperties.SetName(_rulers.Horizontal, texts["ruler.horizontal"]);
        Avalonia.Automation.AutomationProperties.SetName(_rulers.Vertical, texts["ruler.vertical"]);
        _canvas.SelectionChanged += (_, _) => _rulers.Update();
        _canvasEditing.Attach();
        _canvas.Drawing = _drawing;

        Content = Build();

        _deck.Changed += (_, _) =>
        {
            _lastEditedId = _state.Current?.Id;
            Sync();
        };
        _actions.SlideFocusRequested += (_, slide) =>
        {
            _lastEditedId = slide.Id;
            Select(slide);
        };

        _state.Current = _deck.Slides.Count > 0 ? _deck.Slides[0] : null;
        Sync();
    }

    public void CommitPendingEdits()
    {
        _text.CommitEditor();
        if (_state.Current is { } slide && _notesPane.IsVisible) _actions.SetNotes(slide, _notesPane.Text ?? string.Empty);
        _modes.CommitPendingEdits(_state.Current);
    }

    private Control Build()
    {
        var railPanel = _filmstrip.BuildRail();
        var metrics = _state.Metrics;

        var stage = new Panel { Background = Brushes.Transparent, Children = { _canvas, _state.Overlay } };
        stage.DoubleTapped += OnCanvasDoubleTapped;
        stage.AddHandler(PointerPressedEvent, OnStagePressed,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);

        var stageScroll = new ScrollViewer
        {
            Content = stage,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        _navigation.Scroll = stageScroll;
        stageScroll.AddHandler(PointerWheelChangedEvent, _navigation.OnStageWheel,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);

        var stageColumn = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto") };
        _colorBar.BuildColorBar();
        Grid.SetRow(_colorBar.Panel, 3);
        stageColumn.Children.Add(_colorBar.Panel);
        var ruled = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetColumn(_rulers.Horizontal, 1);
        Grid.SetRow(_rulers.Vertical, 1);
        Grid.SetRow(stageScroll, 1);
        Grid.SetColumn(stageScroll, 1);
        ruled.Children.Add(_rulers.Horizontal);
        ruled.Children.Add(_rulers.Vertical);
        ruled.Children.Add(stageScroll);
        Grid.SetRow(ruled, 1);
        Grid.SetRow(_notesPane, 2);
        _notesPane.PlaceholderText = _texts["deck.notes.placeholder"];
        _notesPane.Margin = new Thickness(0, metrics.SpacingMedium, 0, 0);
        _notesPane.LostFocus += (_, _) =>
        {
            if (_state.Current is { } slide) _actions.SetNotes(slide, _notesPane.Text ?? string.Empty);
        };
        _options.Changed += (_, _) => ApplyOptions();
        _options.ZoomToSelection = _navigation.ZoomToSelection;
        stage.PointerMoved += _navigation.OnStagePointerMoved;
        stage.PointerReleased += (_, _) => _navigation.EndPan();
        stageColumn.Children.Add(ruled);
        stageColumn.Children.Add(_notesPane);

        var stageHost = new Border
        {
            Child = stageColumn,
            Padding = new Thickness(metrics.SpacingLarge),
        }.Themed(Border.BackgroundProperty, Palette.Keys.Canvas);

        var inspectorPanel = _inspector.BuildPanel();

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        _railPanel = railPanel;
        Grid.SetColumn(railPanel, 0);
        Grid.SetColumn(stageHost, 1);
        Grid.SetColumn(inspectorPanel, 2);
        layout.Children.Add(railPanel);
        layout.Children.Add(stageHost);
        layout.Children.Add(inspectorPanel);

        _normalLayout = layout;
        _ = new PulseOffice.UI.Controls.Navigation.KeyboardFocusFrame(stageHost, _canvas);
        _regions =
        [
            new PulseOffice.UI.Controls.Navigation.DelegateWindowRegion(
                () => railPanel.IsEffectivelyVisible && _filmstrip.CurrentThumbnailFrame is not null,
                () => railPanel.IsKeyboardFocusWithin,
                () =>
                {
                    if (_filmstrip.CurrentThumbnailFrame is not { } current) return false;
                    current.BringIntoView();
                    return current.Focus(NavigationMethod.Tab);
                }),
            new PulseOffice.UI.Controls.Navigation.DelegateWindowRegion(() => stageHost.IsEffectivelyVisible, () => stageHost.IsKeyboardFocusWithin, () =>
            {
                if (_shapes.Editor.IsEditing || _text.Editor is not null) FocusDocument();
                else _canvas.Focus(NavigationMethod.Tab);
                return true;
            }),
            new PulseOffice.UI.Controls.Navigation.ControlWindowRegion(inspectorPanel),
        ];
        _modeHost.Children.Add(layout);
        return _modeHost;
    }

    public void SetViewMode(DeckViewMode mode)
    {
        if (_state.Mode == mode) return;
        _state.Mode = mode;

        _modeHost.Children.Clear();
        _modeHost.Children.Add(_modes.Build(mode, _normalLayout!));

        AttachContext();
    }

    public DeckViewMode ViewMode => _state.Mode;

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _state.Target = BuildTarget();
        _deckContext.Attach(_state.Target);
        _drawing.Changed += OnDrawingStateChanged;
        _texts.LanguageChanged += OnLanguageChanged;
        _proofing.StartProofing(DictionaryChanges);
        StartForms();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _filmstrip.RefreshCaption();
        _inspector.ResetMotion();
    }

    private void OnDrawingStateChanged(object? sender, EventArgs e) => Reannounce();

    private void Reannounce()
    {
        if (_state.Target is not null) _deckContext.Reannounce(_state.Target);
    }

    private void StartForms()
    {
        _canvas.Forms = _forms;
        if (_design is null) return;
        var starting = !_formsStarted;
        _formsStarted = true;
        _design.Changed += OnDesignChanged;
        OnDesignChanged(this, EventArgs.Empty);

        if (starting && !_design.IsDesigning && !_deck.FormSettings.OpenInDesignMode && _deck.FormSettings.AutomaticFocus && _state.Current is { } slide
            && slide.Elements.OfType<ObjectElement>()
                .Where(element => element.Content is PulseOffice.Document.Forms.FormControlObject)
                .OrderBy(element => ((PulseOffice.Document.Forms.FormControlObject)element.Content).Properties.TabIndex)
                .FirstOrDefault() is { } first)
            _state.Target?.Select([first]);
    }

    private void OnDesignChanged(object? sender, EventArgs e)
    {
        _canvas.DesignMode = _design?.IsDesigning ?? true;
        _canvas.InvalidateVisual();
        Reannounce();
    }

    private DeckTarget BuildTarget() => new(
        _deck, _actions,
        () => _state.Current,
        () => CurrentSlideIndex,
        () => _player.TogglePlay(),
        _text.ApplyTextFormat,
        () => _state.Editing is not null || _state.SelectedText is not null || _shapes.Editor.IsEditing || _state.Selected is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject { Text.Length: > 0 } },
        () => _state.Selected,
        () => _canvas.Selection,
        SelectElements,
        GoToSlide,
        () => _text.Editor?.SelectedText is { Length: > 0 } s ? s : null,
        _insertion.InsertFormulaAsync,
        SetViewMode,
        () => _state.Mode,
        LastEditedIndex,
        () => _canvas.Zoom,
        _navigation.SetZoom,
        () => _canvas.EffectiveZoom,
        () => _text.Editor is not null || _shapes.Editor.IsEditing,
        _text.TypeAtCaret,
        _options,
        zoom => _navigation.SetZoom(zoom, record: false),
        (data, name) => _insertion.InsertImageBytes(data, name),
        (data, name, link) => _insertion.InsertImageBytes(data, name, link),
        _text.CommitSelection,
        () => _text.LiveEditing() ?? (_state.SelectedText is { } chosen ? _state.Current?.Find(chosen.Id) as TextElement : null),
        _text.EditorRange)
    {
        TextCaret = () => _text.Editor?.CaretIndex,
        MasterLayoutId = () => _state.MasterLayoutId,
        SelectMasterLayout = id =>
        {
            _state.MasterLayoutId = id;
            if (_state.Mode == DeckViewMode.Master) RefreshMode();
        },
        ShapeText = _shapes.ShapeRichTextInFocus,
        ShapeRange = () => _shapes.Editor.PendingProbe() is { } probe ? (probe.Start, probe.End) : _shapes.Editor.IsEditing ? _shapes.Editor.Selection : null,
        InsertParagraphsAtCaret = _text.InsertParagraphsAtCaret,
        EditText = id =>
        {
            if (_state.Current?.Find(id) is TextElement element) _text.BeginEdit(element);
        },
    };

    private int? LastEditedIndex() =>
        _lastEditedId is { } id && _deck.Slides.FirstOrDefault(slide => slide.Id == id) is { } slide
            ? _deck.IndexOf(slide)
            : null;

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _player.StopPlay();
        if (_state.Target is not null) _deckContext.Detach(_state.Target);
        _drawing.Changed -= OnDrawingStateChanged;
        _texts.LanguageChanged -= OnLanguageChanged;
        _proofing.StopProofing(DictionaryChanges);
        if (_design is not null) _design.Changed -= OnDesignChanged;
    }

    public int CurrentSlideIndex => _state.Current is null ? 0 : Math.Max(0, _deck.IndexOf(_state.Current));

    public void ApplyReadOnly() => _inspector.ApplyReadOnly();

    private void Sync()
    {
        if (_state.Current is null || _deck.IndexOf(_state.Current) < 0)
            _state.Current = _deck.Slides.Count > 0 ? _deck.Slides[0] : null;

        if (_state.Current is not null) _connectors.Reroute(_state.Current);

        _state.Syncing = true;

        _canvas.Size = _deck.Size;
        _canvas.DeckTheme = _deck.Theme;
        _canvas.Master = _deck.Master;

        _state.Syncing = false;

        _filmstrip.BuildFilmstrip();
        _inspector.BuildInspector();
        ApplyReadOnly();
        var survivors = _canvas.Selection
            .Select(s => _state.Current?.Find(s.Id))
            .OfType<SlideElement>()
            .ToList();
        _state.Selected = _state.Selected is null ? null : _state.Current?.Find(_state.Selected.Id);
        _state.SelectedText = _state.Selected as TextElement;
        if (survivors.Count > 1)
            _canvas.SetSelection(survivors, _state.Selected ?? survivors[^1]);
        else
            _canvas.SelectedElement = _state.Selected;
        _canvas.Chrome = _state.ChromeFor(_state.Current);
        _canvas.Fields = new PulseOffice.Presentations.SlideFieldsFactory().For(_deck, _state.Current);
        _canvas.Show(_state.Current);
        if (_state.Editing is not null && _state.Current?.Find(_state.Editing.Id) is null) _text.CommitEditor();
        if (_shapes.Editor.EditingId is { } shapeId && _state.Current?.Find(shapeId) is null) _shapes.Editor.Commit();
        if (_player.PlaysOther(_state.Current)) _player.StopPlay();
        if (!_notesPane.IsKeyboardFocusWithin) _notesPane.Text = _state.Current?.Notes ?? string.Empty;

        if (_state.Mode != DeckViewMode.Normal) RefreshMode();
        _rulers.Update();
    }

    private void RefreshMode()
    {
        _modeHost.Children.Clear();
        _modeHost.Children.Add(_modes.Build(_state.Mode, _normalLayout!));
    }

    private void ApplyOptions()
    {
        if (_railPanel is not null) _railPanel.IsVisible = _options.SlidePane;
        if (_notesPane.IsVisible && !_options.NotesPane && _state.Current is { } noted)
            _actions.SetNotes(noted, _notesPane.Text ?? string.Empty);
        _notesPane.IsVisible = _options.NotesPane;
        _colorBar.Panel.IsVisible = _options.ColorBar;
        if (!_notesPane.IsKeyboardFocusWithin) _notesPane.Text = _state.Current?.Notes ?? string.Empty;
        _canvas.ShowComments = _options.ShowComments;
        _canvas.TableSelection = _options.TableCells;
        _canvas.Quality = _options.Quality;
        _canvas.OpenSlideGroupId = _options.OpenGroup?.SlideGroupId;
        _canvas.InvalidateVisual();
        Reannounce();
    }

    private void Select(Slide slide)
    {
        _text.CommitEditor();
        if (_notesPane.IsVisible && _state.Current is { } previous) _actions.SetNotes(previous, _notesPane.Text ?? string.Empty);
        _state.Current = slide;
        _notesPane.Text = slide.Notes;
        Sync();
        AttachContext();
    }

    private void OnStagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed && _menus.OpenCanvasMenu(e)) return;
        if (_navigation.HandleTool(e)) return;

        if (_shapes.Editor.IsEditing && !(e.Source is Control pressed && (ReferenceEquals(pressed, _shapes.Editor) || _ancestry.IsInside(pressed, _shapes.Editor))))
            _shapes.Editor.Commit();

        if (_text.Editor is not null)
        {
            if (e.Source is Control source && _text.InEditor(source))
                return;
            _text.CommitEditor();
        }

        if (_text.Editor is null) _canvas.Focus();
    }

    public IReadOnlyList<PulseOffice.UI.Controls.Navigation.IWindowRegion> Regions => _state.Mode == DeckViewMode.Normal ? _regions : [new PulseOffice.UI.Controls.Navigation.ControlWindowRegion(this)];

    public void FocusDocument()
    {
        if (_shapes.Editor.IsEditing) _shapes.Editor.Box.Focus();
        else if (_text.Editor is not null) _text.Editor.Focus();
        else _canvas.Focus();
    }

    private void OnCanvasSelectionChanged(object? sender, SlideElement? element)
    {
        _state.Selected = element;
        _state.SelectedText = element as TextElement;
        AttachContext();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _stageKeys.TextInput(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _stageKeys.KeyDown(e);
    }

    private void GoToSlide(int index)
    {
        if (_deck.Slides.Count == 0) return;
        Select(_deck.Slides[Math.Clamp(index, 0, _deck.Slides.Count - 1)]);
    }

    private void SelectElements(IReadOnlyList<SlideElement> elements)
    {
        if (_state.Current is null) return;
        var live = elements.Select(e => _state.Current.Find(e.Id)).OfType<SlideElement>().ToList();
        _state.Selected = live.Count > 0 ? live[^1] : null;
        _state.SelectedText = _state.Selected as TextElement;

        if (live.Count > 1) _canvas.SetSelection(live, _state.Selected);
        else _canvas.SelectedElement = _state.Selected;

        AttachContext();
        Focus();
    }

    private void OnCanvasDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_state.Current is null) return;
        if (e.Source is Control pressed && _text.InEditor(pressed)) return;

        var point = e.GetPosition(_canvas);
        var top = _canvas.ElementAt(point);
        if (top is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject } shapeElement && !_actions.IsReadOnly)
        {
            _shapes.BeginShapeText(shapeElement, null);
            e.Handled = true;
            return;
        }

        if (top is ObjectElement)
        {
            if (!_actions.IsReadOnly && ObjectEditing?.TryEditSelection() == true) e.Handled = true;
            return;
        }

        if (top is TextElement element) _text.BeginEdit(element);
    }

    private void AttachContext()
    {
        if (_state.Target is not null) _deckContext.Attach(_state.Target);
    }

    private IBrush Accent() => this.FindResource("PulseAccent") as IBrush ?? Brushes.OrangeRed;

    Control IDeckViewHost.View => this;

    IBrush IDeckViewHost.Accent() => Accent();

    void IDeckViewHost.AttachContext() => AttachContext();

    void IDeckViewHost.NotifyRibbon() => RibbonState?.Notify();

    TextElement? IDeckViewHost.LiveEditing() => _text.LiveEditing();

    TextOffsetMap IDeckViewHost.CommitEditor() => _text.CommitEditor();

    void IDeckViewHost.Reannounce() => Reannounce();

    void IDeckViewHost.RefreshMode() => RefreshMode();

    void IDeckViewHost.Select(Slide slide) => Select(slide);

    void IDeckViewHost.GoToSlide(int index) => GoToSlide(index);

    void IDeckViewHost.SelectElements(IReadOnlyList<SlideElement> elements) => SelectElements(elements);
}
