using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls.Lists;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal interface IFormulaVocabulary
{
    IEnumerable<string> Names();

    FunctionDefinition? FunctionShownAs(string shown);

    string CanonicalName(string shown);

    char ArgumentSeparator { get; }
}

internal sealed class LedgerFormulaAssist
{
    private readonly TextBox _box;
    private readonly IFormulaVocabulary _vocabulary;
    private readonly ITextCatalog _texts;
    private readonly Func<bool> _active;
    private readonly Popup _tipPopup;
    private bool _accepting;
    private string? _shownFor;

    public LedgerFormulaAssist(
        TextBox box, IFormulaVocabulary vocabulary, ITextCatalog texts, Func<bool> active, DesignTokens.Metrics metrics)
    {
        _box = box;
        _vocabulary = vocabulary;
        _texts = texts;
        _active = active;

        Tip = new TextBlock
        {
            FontSize = metrics.FontSizeSmall,
            Padding = new Thickness(6, 2),
            IsHitTestVisible = false,
        }.Themed(TextBlock.BackgroundProperty, Palette.Keys.SurfaceRaised).Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);
        _tipPopup = new Popup
        {
            PlacementTarget = box,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = PopupAnchor.TopLeft,
            PlacementGravity = PopupGravity.TopRight,
            IsLightDismissEnabled = false,
            Child = Tip,
        };
        ((ISetLogicalParent)_tipPopup).SetParent(box);

        box.TextChanged += (_, _) => Refresh();
        box.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.CaretIndexProperty) Refresh();
        };
        box.LostFocus += (_, _) => HideTip();
    }

    public IFormulaCompletion Completion { get; set; } = new FormulaCompletion();

    public IFunctionSignatures Signatures { get; set; } = new FunctionSignatures();

    public IFunctionArgumentNames ArgumentNames { get; set; } = new FunctionArgumentNames();

    public IChoiceListPopup List { get; set; } = new ChoiceListPopup();

    public TextBlock Tip { get; }

    public bool IsTipShown => _tipPopup.IsOpen;

    public IReadOnlyList<string> Shown { get; private set; } = [];

    public void Hide()
    {
        List.Close();
        Shown = [];
        _shownFor = null;
        HideTip();
    }

    public bool HandleKey(KeyEventArgs e)
    {
        if (!_active()) return false;
        if (List.IsOpen && e.KeyModifiers == KeyModifiers.None
            && e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Tab or Key.Escape)
        {
            e.Handled = List.HandleKey(e.Key);
            if (e.Key == Key.Escape) Shown = [];
            return e.Handled;
        }

        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None && Shown.Count > 0)
        {
            Accept(Shown[0]);
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None && OpenTypedFunction())
        {
            e.Handled = true;
            return true;
        }
        return false;
    }

    public void Accept(string name)
    {
        var text = _box.Text ?? string.Empty;
        var caret = _box.CaretIndex;
        var start = Completion.NameStart(text, caret);
        if (start < 0) return;
        var accepted = Completion.Accept(text, caret, name, _vocabulary.FunctionShownAs(name) is not null);
        _accepting = true;
        try
        {
            _box.Text = accepted;
            _box.CaretIndex = accepted.Length - (text.Length - caret);
        }
        finally
        {
            _accepting = false;
        }
        Shown = [];
        List.Close();
        _box.Focus();
        Refresh();
    }

    private bool OpenTypedFunction()
    {
        var text = _box.Text ?? string.Empty;
        var caret = _box.CaretIndex;
        var start = Completion.NameStart(text, caret);
        if (start < 0 || _vocabulary.FunctionShownAs(text[start..caret]) is null) return false;
        Accept(text[start..caret]);
        return true;
    }

    private void Refresh()
    {
        if (!_active())
        {
            Hide();
            return;
        }

        if (!_accepting) ShowChoices();
        ShowTip();
    }

    private void ShowChoices()
    {
        var text = _box.Text ?? string.Empty;
        var caret = _box.CaretIndex;
        IReadOnlyList<string> matches = _box.SelectionStart == _box.SelectionEnd
            ? Completion.Matches(text, caret, _vocabulary.Names())
            : [];
        if (matches.Count == 1 && Completion.NameStart(text, caret) is var start and >= 0
            && string.Equals(matches[0], text[start..caret], StringComparison.OrdinalIgnoreCase))
            matches = [];
        if (matches.Count == 0)
        {
            Shown = [];
            _shownFor = null;
            List.Close();
            return;
        }

        var key = text[..caret] + "\u0001" + string.Join('\u0001', matches);
        if (key == _shownFor && List.IsOpen) return;
        _shownFor = key;
        Shown = matches;
        if (TopLevel.GetTopLevel(_box) is null) return;
        List.Show(_box, new Rect(_box.Bounds.Size), matches, null, Accept);
    }

    private void ShowTip()
    {
        var text = _box.Text ?? string.Empty;
        var separator = _vocabulary.ArgumentSeparator;
        if (Completion.CallAt(text, _box.CaretIndex, separator) is not { } call
            || _vocabulary.FunctionShownAs(call.Name) is not { } function)
        {
            HideTip();
            return;
        }

        var canonical = _vocabulary.CanonicalName(call.Name);
        var slots = function.MaxArguments is int.MaxValue or > 8
            ? Math.Max(Math.Max(function.MinArguments, Signatures.Arguments(canonical).Count), call.Argument + 1)
            : function.MaxArguments;
        var inlines = new InlineCollection { new Run(call.Name + "(") };
        for (var index = 0; index < slots; index++)
        {
            if (index > 0) inlines.Add(new Run(separator + " "));
            var label = ArgumentNames.Localize(Signatures.ArgumentName(canonical, index), _texts.Language.ToCode());
            if (label.Length == 0) label = _texts.Format("functions.argument", index + 1);
            if (index >= function.MinArguments) label = "[" + label + "]";
            var run = new Run(label);
            if (index == call.Argument) run.FontWeight = FontWeight.Bold;
            inlines.Add(run);
        }
        if (function.MaxArguments is int.MaxValue) inlines.Add(new Run(separator + " …"));
        inlines.Add(new Run(")"));
        Tip.Inlines = inlines;
        if (TopLevel.GetTopLevel(_box) is not null) _tipPopup.IsOpen = true;
        Tip.IsVisible = true;
    }

    private void HideTip()
    {
        _tipPopup.IsOpen = false;
        Tip.IsVisible = false;
    }
}
