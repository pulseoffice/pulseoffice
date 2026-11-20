using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class Round295LedgerWindowWalkTests
{
    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private sealed class Windows : ILedgerDocumentFactory
    {
        public OpenDocument? CreateSecondView(OpenDocument document, string title) =>
            document.CreateWindow(new Avalonia.Controls.Panel(), title);

        public OpenDocument CreateEmpty() => throw new NotSupportedException();

        public OpenDocument CreateFrom(PulseOffice.Sheets.Workbook workbook, string title) => throw new NotSupportedException();
    }

    private static Func<Task> NewWindow(IDocumentWorkspace workspace, ILedgerDocumentFactory factory)
    {
        var command = new LedgerPartialCommands(Fake<IActiveSheetContext>(), Fake<ISeriesFill>(), Fake<Engine.Operations.IStandardFilter>(), Fake<ISheetRangeFilters>(),
                factory, workspace, Fake<IDialogService>(),
                Fake<IStatusBarService>(), Texts, Fake<Engine.Operations.ISheetDuplicator>(),
                new DataDialogs.LedgerDialogKit(Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter()),
                Fake<DataDialogs.ISeriesPreviewers>(), Fake<ISheetValueWrites>(), new WorksheetTabPolicy())
            .GetCommands().Single(command => command.Id.Value == "ledger.window.new");
        return () => command.Execute(workspace.Context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task New_windows_are_numbered_from_the_first_and_share_one_history()
    {
        var workspace = new DocumentWorkspace();
        var history = new UndoStack();
        var first = new OpenDocument(ModuleKind.Sheets, "Budget", new Avalonia.Controls.Panel(), history);
        workspace.Open(first);
        var newWindow = NewWindow(workspace, new Windows());

        await newWindow();
        await newWindow();
        workspace.Close(workspace.Documents.Single(document => document.Title == "Budget : 2"));
        await newWindow();

        Assert.Equal(["Budget", "Budget : 3", "Budget : 4"], workspace.Documents.Select(document => document.Title));
        Assert.All(workspace.Documents, document => Assert.Same(history, document.History));
    }
}
