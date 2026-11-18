using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round188DeckPlainTextPasteTests(HeadlessDesktopSession headless)
{
    private readonly UiThreadSession _session = headless.Session;

    [Fact]
    public Task Plain_text_lines_pasted_into_Deck_become_paragraphs() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        suite.Activate(ModuleKind.Presentations);
        suite.Deck.Select([]);
        var package = new ExchangePackage { Id = "r188", Representations = [ExchangeRepresentation.OfText(ExchangeFlavor.Text, "uno\r\ndue")] };

        Assert.True(suite.Exchange.Receive(ModuleKind.Presentations, package, ExchangePasteMode.UnformattedText));

        var pasted = suite.Deck.CurrentSlide()!.Elements.OfType<TextElement>().Single(element => element.Text.PlainText.Contains("uno", StringComparison.Ordinal));
        Assert.Equal(["uno", "due"], pasted.Text.Paragraphs.Select(paragraph => paragraph.Text));
    }, CancellationToken.None);
}
