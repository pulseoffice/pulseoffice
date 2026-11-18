using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round189DeckPasteLabelTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task Text_pasted_into_Deck_is_named_paste_in_the_history() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.Activate(ModuleKind.Presentations);
        suite.Deck.Select([]);
        var package = new ExchangePackage { Id = "r189", Representations = [ExchangeRepresentation.OfText(ExchangeFlavor.Text, "uno")] };

        Assert.True(suite.Exchange.Receive(ModuleKind.Presentations, package, ExchangePasteMode.UnformattedText));

        var paste = suite.Service<ITextCatalog>()["deck.action.paste"];
        Assert.Equal(paste, suite.Document(ModuleKind.Presentations).History.UndoDescription);
    }, CancellationToken.None);
}
