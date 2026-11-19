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
using PulseOffice.UI.Controls.Theming;
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
    private bool _functionSuggested;
    private string? _namesLanguage;
    private IReadOnlyList<string> _functionNames = [];
    private Dictionary<string, string> _canonicalNames = new(StringComparer.OrdinalIgnoreCase);
    private int _typedLength;
    private string? _shownLanguage;
    private bool _enterMode;
    private string? _pointedText;
    private string? _suggestedText;
    private bool _pointedAway;
    private (int Start, int Length)? _pointed;

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

        AnnotatedRange = new PulseOffice.UI.Controls.Text.TextRangeHighlight(CellEditor);

        CellEditor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ArgumentTip = new TextBlock
        {
            FontSize = _metrics.FontSizeSmall,
            Padding = new Thickness(6, 2),
            IsVisible = false,
            IsHitTestVisible = false,
        }.Themed(TextBlock.BackgroundProperty, Palette.Keys.SurfaceRaised).Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);
        CellEditor.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.CaretIndexProperty) ShowArgumentTip();
        };
        CellEditor.TextChanged += (_, _) =>
        {
            if (CellEditor.Text != _suggestedText) _functionSuggested = false;
            if (CellEditor.Text != _pointedText) _pointed = null;
            Suggest();
            MirrorEditor();
        };
        FormulaBox.KeyDown += OnFormulaKeyDown;
        FormulaBox.TextChanged += (_, _) =>
        {
            if (FormulaBox.Text != _pointedText) _pointed = null;
            if (!_updatingFromSelection && _formulaTarget is null && FormulaBox.IsFocused)
            {
                _formulaTarget = new SheetCell(_grid.SheetIndex, _selection.Active);
                _pointedAway = false;
            }
        };
        FormulaBox.GotFocus += (_, _) => TakeOverEditor();
        NameBox.KeyDown += OnNameBoxKeyDown;
    }

    public event EventHandler? SelectionStale;

    public event EventHandler<int>? SheetRequested;

    public TextBox NameBox { get; }

    public TextBox FormulaBox { get; }

    public TextBox CellEditor { get; }

    public PulseOffice.UI.Controls.Text.TextRangeHighlight AnnotatedRange { get; }

    public bool IsEditing { get; private set; }

    public bool IsIdle => !IsEditing && _formulaTarget is null;

    public INameBoxResolver Resolver { get; set; } = new NameBoxResolver();

    public Engine.Operations.IReferenceCycler References { get; set; } = new Engine.Operations.ReferenceCycler();

    public Engine.Operations.IFormulaPointing Pointing { get; set; } = new Engine.Operations.FormulaPointing();

    public Engine.Operations.IFormulaCompletion Completion { get; set; } = new Engine.Operations.FormulaCompletion();

    public TextBlock ArgumentTip { get; }

    private IEnumerable<string> CompletionNames()
    {
        var language = _writer.Language;
        if (_namesLanguage != language)
        {
            _namesLanguage = language;
            _canonicalNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (var function in _engine.Functions.All.Where(function => !function.Name.StartsWith('_')))
                _canonicalNames.TryAdd(_writer.Formulas?.FunctionName(function.Name, language) ?? function.Name, function.Name);
            _functionNames = [.. _canonicalNames.Keys];
        }
        return _functionNames.Concat(_workbook.DefinedNames.Select(name => name.Name));
    }

    private void ShowArgumentTip()
    {
        var text = CellEditor.Text ?? string.Empty;
        var separator = _writer.Formulas?.Punctuation(_writer.Language).Argument ?? ',';
        if (!IsEditing || Completion.CallAt(text, CellEditor.CaretIndex, separator) is not { } call
            || _engine.Functions.Find(CanonicalName(call.Name)) is not { } function)
        {
            ArgumentTip.IsVisible = false;
            return;
        }

        var slots = function.MaxArguments is int.MaxValue or > 6 ? Math.Max(function.MinArguments, call.Argument + 1) + 1 : function.MaxArguments;
        var inlines = new Avalonia.Controls.Documents.InlineCollection { new Avalonia.Controls.Documents.Run(call.Name + "(") };
        for (var index = 0; index < slots; index++)
        {
            if (index > 0) inlines.Add(new Avalonia.Controls.Documents.Run(separator + " "));
            var label = _texts.Format("functions.argument", index + 1);
            if (index >= function.MinArguments) label = "[" + label + "]";
            var run = new Avalonia.Controls.Documents.Run(label);
            if (index == call.Argument) run.FontWeight = Avalonia.Media.FontWeight.Bold;
            inlines.Add(run);
        }
        if (function.MaxArguments is int.MaxValue) inlines.Add(new Avalonia.Controls.Documents.Run(separator + " …"));
        inlines.Add(new Avalonia.Controls.Documents.Run(")"));
        ArgumentTip.Inlines = inlines;
        Canvas.SetLeft(ArgumentTip, Canvas.GetLeft(CellEditor));
        Canvas.SetTop(ArgumentTip, Canvas.GetTop(CellEditor) + CellEditor.Height + 2);
        ArgumentTip.IsVisible = true;
    }

    private string CanonicalName(string shown)
    {
        CompletionNames();
        return _canonicalNames.GetValueOrDefault(shown, shown);
    }

    private bool AcceptFunction()
    {
        var text = CellEditor.Text ?? string.Empty;
        var suggested = _functionSuggested && CellEditor.SelectionStart != CellEditor.SelectionEnd;
        _functionSuggested = false;
        if (!text.StartsWith('=') || (!suggested && CellEditor.CaretIndex != text.Length)) return false;
        CompletionNames();
        var isFunction = _canonicalNames.ContainsKey(LastName(text));
        if (!isFunction && !suggested) return false;
        CellEditor.Text = isFunction ? text + "(" : text;
        CellEditor.CaretIndex = CellEditor.Text.Length;
        return true;
    }

    private void DropFunctionSuggestion()
    {
        if (!_functionSuggested || CellEditor.SelectionStart == CellEditor.SelectionEnd) return;
        var text = CellEditor.Text ?? string.Empty;
        var start = Math.Min(CellEditor.SelectionStart, CellEditor.SelectionEnd);
        _suggesting = true;
        CellEditor.Text = text[..Math.Clamp(start, 0, text.Length)];
        _suggesting = false;
        _functionSuggested = false;
    }

    private string LastName(string text)
    {
        var start = text.Length;
        while (start > 1 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] is '_' or '.')) start--;
        return text[start..];
    }

    public bool TryPoint(CellRange range)
    {
        var box = IsEditing ? CellEditor : _formulaTarget is not null ? FormulaBox : null;
        if (box is null) return false;
        var sheet = IsEditing ? _editingSheet : _formulaTarget!.Value.SheetIndex;
        if (sheet != _grid.SheetIndex) return false;

        var text = box.Text ?? string.Empty;
        int start, length;
        if (_pointed is { } pointed && pointed.Start + pointed.Length <= text.Length) (start, length) = pointed;
        else
        {
            if (box.SelectionStart != box.SelectionEnd || !Pointing.AcceptsReference(text, box.CaretIndex)) return false;
            (start, length) = (box.CaretIndex, 0);
        }

        var placed = Pointing.Place(text, start, length, range.ToString());
        _suggesting = true;
        try
        {
            if (box == FormulaBox) _updatingFromSelection = true;
            box.Text = placed.Text;
            box.CaretIndex = placed.Caret;
        }
        finally
        {
            _updatingFromSelection = false;
            _suggesting = false;
        }

        _typedLength = placed.Text.Length;
        _pointed = (placed.Start, placed.Length);
        _pointedText = placed.Text;
        _pointedAway = true;
        box.Focus();
        return true;
    }

    private void ReturnToEditedCell(CellAddress address)
    {
        if (!_pointedAway) return;
        _pointedAway = false;
        _pointed = null;
        _selection.MoveTo(address);
    }

    private bool PointByKey(KeyEventArgs e)
    {
        var (rows, columns) = e.Key switch
        {
            Key.Left => (0, -1),
            Key.Right => (0, 1),
            Key.Up => (-1, 0),
            Key.Down => (1, 0),
            _ => (0, 0),
        };
        if (rows == 0 && columns == 0) return false;
        if (!_enterMode || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None) return false;

        var text = CellEditor.Text ?? string.Empty;
        if (_pointed is not null || Pointing.AcceptsReference(text, CellEditor.CaretIndex))
        {
            _selection.Move(rows, columns, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            e.Handled = true;
            return true;
        }

        if (e.KeyModifiers != KeyModifiers.None) return false;
        var edited = _editingAddress;
        CommitEditor();
        _selection.MoveTo(edited);
        _selection.Move(rows, columns, extend: false);
        _grid.EnsureVisible(_selection.Active);
        _grid.Focus();
        e.Handled = true;
        return true;
    }

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
        _shownLanguage = _writer.Language;
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
        Canvas.SetLeft(AnnotatedRange, bounds.X);
        Canvas.SetTop(AnnotatedRange, bounds.Y);
        AnnotatedRange.Width = CellEditor.Width;
        AnnotatedRange.Height = bounds.Height;
        if (replaceContent is null && sheet.Comments.GetValueOrDefault(_selection.Active)?.AnnotatedText is { } annotated)
            AnnotatedRange.Show(annotated.Start, annotated.Length);
        else AnnotatedRange.Hide();

        _suggesting = true;
        CellEditor.Text = replaceContent
            ?? _writer.EditableText(cell, sheet, _selection.Active);
        _suggesting = false;
        _typedLength = CellEditor.Text?.Length ?? 0;
        _shownLanguage = _writer.Language;

        _editingAddress = _selection.Active;
        _editingSheet = _grid.SheetIndex;
        _enterMode = replaceContent is not null;
        _pointed = null;
        _pointedAway = false;
        _suggestedText = null;
        IsEditing = true;
        _overlay.IsHitTestVisible = true;
        CellEditor.IsVisible = true;
        CellEditor.Focus();
        CellEditor.CaretIndex = CellEditor.Text?.Length ?? 0;
        MirrorEditor();
    }

    public void FollowLanguage()
    {
        var language = _writer.Language;
        if (_shownLanguage is not { } from || from == language) return;
        _shownLanguage = language;

        if (IsEditing)
        {
            _suggesting = true;
            CellEditor.Text = _writer.Translated(CellEditor.Text ?? string.Empty, from);
            _suggesting = false;
            _typedLength = CellEditor.Text?.Length ?? 0;
            CellEditor.CaretIndex = CellEditor.Text?.Length ?? 0;
            MirrorEditor();
        }
        else if (_formulaTarget is not null)
        {
            _updatingFromSelection = true;
            FormulaBox.Text = _writer.Translated(FormulaBox.Text ?? string.Empty, from);
            _updatingFromSelection = false;
            FormulaBox.CaretIndex = FormulaBox.Text?.Length ?? 0;
        }
    }

    public void CommitEditor()
    {
        if (!IsEditing) return;

        var text = CellEditor.Text ?? string.Empty;
        var address = _editingAddress;

        IsEditing = false;
        CellEditor.IsVisible = false;
        ArgumentTip.IsVisible = false;
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
        if (_suggesting || !IsEditing || CellEditor.Text == _suggestedText) return;

        var typed = CellEditor.Text ?? string.Empty;
        var grew = typed.Length > _typedLength;
        _typedLength = typed.Length;

        if (!grew || typed.Length == 0) return;
        if (CellEditor.CaretIndex != typed.Length) return;

        if (_editingSheet < 0 || _editingSheet >= _workbook.Sheets.Count) return;
        var sheet = _workbook.Sheets[_editingSheet];

        if (typed.StartsWith('='))
        {
            if (Completion.Complete(typed, typed.Length, CompletionNames()) is not { } name) return;
            _suggesting = true;
            try
            {
                CellEditor.Text = name;
                CellEditor.SelectionStart = typed.Length;
                CellEditor.SelectionEnd = name.Length;
            }
            finally
            {
                _suggesting = false;
            }
            _functionSuggested = true;
            _suggestedText = name;
            return;
        }

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
        _suggestedText = completion;
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
        ArgumentTip.IsVisible = false;
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
        if (PointByKey(e)) return;

        var edited = _editingAddress;
        switch (e.Key)
        {
            case Key.F2 when e.KeyModifiers == KeyModifiers.None:
                _enterMode = !_enterMode;
                _pointed = null;
                e.Handled = true;
                break;

            case Key.Enter:
                DropFunctionSuggestion();
                CommitEditor();
                ReturnToEditedCell(edited);
                _selection.Advance(byRow: true, backwards: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                _grid.EnsureVisible(_selection.Active);
                _grid.Focus();
                e.Handled = true;
                break;

            case Key.Tab when e.KeyModifiers == KeyModifiers.None && AcceptFunction():
                e.Handled = true;
                break;

            case Key.Tab:
                CommitEditor();
                ReturnToEditedCell(edited);
                _selection.Advance(byRow: false, backwards: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                _grid.EnsureVisible(_selection.Active);
                _grid.Focus();
                e.Handled = true;
                break;

            case Key.Escape:

                EndEdit();
                ReturnToEditedCell(edited);
                e.Handled = true;
                break;
        }
    }

    private void OnFormulaKeyDown(object? sender, KeyEventArgs e)
    {
        if (_updatingFromSelection) return;
        if (CycleReference(FormulaBox, e)) return;

        var edited = _formulaTarget?.Address ?? _selection.Active;
        switch (e.Key)
        {
            case Key.Enter:
                CommitFormulaBar();
                ReturnToEditedCell(edited);
                _selection.Advance(byRow: true, backwards: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                _grid.EnsureVisible(_selection.Active);
                _grid.Focus();
                e.Handled = true;
                break;

            case Key.Escape:

                _formulaTarget = null;
                ReturnToEditedCell(edited);
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
        ArgumentTip.IsVisible = false;
        _overlay.IsHitTestVisible = false;

        _updatingFromSelection = true;
        FormulaBox.Text = text;
        _updatingFromSelection = false;
        _formulaTarget = new SheetCell(_editingSheet, _editingAddress);
        FormulaBox.CaretIndex = text.Length;
    }
}
