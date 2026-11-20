using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Lists;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round280FormulaAssistTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));
    private static readonly PulseOffice.Localization.TextCatalog Texts = new(PulseOffice.Localization.UiLanguage.English);

    private sealed class Vocabulary : IFormulaVocabulary
    {
        private static readonly FunctionDefinition Sum = new() { Name = "SUM", MinArguments = 1, MaxArguments = int.MaxValue, Implementation = (_, _) => default! };
        private static readonly FunctionDefinition SumIf = new() { Name = "SUMIF", MinArguments = 2, MaxArguments = 3, Implementation = (_, _) => default! };

        public IEnumerable<string> Names() => ["SUM", "SUMIF", "Sales"];

        public FunctionDefinition? FunctionShownAs(string shown) =>
            string.Equals(shown, "SUM", StringComparison.OrdinalIgnoreCase) ? Sum
            : string.Equals(shown, "SUMIF", StringComparison.OrdinalIgnoreCase) ? SumIf
            : null;

        public string CanonicalName(string shown) => shown.ToUpperInvariant();

        public char ArgumentSeparator => ',';
    }

    private sealed class RecordingList : IChoiceListPopup
    {
        public bool IsOpen { get; private set; }

        public Action<string>? Chosen { get; private set; }

        public void Show(Control host, Rect anchor, IReadOnlyList<string> choices, string? current, Action<string> chosen, Action? closed = null)
        {
            IsOpen = choices.Count > 0;
            Chosen = chosen;
        }

        public void Close() => IsOpen = false;

        public bool HandleKey(Key key) => IsOpen;
    }

    private static (TextBox Box, LedgerFormulaAssist Assist, RecordingList List) Build()
    {
        var box = new TextBox();
        var assist = new LedgerFormulaAssist(box, new Vocabulary(), Texts, () => true, DesignTokens.For(InterfaceDensity.Compact));
        var list = new RecordingList();
        assist.List = list;
        return (box, assist, list);
    }

    [Fact]
    public Task Typing_a_name_lists_every_match_and_choosing_one_opens_the_function() => Session.Dispatch(() =>
    {
        var (box, assist, _) = Build();

        box.Text = "=1+su";
        box.CaretIndex = 5;
        assist.Accept("SUMIF");

        Assert.Equal("=1+SUMIF(", box.Text);
        box.Text = "=1+SU";
        box.CaretIndex = 5;
        box.Text = "=1+SUM";
        box.CaretIndex = 6;
        Assert.Equal(["SUM", "SUMIF"], assist.Shown);
    }, CancellationToken.None);

    [Fact]
    public Task The_argument_tip_names_the_arguments_and_marks_the_current_one() => Session.Dispatch(() =>
    {
        var (box, assist, _) = Build();

        box.Text = "=SUMIF(A1:A3,";
        box.CaretIndex = box.Text.Length;

        var runs = assist.Tip.Inlines!.OfType<Run>().ToList();
        Assert.Contains(runs, run => run.Text == "range");
        Assert.Contains(runs, run => run.Text == "criteria" && run.FontWeight == Avalonia.Media.FontWeight.Bold);
        Assert.Contains(runs, run => run.Text == "[sum_range]");
        Assert.True(assist.Tip.IsVisible);
    }, CancellationToken.None);
}
