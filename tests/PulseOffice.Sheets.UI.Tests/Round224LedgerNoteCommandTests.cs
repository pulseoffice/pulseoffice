using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round224LedgerNoteCommandTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    [Fact]
    public async Task Show_comment_toggles_only_the_selected_note_in_one_undo_step()
    {
        var (context, workbook) = Round224SheetContext.Create(Engines);
        var sheet = workbook.Sheets[0];
        sheet.Comments[CellAddress.First] = new CellComment { Text = "one" };
        sheet.Comments[new CellAddress(4, 4)] = new CellComment { Text = "two" };
        var command = new LedgerSheetToolsCommands(context, new Engine.Operations.SheetOutline(), new Engine.Operations.SheetSnapshots(), null!,
                new StatusBarService(TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
                    new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System)), Texts)
            .GetCommands().Single(definition => definition.Id.Value == "ledger.comment.toggleShown");

        await command.Execute(null!, CancellationToken.None);

        Assert.True(sheet.Comments[CellAddress.First].IsAlwaysVisible);
        Assert.False(sheet.Comments[new CellAddress(4, 4)].IsAlwaysVisible);
        Assert.Contains("cell/notes", command.ContextPaths);

        context.History!.Undo();
        Assert.False(sheet.Comments[CellAddress.First].IsAlwaysVisible);
    }
}
