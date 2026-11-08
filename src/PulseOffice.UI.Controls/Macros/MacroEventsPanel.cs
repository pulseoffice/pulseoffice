using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using PulseOffice.Document.Forms;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Macros;

public sealed class MacroEventsPanel : UserControl
{
    private readonly MacroEventsContext _context;
    private readonly IMacroBindingRules _rules;
    private readonly MacroEventsChrome _chrome;
    private readonly ILogger _logger;
    private readonly List<MacroEventRow> _rows = [];
    private readonly TextBlock _status;
    private readonly TextBlock _statusHint;
    private readonly TextBlock _error;
    private readonly Border _statusCard;
    private readonly Border _empty;
    private bool _editing;

    public MacroEventsPanel(
        MacroAssignments assignments,
        IReadOnlyList<MacroEventSlot> slots,
        MacroEventsContext context,
        IMacroBindingRules rules,
        MacroEventsChrome chrome,
        ILogger logger)
    {
        Assignments = assignments ?? throw new ArgumentNullException(nameof(assignments));
        ArgumentNullException.ThrowIfNull(slots);
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _status = new TextBlock
        {
            FontSize = chrome.Metrics.FontSize,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);
        _statusHint = chrome.Description(string.Empty);
        _error = new TextBlock
        {
            FontSize = chrome.Metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);

        _statusCard = StatusCard();
        _empty = EmptyState();

        foreach (var slot in slots)
            _rows.Add(new MacroEventRow(slot, this, chrome));

        Assignments.Changed += (_, _) =>
        {
            Refresh();
            AssignmentsChanged?.Invoke(this, EventArgs.Empty);
        };

        Content = Layout();
        Refresh();
    }

    public event EventHandler? AssignmentsChanged;

    public MacroAssignments Assignments { get; }

    public bool ReadOnly => _context.ReadOnly;

    internal IReadOnlyList<DocumentMacro> Macros => _context.Library.All;

    internal MacroBindingStatus StatusOf(string? macro) => _rules.Status(macro, Macros);

    internal async Task EditAsync(string? macro)
    {
        if (_editing) return;

        _editing = true;
        _error.IsVisible = false;
        var before = Macros;

        try
        {
            _logger.LogInformation("Macro events: opening the macro editor on {Macro}", macro ?? "(none)");
            await _context.Editor.OpenAsync(macro, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Macro events: the macro editor could not be opened");
            _error.Text = _chrome.Texts.Format("macroEvents.error", ex.Message);
            _error.IsVisible = true;
        }
        finally
        {
            _editing = false;
            _rules.Reconcile(Assignments, before, Macros);
            Refresh();
        }
    }

    internal void CreateFor(MacroEventSlot slot)
    {
        if (ReadOnly) return;

        var name = _rules.ProposeName(slot.NameStem, Macros);
        _context.Library.Store(new DocumentMacro(name, MacroLanguage.JavaScript, Sample(slot)));
        Assignments.Assign(slot.Key, name);

        _ = EditAsync(name);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _context.Library.Changed += OnLibraryChanged;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _context.Library.Changed -= OnLibraryChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLibraryChanged(object? sender, EventArgs e) => Refresh();

    private string Sample(MacroEventSlot slot) =>
        _chrome.Texts.Format("macroEvents.sample.javascript", _chrome.Texts[slot.TitleKey]);

    private void Refresh()
    {
        var count = Macros.Count;
        var inviting = count == 0 && !ReadOnly;

        _status.Text = _chrome.Texts.Format("macroEvents.status.count", count.ToString("N0", CultureInfo.CurrentCulture));
        _statusHint.Text = _chrome.Texts[ReadOnly ? "macroEvents.status.readOnly" : "macroEvents.status.hint"];
        _statusCard.IsVisible = !inviting;
        _empty.IsVisible = inviting;

        foreach (var row in _rows) row.Refresh();
    }

    private Control Layout()
    {
        var list = new StackPanel();
        for (var index = 0; index < _rows.Count; index++)
        {
            if (index > 0)
                list.Children.Add(new Border { Height = 1 }.Themed(Border.BackgroundProperty, Palette.Keys.Border));
            list.Children.Add(_rows[index].View);
        }

        var root = new StackPanel { Spacing = _chrome.Metrics.SpacingMedium };
        root.Children.Add(_statusCard);
        root.Children.Add(_empty);
        root.Children.Add(_error);
        root.Children.Add(_chrome.Card(list));
        return root;
    }

    private Button OpenEditorButton() => _chrome.Quiet(
        MacroEventsChrome.MacroIcon,
        _chrome.Texts["macroEvents.openEditor"],
        _chrome.Texts["macroEvents.openEditor.tip"],
        () => _ = EditAsync(null));

    private Border StatusCard()
    {
        var metrics = _chrome.Metrics;

        var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(_status);
        labels.Children.Add(_statusHint);

        var open = OpenEditorButton();
        var icon = _chrome.Icon(MacroEventsChrome.MacroIcon, 22);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = metrics.SpacingMedium };
        Grid.SetColumn(labels, 1);
        Grid.SetColumn(open, 2);
        header.Children.Add(icon);
        header.Children.Add(labels);
        header.Children.Add(open);

        return new Border
        {
            Padding = new Thickness(metrics.SpacingLarge - 4, metrics.SpacingMedium),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            Child = header,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised);
    }

    private Border EmptyState()
    {
        var metrics = _chrome.Metrics;

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_chrome.Title(_chrome.Texts["macroEvents.empty.title"]));
        text.Children.Add(_chrome.Description(_chrome.Texts["macroEvents.empty.body"]));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                OpenEditorButton(),
                _chrome.Primary(_chrome.Texts["macroEvents.empty.create"], () => _ = EditAsync(null)),
            },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = metrics.SpacingMedium };
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(text);
        grid.Children.Add(buttons);

        return new Border
        {
            Padding = new Thickness(metrics.SpacingLarge - 4, metrics.SpacingMedium),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            BorderThickness = new Thickness(1),
            Child = grid,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Info)
         .Themed(Border.BackgroundProperty, Palette.Keys.Hover);
    }
}
