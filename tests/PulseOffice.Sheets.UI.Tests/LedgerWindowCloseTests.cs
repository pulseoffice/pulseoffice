using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class LedgerWindowCloseTests
{
    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    private static T Fake<T>() where T : class => System.Reflection.DispatchProxy.Create<T, LedgerCommandsAuditTests.Inert>();

    private static CommandDefinition CloseWindow(IDocumentWorkspace workspace) =>
        new LedgerPartialCommands(Fake<IActiveSheetContext>(), Fake<ISeriesFill>(), Fake<Engine.Operations.IStandardFilter>(), Fake<ISheetRangeFilters>(),
                Fake<ILedgerDocumentFactory>(), workspace, Fake<IDialogService>(),
                Fake<IStatusBarService>(), Texts, Fake<Engine.Operations.ISheetDuplicator>(),
                new DataDialogs.LedgerDialogKit(Texts, Fake<PulseOffice.UI.Icons.IIconCatalog>(), new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter()),
                Fake<DataDialogs.ISeriesPreviewers>(), Fake<ISheetValueWrites>(), new WorksheetTabPolicy())
            .GetCommands().Single(command => command.Id.Value == "ledger.window.close");

    private sealed class Edit : IUndoableAction
    {
        public string Description => "edit";

        public void Undo() { }

        public void Redo() { }

        public bool TryMergeWith(IUndoableAction previous) => false;
    }

    [Fact]
    public async Task Closing_the_window_of_a_modified_workbook_with_no_other_window_does_not_discard_it()
    {
        var workspace = new DocumentWorkspace();
        var other = new OpenDocument(ModuleKind.Sheets, "Other", new Avalonia.Controls.Panel(), new UndoStack());
        var history = new UndoStack();
        var modified = new OpenDocument(ModuleKind.Sheets, "Budget", new Avalonia.Controls.Panel(), history);
        workspace.Open(other);
        workspace.Open(modified);
        history.Push(new Edit());

        var command = CloseWindow(workspace);
        Assert.False(command.IsEnabled(workspace.Context));
        await command.Execute(workspace.Context, TestContext.Current.CancellationToken);

        Assert.Contains(modified, workspace.Documents);
    }

    [Fact]
    public async Task Closing_a_second_window_keeps_the_workbook_open_in_the_first()
    {
        var workspace = new DocumentWorkspace();
        var history = new UndoStack();
        var first = new OpenDocument(ModuleKind.Sheets, "Budget", new Avalonia.Controls.Panel(), history);
        var second = new OpenDocument(ModuleKind.Sheets, "Budget : 2", new Avalonia.Controls.Panel(), history);
        workspace.Open(first);
        workspace.Open(second);
        history.Push(new Edit());

        var command = CloseWindow(workspace);
        Assert.True(command.IsEnabled(workspace.Context));
        await command.Execute(workspace.Context, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(second, workspace.Documents);
        Assert.Contains(first, workspace.Documents);
    }
}
