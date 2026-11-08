using PulseOffice.Foundation.Zoom;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using PulseOffice.Documents.UI.Canvas;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Text;
using PulseOffice.UI.Controls.Text.Editing;
using PulseOffice.UI.Theming;
using PulseOffice.UI.Controls.Theming;
using Avalonia.VisualTree;

namespace PulseOffice.Documents.UI;

public sealed record FolioViewServices(
    FolioCanvasServices Canvas,
    IActiveFolioContext Context,
    IDocumentWorkspace Workspace,
    IRibbonStateNotifier RibbonState,
    IFolioKeyMap Keys,
    IFolioContextMenus Menus,
    IFolioImages Images,
    FolioRulerServices Rulers,
    PulseOffice.Documents.Editing.IStyleEditing Styles,
    IFolioDrawingPlacement Drawing,
    IFolioDrawingTools Tools,
    PulseOffice.Documents.Editing.IDirectCursor DirectCursor,
    IFolioCaretSpeech Speech,
    IFolioObjectCycle Cycle,
    IFolioEditRefusals Refusals,
    PulseOffice.UI.Controls.Shapes.IObjectNudgeKeys Nudges,
    IFolioEditingKeys EditingKeys);

public sealed class FolioView : Grid, IPendingEditsOwner, IDocumentFocusTarget, ITextEditingTarget, IInlineImeSurface
{
    private readonly FolioEditor _editor;
    private static readonly PulseOffice.UI.Controls.Shapes.NudgeSteps PointSteps = new(1, 10);

    private readonly FolioViewServices _services;
    private readonly FolioCanvas _canvas;
    private readonly TextInputSink _sink = new();
    private readonly InlineImeClient _ime;
    private readonly FolioTarget _target;
    private readonly FolioRulers _rulers;
    private readonly ScrollViewer _scroll;

    public FolioView(FolioEditor editor, FolioViewServices services)
    {
        _editor = editor;
        _services = services;
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        _canvas = new FolioCanvas(editor, services.Canvas, scroll)
            .Themed(FolioCanvas.DeskProperty, Palette.Keys.Canvas);
        _canvas.Bind(FolioCanvas.AccentProperty, _canvas.GetResourceObservable("PulseAccent"));
        scroll.Content = _canvas;
        Avalonia.Automation.AutomationProperties.SetName(_sink, services.Canvas.Texts["folio.surface.name"]);
        _sink.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        _sink.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        _rulers = new FolioRulers(editor, _canvas, scroll, services.Rulers);
        Avalonia.Automation.AutomationProperties.SetName(_rulers.Horizontal, services.Canvas.Texts["ruler.horizontal"]);
        Avalonia.Automation.AutomationProperties.SetName(_rulers.Vertical, services.Canvas.Texts["ruler.vertical"]);
        _scroll = scroll;
        RowDefinitions = new RowDefinitions("Auto,*");
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        SetRow(_sink, 1);
        SetColumn(_sink, 1);
        SetRow(scroll, 1);
        SetColumn(scroll, 1);
        SetColumn(_rulers.Horizontal, 1);
        SetRow(_rulers.Vertical, 1);
        Children.Add(_sink);
        Children.Add(scroll);
        Children.Add(_rulers.Horizontal);
        Children.Add(_rulers.Vertical);
        services.Rulers.Layout.Changed += (_, _) => ApplyScrollBars();
        ApplyScrollBars();
        _ime = new InlineImeClient(this);

        _target = new FolioTarget(editor, () => _canvas.Zoom, SetZoom, () => _canvas.Scale / PageDesk.PointsToPixels, CopyAsync, CutAsync, PasteAsync);

        _canvas.PointerPlaced += (_, _) => FocusDocument();
        _canvas.MenuRequested += (_, _) => services.Menus.Open(editor, _canvas, FocusDocument);
        _canvas.RegionRequested += (_, offer) => AddRegion(offer);
        _canvas.DirectCursorRequested += (_, target) => PlaceDirectCursor(target);
        _canvas.Drawn += (_, sketch) => services.Drawing.Place(editor, sketch, services.Tools.Connector);
        _sink.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _sink.AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        _sink.AddHandler(TextInputMethodClientRequestedEvent, OnClientRequested, RoutingStrategies.Bubble, handledEventsToo: true);
        _sink.GotFocus += (_, _) =>
        {
            Keyboard(true);
            Speak();
        };
        _sink.LostFocus += (_, _) =>
        {
            SetPreedit(null, null);
            Keyboard(false);
        };
        editor.Changed += OnEditorChanged;
        editor.Refused += (_, verdict) => _services.Refusals.Report(verdict);
        editor.PreeditChanged += (_, _) =>
        {
            _canvas.Refresh();
            _canvas.EnsureCaretVisible();
        };
    }

    public FolioEditor Editor => _editor;

    public FolioRulers Rulers => _rulers;

    private void AddRegion(FolioCanvas.RegionOffer offer)
    {
        if (offer.Page >= _editor.Layout.Pages.Count || _editor.RefusesDocumentEdit()) return;
        var name = _editor.Layout.Pages[offer.Page].StyleName;
        _editor.Change(() => _services.Styles.Change(_editor.Document, _editor.History, _editor.Document.Styles.Page, name,
            style => (offer.Header ? style.Header : style.Footer).Enabled = true, offer.Header ? "folio.page.header" : "folio.page.footer"));
    }

    private void ApplyScrollBars()
    {
        var layout = _services.Rulers.Layout;
        _scroll.HorizontalScrollBarVisibility = layout.HorizontalScrollBar ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto : Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        _scroll.VerticalScrollBarVisibility = layout.VerticalScrollBar ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto : Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        _rulers.Update();
    }

    public FolioCanvas Canvas => _canvas;

    public TextBox InputSink => _sink;

    public Visual TextViewVisual => _canvas;

    public Rect CaretRectangle => _canvas.CaretRectangle;

    public void SetPreedit(string? text, int? cursor) => _editor.SetPreedit(text, cursor);

    public void CommitPendingEdits() => SetPreedit(null, null);

    public void FocusDocument() => _sink.Focus();

    public void SetZoom(ViewZoom zoom)
    {
        _canvas.SetZoom(zoom);
        Announce();
    }

    public void Perform(TextEditingAction action)
    {
        switch (action)
        {
            case TextEditingAction.SelectAll:
                _editor.SelectAll();
                break;
            case TextEditingAction.Copy:
                _ = CopyAsync();
                break;
            case TextEditingAction.Cut:
                _ = CutAsync();
                break;
            case TextEditingAction.Paste:
                _ = PasteAsync();
                break;
            case TextEditingAction.Undo:
                _services.EditingKeys.Undo(_editor);
                break;
            case TextEditingAction.Redo:
                _services.EditingKeys.Redo(_editor);
                break;
        }
    }

    public Task CopyAsync() => _services.EditingKeys.CopyAsync();

    public Task CutAsync() => _services.EditingKeys.CutAsync();

    public Task PasteAsync() => _services.EditingKeys.PasteAsync();

    private void PlaceDirectCursor(PulseOffice.Documents.Editing.DirectCursorTarget target)
    {
        if (_editor.RefusesEdit(target.AfterParagraphId)) return;
        var position = _services.DirectCursor.Place(_editor.Document, _editor.History, target);
        _editor.Reflow();
        _editor.PlaceCaret(position, extend: false);
        FocusDocument();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _services.Context.Attach(_target);
        Announce();
        Avalonia.Threading.Dispatcher.UIThread.Post(FocusOnArrival, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void FocusOnArrival()
    {
        if (Attached()) FocusDocument();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _services.Context.Detach(_target);
        base.OnDetachedFromVisualTree(e);
    }

    private void Keyboard(bool focused)
    {
        _canvas.HasKeyboard = focused;
        _canvas.Wake();
    }

    private void OnClientRequested(object? sender, TextInputMethodClientRequestedEventArgs e)
    {
        if (e.Client is { } client && !ReferenceEquals(client, _ime)) _ime.Wrap(client);
        if (e.Client is not null) e.Client = _ime;
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrEmpty(e.Text) || e.Text.All(char.IsControl)) return;
        if (_editor.SelectedObjectId is { } selected && !_editor.EnterFrame(selected, null)) return;
        _editor.Type(e.Text);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_services.Menus.IsMenuKey(e))
        {
            e.Handled = true;
            _services.Menus.OpenAt(_editor, _canvas, _canvas.CaretRectangle, FocusDocument);
            return;
        }
        if (_services.Tools.Tool != PulseOffice.UI.Controls.Drawing.DrawingTool.None && e.Key is Key.Escape or Key.Enter)
        {
            if (e.Key == Key.Escape || !_canvas.CompleteDrawing()) _canvas.CancelDrawing();
            e.Handled = true;
            return;
        }
        if (_editor.SelectedObjectId is { } nudged && _services.Nudges.Read(e.Key, e.KeyModifiers) is { } nudge)
        {
            var (dx, dy) = nudge.Distance(PointSteps);
            _services.Canvas.Mover.Nudge(_editor, nudged, dx, dy);
            e.Handled = true;
            return;
        }
        if (_editor.SelectedObjectId is not null && e.Key == Key.Tab && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            _services.Cycle.Step(_editor, e.KeyModifiers == KeyModifiers.Shift ? -1 : 1);
            e.Handled = true;
            return;
        }
        if (_editor.SelectedObjectId is not null && e.KeyModifiers == KeyModifiers.None)
        {
            if (e.Key is Key.Delete or Key.Back)
            {
                _editor.DeleteSelectedObject();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                _editor.SelectObject(null);
                e.Handled = true;
                return;
            }
        }
        if (_services.Keys.Resolve(e.Key, e.KeyModifiers) is not { } action) return;
        e.Handled = true;
        if (e.Key is not (Key.Enter or Key.Back or Key.Delete)) _editor.Services.Assist.WithdrawCompletion(_editor);
        action.Apply(this);
    }

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        _canvas.Refresh();
        _canvas.EnsureCaretVisible();
        _ime.CaretMoved();
        Announce();
    }

    private void Announce()
    {
        if (!Attached()) return;
        var selected = _editor.HasSelection;
        var table = _editor.InTable ? SelectionKind.Table : SelectionKind.None;
        var item = _editor.SelectedObjectId is { } id ? _editor.Document.Objects.Find(id) : null;
        _services.Workspace.UpdateContext(context => context with
        {
            Selection = item is not null ? Kind(item) : (selected ? SelectionKind.TextRange : SelectionKind.TextCaret) | table,
            State = selected ? context.State | DocumentState.HasSelection : context.State & ~DocumentState.HasSelection,
        });
        _services.RibbonState.Notify();
        Speak();
    }

    private void Speak()
    {
        var spoken = _services.Speech.Describe(_editor);
        Avalonia.Automation.AutomationProperties.SetName(_sink, spoken.Name);
        Avalonia.Automation.AutomationProperties.SetHelpText(_sink, spoken.Help);
    }

    private bool Attached() => TopLevel.GetTopLevel(this) is not null;

    private SelectionKind Kind(PulseOffice.Documents.Objects.AnchoredObject item) => item.Content switch
    {
        PulseOffice.Documents.Objects.TextBoxContent => SelectionKind.Frame,
        PulseOffice.Documents.Objects.EmbeddedContent { Value: PulseOffice.Drawing.Assets.RasterImageObject } => SelectionKind.Image,
        PulseOffice.Documents.Objects.EmbeddedContent { Value: PulseOffice.Charts.ChartDefinition } => SelectionKind.Chart,
        _ => SelectionKind.Shape,
    };
}
