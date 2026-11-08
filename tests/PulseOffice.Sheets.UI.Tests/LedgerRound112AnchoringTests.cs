using Avalonia.Controls;
using Avalonia.LogicalTree;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Pages;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.Sheets.UI.PageLayout;
using PulseOffice.Testing;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Printing;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerRound112AnchoringTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.English);

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private sealed class AnsweringDialogs(Func<Control, object?> answer) : IDialogService
    {
        public bool IsDialogOpen => false;

        public List<Control> Shown { get; } = [];

        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default)
        {
            Shown.Add(content);
            return Task.FromResult((TResult?)answer(content));
        }

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }

    private (Workbook Workbook, ActiveSheetContext Context, PulseOffice.Document.Commands.UndoStack History) Ledger(PlacedObject selected)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(selected);
        var history = new PulseOffice.Document.Commands.UndoStack();
        var context = new ActiveSheetContext();
        PlacedObject? current = selected;
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
            Engine = Engines.Create(workbook),
            Selection = () => new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)),
            WriteSelection = _ => { },
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => 1,
            WriteZoom = _ => { },
            ReadSelectedObject = () => current,
            ReadSelectedObjects = () => current is null ? [] : [current],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = placed => current = placed,
        });
        return (workbook, context, history);
    }

    [Fact]
    public Task The_position_and_size_dialog_sets_the_anchoring_and_undo_restores_it() => Session.Dispatch(async () =>
    {
        var placed = new PlacedObject
        {
            Id = "s1",
            Content = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle },
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(2), Twip.FromCentimeters(1)),
        };
        var (workbook, context, history) = Ledger(placed);
        var dialogs = new AnsweringDialogs(_ => new Dictionary<string, object>
        {
            ["size"] = new LockedSize(2, 1, false),
            ["x"] = 0.0,
            ["y"] = 0.0,
            ["anchoring"] = 2,
        });
        var commands = new LedgerImageCommands(context, Fake<IRasterImageFiles>(), Fake<IRasterRecolor>(), Fake<IRasterColorAdjust>(),
            dialogs, Fake<IStatusBarService>(), Fake<PulseOffice.UI.Controls.Objects.IAltTextPrompt>(), _texts, new IconCatalog());

        await commands.GetCommands().Single(command => command.Id == "ledger.object.positionSize").Execute(null!, CancellationToken.None);

        var choice = Assert.Single(dialogs.Shown).GetLogicalDescendants().OfType<ComboBox>().Last();
        Assert.Equal(1, choice.SelectedIndex);
        Assert.Equal(_texts["ledger.object.anchoring.fixed"], ((IEnumerable<string>)choice.ItemsSource!).Last());
        Assert.Equal(ObjectAnchoring.Fixed, workbook.Sheets[0].Objects[0].Anchoring);
        history.Undo();
        Assert.Equal(ObjectAnchoring.Move, workbook.Sheets[0].Objects[0].Anchoring);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task A_custom_paper_is_shown_as_custom_with_its_size() => Session.Dispatch(() =>
    {
        var papers = new PaperCatalog();
        var custom = new PaperSize("custom", new DocumentSize(Twip.FromMillimeters(123), Twip.FromMillimeters(234)));
        var input = new SheetPageSetupInput(new PageSetup { Paper = custom }, papers.All, string.Empty, 0, 0, "Sheet1", "book.ods", Fake<IReferenceSource>());
        var view = new SheetPageSetupView(input, new SheetPageSetupServices(new PageSketchLayout(), new HeaderFooterTokens(), new MeasureUnits(), new IconCatalog()), _texts);
        var root = view.Build();
        new Window { Content = root }.Show();

        var paper = root.GetLogicalDescendants().OfType<ComboBox>()
            .First(box => box.ItemsSource is IEnumerable<string> items && items.Any(item => item.Contains("A4", StringComparison.Ordinal)));
        var shown = ((IEnumerable<string>)paper.ItemsSource!).ElementAt(paper.SelectedIndex);
        Assert.StartsWith(_texts["page.paper.custom"], shown, StringComparison.Ordinal);
        Assert.Contains("123 × 234 mm", shown, StringComparison.Ordinal);
        Assert.Equal(custom, view.Collect().Setup.Paper);
    }, TestContext.Current.CancellationToken);
}
