using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.UI.Controls.Links;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class LedgerObjectLinkTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider().GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private sealed class AnsweringPrompt(LinkChoice? answer) : IHyperlinkPrompt
    {
        public LinkRequest? Asked { get; private set; }

        public Task<LinkChoice?> AskAsync(LinkRequest request, CancellationToken ct = default)
        {
            Asked = request;
            return Task.FromResult(answer);
        }
    }

    [Fact]
    public async Task TheObjectHyperlinkIsEditedAndRemovedWithUndo()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Objects.Add(new PlacedObject { Id = "s", Anchor = new CellAddress(0, 0), Content = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle } });
        var history = new UndoStack();
        var context = Context(workbook, history);
        var prompt = new AnsweringPrompt(new LinkChoice(LinkKind.Web, "https://example.com/", string.Empty));
        var commands = new LedgerObjectCommands(context, Fake<PulseOffice.UI.Controls.Services.IDialogService>(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English), Fake<IContentFlipping>(), prompt, Fake<ILinkFollower>()).GetDialogCommands().ToList();

        await commands.Single(command => command.Id.Value == "ledger.object.hyperlink").Execute(null!, CancellationToken.None);

        Assert.False(prompt.Asked!.ShowsText);
        Assert.Equal("https://example.com/", sheet.Objects[0].Hyperlink);
        Assert.True(commands.Single(command => command.Id.Value == "ledger.object.hyperlink.remove").EnabledWhen!(null!));

        await commands.Single(command => command.Id.Value == "ledger.object.hyperlink.remove").Execute(null!, CancellationToken.None);
        Assert.Null(sheet.Objects[0].Hyperlink);

        history.Undo();
        Assert.Equal("https://example.com/", sheet.Objects[0].Hyperlink);
        history.Undo();
        Assert.Null(sheet.Objects[0].Hyperlink);
    }

    private ActiveSheetContext Context(Workbook workbook, UndoStack history)
    {
        var context = new ActiveSheetContext();
        PlacedObject? selected = workbook.Sheets[0].Objects[0];
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
            ReadSelectedObject = () => selected is null ? null : workbook.Sheets[0].Objects.FirstOrDefault(item => item.Id == selected.Id),
            ReadSelectedObjects = () => selected is null ? [] : [selected],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = placed => selected = placed,
        });
        return context;
    }
}
