using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal sealed class LedgerCellEditing
{
    private readonly Workbook _workbook;
    private readonly CalculationEngine _engine;
    private readonly SheetGridView _grid;
    private readonly SheetSelection _selection;
    private readonly Canvas _overlay;
    private readonly IStatusBarService _status;
    private readonly ITextCatalog _texts;
    private readonly LedgerCellWriter _writer;
    private readonly Engine.Operations.IDataValidator? _validator;
    private readonly Engine.Operations.IAutoInput _autoInput;
    private readonly DesignTokens.Metrics _metrics;

    private bool _updatingFromSelection;
    private SheetCell? _formulaTarget;
    private CellAddress _editingAddress;
    private int _editingSheet;
    private bool _suggesting;
    private int _typedLength;

    public LedgerCellEditing(
        Workbook workbook, CalculationEngine engine, SheetGridView grid, SheetSelection selection, Canvas overlay,
        IStatusBarService status, ITextCatalog texts, LedgerCellWriter writer,
        Engine.Operations.IDataValidator? validator, Engine.Operations.IAutoInput autoInput, DesignTokens.Metrics metrics)
    {
        _workbook = workbook;
        _engine = engine;
        _grid = grid;
        _selection = selection;
        _overlay = overlay;
        _status = status;
        _texts = texts;
        _writer = writer;
        _validator = validator;
        _autoInput = autoInput;
        _metrics = metrics;

        NameBox = new TextBox
        {
            Width = 96,
            FontSize = _metrics.FontSize,
            CornerRadius = new CornerRadius(_metrics.Radius),
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        FormulaBox = new TextBox
        {
            Name = "FormulaBar",
            FontSize = _metrics.FontSize,
            CornerRadius = new CornerRadius(_metrics.Radius),
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        Avalonia.Automation.AutomationProperties.SetName(NameBox, _texts["ledger.nameBox"]);
        Avalonia.Automation.AutomationProperties.SetName(FormulaBox, _texts["command.ledger.view.formulaBar.title"]);

        CellEditor = new TextBox
        {
            FontSize = _metrics.FontSize,
            Padding = new Thickness(2, 0),
            IsVisible = false,
            BorderThickness = new Thickness(2),
        };

        CellEditor.KeyDown += OnEditorKeyDown;
        CellEditor.TextChanged += (_, _) =>
        {
            Suggest();
            MirrorEditor();
        };
        FormulaBox.KeyDown += OnFormulaKeyDown;
        FormulaBox.TextChanged += (_, _) =>
        {
            if (!_updatingFromSelection && _formulaTarget is null && FormulaBox.IsFocused)
                _formulaTarget = new SheetCell(_grid.SheetIndex, _selection.Active);
        };
        FormulaBox.GotFocus += (_, _) => TakeOverEditor();
        NameBox.KeyDown += OnNameBoxKeyDown;
    }

    public event EventHandler? SelectionStale;

    public event EventHandler<int>? SheetRequested;

    public TextBox NameBox { get; }

    public TextBox FormulaBox { get; }

    public TextBox CellEditor { get; }

    public bool IsEditing { get; private set; }

    public bool IsIdle => !IsEditing && _formulaTarget is null;

    public INameBoxResolver Resolver { get; set; } = new NameBoxResolver();

    public Engine.Operations.IReferenceCycler References { get; set; } = new Engine.Operations.ReferenceCycler();

    public PulseOffice.UI.Controls.Lists.IChoiceListPopup Choices { get; set; } = new PulseOffice.UI.Controls.Lists.ChoiceListPopup();

    public void ApplyZoom(double zoom) => CellEditor.FontSize = _metrics.FontSize * zoom;

    public void ShowSelection(Worksheet sheet)
    {
        var cell = sheet[_selection.Active];

        _formulaTarget = null;
        _updatingFromSelection = true;
        NameBox.Text = _selection.IsSingleCell
            ? _selection.Active.ToA1()
            : _selection.Range.ToString();

        FormulaBox.Text = _writer.EditableText(cell, sheet, _selection.Active);
        _updatingFromSelection = false;
        AnnounceCell(sheet, cell);
    }

    public void FocusArea(SheetInputArea area)
    {
        if (area == SheetInputArea.NameBox)
        {
            NameBox.Focus(NavigationMethod.Tab);
            NameBox.SelectAll();
        }
        else if (area == SheetInputArea.FormulaBar && FormulaBox.IsEffectivelyVisible)
        {
            FormulaBox.Focus(NavigationMethod.Tab);
            FormulaBox.CaretIndex = FormulaBox.Text?.Length ?? 0;
        }
        else _grid.Focus(NavigationMethod.Tab);
    }

    public void BeginEdit(string? replaceContent)
    {
        _grid.EnsureVisible(_selection.Active);

        var sheet = _workbook.Sheets[_grid.SheetIndex];
        if (_writer.IsLocked(sheet, _selection.Active))
        {
            _status.Report(_texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var cell = sheet[_selection.Active];
        var bounds = _grid.ActiveCellBounds();

        Canvas.SetLeft(CellEditor, bounds.X);
        Canvas.SetTop(CellEditor, bounds.Y);
        CellEditor.Width = Math.Max(bounds.Width, 60);
        CellEditor.Height = bounds.Height;

        _suggesting = true;
        CellEditor.Text = replaceContent
            ?? _writer.EditableText(cell, sheet, _selection.Active);
        _suggesting = false;
        _typedLength = CellEditor.Text?.Length ?? 0;

        _editingAddress = _selection.Active;
        _editingSheet = _grid.SheetIndex;
        IsEditing = true;
        _overlay.IsHitTestVisible = true;
        CellEditor.IsVisible = true;
        CellEditor.Focus();
        CellEditor.CaretIndex = CellEditor.Text?.Length ?? 0;
        MirrorEditor();
    }

    public void CommitEditor()
    {
        if (!IsEditing) return;

        var text = CellEditor.Text ?? string.Empty;
        var address = _editingAddress;

        IsEditing = false;
        CellEditor.IsVisible = false;
        _overlay.IsHitTestVisible = false;

        _writer.Write(text, address, _editingSheet);
    }

    public void CommitFormulaBar()
    {
        if (_formulaTarget is null) return;
        Commit();
    }

    public bool HasChoices(CellAddress address) => ListRuleAt(address) is not null;

    public void ShowChoices(CellAddress address)
    {
        if (IsEditing) CommitEditor();
        var sheet = _workbook.Sheets[_grid.SheetIndex];
        var choices = ListRuleAt(address) is { } rule && _validator is not null
            ? _validator.ChoicesOf(_engine, _grid.SheetIndex, rule)
            : _autoInput.Choices(sheet, address);
        if (choices.Count == 0) return;

        var target = new SheetCell(_grid.SheetIndex, address);
        var current = sheet[address].Value is { IsEmpty: false } value ? value.ToString() : null;
        Choices.Show(_grid, _grid.ActiveCellBounds(), choices, current, chosen =>
        {
            _writer.Write(chosen, target.Address, target.SheetIndex);
            SelectionStale?.Invoke(this, EventArgs.Empty);
        }, () => _grid.Focus());
    }

    private DataValidation? ListRuleAt(CellAddress address) =>
        _validator?.RuleAt(_workbook.Sheets[_grid.SheetIndex], address) is { Kind: ValidationKind.List, ShowDropdown: true } rule ? rule : null;

    private void AnnounceCell(Worksheet sheet, Cell cell)
    {
        var shown = _grid.PresentedText(_selection.Active);
        var place = _selection.IsSingleCell ? _selection.Active.ToA1() : _selection.Range.ToString();
        var name = _texts.Format("ledger.grid.accessibleName", sheet.Name, place, shown.Length > 0 ? shown : _texts["ledger.grid.accessibleEmpty"]);
        Avalonia.Automation.AutomationProperties.SetName(_grid, name);
        Avalonia.Automation.AutomationProperties.SetHelpText(_grid, cell.HasFormula ? _writer.EditableText(cell, sheet, _selection.Active) : string.Empty);
        Avalonia.Automation.AutomationProperties.SetName(CellEditor, _texts.Format("ledger.grid.editorName", place));
    }

    private void Suggest()
    {
        if (_suggesting || !IsEditing) return;

        var typed = CellEditor.Text ?? string.Empty;
        var grew = typed.Length > _typedLength;
        _typedLength = typed.Length;

        if (!grew || typed.Length == 0) return;
        if (CellEditor.CaretIndex != typed.Length) return;

        if (_editingSheet < 0 || _editingSheet >= _workbook.Sheets.Count) return;
        var sheet = _workbook.Sheets[_editingSheet];

        if (_autoInput.Complete(sheet, _editingAddress, typed) is not { } completion) return;

        _suggesting = true;

        try
        {
            CellEditor.Text = completion;
            CellEditor.SelectionStart = typed.Length;
            CellEditor.SelectionEnd = completion.Length;
        }
        finally
        {
            _suggesting = false;
        }
    }

    private void MirrorEditor()
    {
        if (!IsEditing) return;

        _updatingFromSelection = true;
        FormulaBox.Text = CellEditor.Text;
        _updatingFromSelection = false;
    }

    private void EndEdit()
    {
        IsEditing = false;
        CellEditor.IsVisible = false;
        _overlay.IsHitTestVisible = false;
        SelectionStale?.Invoke(this, EventArgs.Empty);
        _grid.Focus();
    }

    private bool CycleReference(TextBox box, KeyEventArgs e)
    {
        if (e.Key != Key.F4 || e.KeyModifiers != KeyModifiers.None) return false;
        var text = box.Text ?? string.Empty;
        if (!text.StartsWith('=')) return false;

        var cycled = References.CycleAt(text, box.CaretIndex);
        box.Text = cycled.Text;
        box.CaretIndex = Math.Clamp(cycled.Caret, 0, cycled.Text.Length);
        e.Handled = true;
        return true;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (CycleReference(CellEditor, e)) return;

        switch (e.Key)
        {
            case Key.Enter:
                CommitEditor();
                _selection.Advance(byRow: true, backwards: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                _grid.EnsureVisible(_selection.Active);
                _grid.Focus();
                e.Handled = true;
                break;

            case Key.Tab:
                CommitEditor();
                _selection.Advance(byRow: false, backwards: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                _grid.EnsureVisible(_selection.Active);
                _grid.Focus();
                e.Handled = true;
                break;

            case Key.Escape:

                EndEdit();
                e.Handled = true;
                break;
        }
    }

    private void OnFormulaKeyDown(object? sender, KeyEventArgs e)
    {
        if (_updatingFromSelection) return;
        if (CycleReference(FormulaBox, e)) return;

        switch (e.Key)
        {
            case Key.Enter:
                CommitFormulaBar();
                _selection.Advance(byRow: true, backwards: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                _grid.EnsureVisible(_selection.Active);
                _grid.Focus();
                e.Handled = true;
                break;

            case Key.Escape:

                SelectionStale?.Invoke(this, EventArgs.Empty);
                _grid.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnNameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (!GoTo(NameBox.Text ?? string.Empty)) SelectionStale?.Invoke(this, EventArgs.Empty);

        e.Handled = true;
    }

    public bool GoTo(string reference)
    {
        if (Resolver.Resolve(reference, _workbook, _grid.SheetIndex) is not { } target) return false;
        if (target.SheetIndex != _grid.SheetIndex) SheetRequested?.Invoke(this, target.SheetIndex);
        _selection.MoveTo(target.Range.Start);
        if (!target.Range.IsSingleCell) _selection.ExtendTo(target.Range.End);
        _grid.EnsureVisible(target.Range.Start);
        _grid.Focus();
        return true;
    }

    private void Commit()
    {
        var target = _formulaTarget ?? new SheetCell(_grid.SheetIndex, _selection.Active);
        _formulaTarget = null;
        _writer.Write(FormulaBox.Text ?? string.Empty, target.Address, target.SheetIndex);
    }

    private void TakeOverEditor()
    {
        if (!IsEditing) return;

        var text = CellEditor.Text ?? string.Empty;
        IsEditing = false;
        CellEditor.IsVisible = false;
        _overlay.IsHitTestVisible = false;

        _updatingFromSelection = true;
        FormulaBox.Text = text;
        _updatingFromSelection = false;
        _formulaTarget = new SheetCell(_editingSheet, _editingAddress);
        FormulaBox.CaretIndex = text.Length;
    }
}
