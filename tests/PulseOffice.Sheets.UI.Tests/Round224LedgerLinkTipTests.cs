using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls.Links;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round224LedgerLinkTipTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    private sealed class Answering(Func<LinkRequest, LinkChoice> answer) : IHyperlinkPrompt
    {
        public LinkRequest? Asked { get; private set; }

        public Task<LinkChoice?> AskAsync(LinkRequest request, CancellationToken ct = default)
        {
            Asked = request;
            return Task.FromResult<LinkChoice?>(answer(request));
        }
    }

    [Fact]
    public async Task The_link_dialog_shows_and_edits_the_cell_screen_tip()
    {
        var (context, workbook) = Round224SheetContext.Create(Engines);
        var sheet = workbook.Sheets[0];
        sheet[CellAddress.First] = sheet[CellAddress.First] with { Hyperlink = "https://example.org", HyperlinkTip = "Old tip" };
        var prompt = new Answering(request => new LinkChoice(LinkKind.Web, request.Address, request.Text ?? string.Empty) { Tip = "New tip" });

        var command = new LedgerInsertCommands(context, prompt, Texts).GetDialogCommands().Single(definition => definition.Id.Value == "ledger.insert.hyperlink");
        await command.Execute(null!, CancellationToken.None);

        Assert.True(prompt.Asked!.ShowsTip);
        Assert.Equal("Old tip", prompt.Asked.Tip);
        Assert.Equal("New tip", sheet[CellAddress.First].HyperlinkTip);

        context.History!.Undo();
        Assert.Equal("Old tip", sheet[CellAddress.First].HyperlinkTip);
    }
}
