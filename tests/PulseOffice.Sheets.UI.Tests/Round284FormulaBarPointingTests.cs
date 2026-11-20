using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.Testing;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round284FormulaBarPointingTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private Workbook Workbook { get; } = Workbook.CreateDefault();

    private static void Press(Control box, Key key) =>
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = KeyModifiers.None });

    [Fact]
    public Task F2_in_the_formula_bar_switches_arrows_from_the_caret_to_pointing() => Session.Dispatch(() =>
    {
        var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
        var engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook);
        var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
        var status = new PulseOffice.UI.Controls.Services.StatusBarService(
            TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
            new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));
        var presenter = new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(
            System.Globalization.CultureInfo.InvariantCulture, serial => new DateTime(1899, 12, 30).AddDays(serial)));
        var selection = new SheetSelection();
        var grid = new SheetGridView(engine, presenter, new ConditionalFormattingEvaluator(provider.GetRequiredService<Evaluator>()), selection);
        var writer = new LedgerCellWriter(Workbook, engine, new UndoStack(), status, texts, new PulseOffice.Shell.DocumentWorkspace(), grid,
            null, null, null, null);
        var editing = new LedgerCellEditing(Workbook, engine, grid, selection, new Canvas(), status, texts, writer,
            null, new AutoInput(), DesignTokens.For(InterfaceDensity.Compact));
        selection.Changed += (_, _) => editing.TryPoint(selection.Range);

        editing.FormulaBox.Text = "=1+";
        editing.FormulaBox.CaretIndex = 3;
        Press(editing.FormulaBox, Key.Down);
            Assert.Equal(CellAddress.First, selection.Active);

            Press(editing.FormulaBox, Key.F2);
            Press(editing.FormulaBox, Key.Down);

            Assert.True(editing.IsFormulaBarPointing);
            Assert.Equal(new CellAddress(1, 0), selection.Active);
            Assert.Equal("=1+A2", editing.FormulaBox.Text);
    }, TestContext.Current.CancellationToken);
}
