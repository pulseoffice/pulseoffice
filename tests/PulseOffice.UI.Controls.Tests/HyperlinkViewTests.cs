using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Links;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class HyperlinkViewTests
{
    private static readonly TextCatalog Texts = new(UiLanguage.Italian);

    private readonly LinkAddresses _addresses = new();

    private static readonly IReadOnlyList<LinkPlace> Places =
    [
        new("Titoli", string.Empty, "link.place.heading")
        {
            Selectable = false,
            Children =
            [
                new LinkPlace("Introduzione", "#Introduzione|outline", "link.place.heading")
                {
                    Children = [new LinkPlace("Scopo", "#Scopo|outline", "link.place.heading")],
                },
                new LinkPlace("Conclusioni", "#Conclusioni|outline", "link.place.heading"),
            ],
        },
        new("Segnalibri", string.Empty, "link.place.bookmark") { Selectable = false, Children = [new LinkPlace("Tabella1", "#Tabella1", "link.place.bookmark")] },
    ];

    private HyperlinkView View(LinkRequest request) => new(request, new HyperlinkServices(_addresses, Texts, new IconCatalog(), null));

    [Theory]
    [InlineData("https://example.com", LinkKind.Web)]
    [InlineData("example.com", LinkKind.Web)]
    [InlineData("mailto:a@b.it?subject=Ciao", LinkKind.Mail)]
    [InlineData("#Introduzione|outline", LinkKind.Place)]
    [InlineData("file:///tmp/a.odt", LinkKind.File)]
    [InlineData("../altro/b.ods", LinkKind.File)]
    [InlineData("/Users/x/c.odp", LinkKind.File)]
    [InlineData("", LinkKind.Web)]
    public void LIndirizzoSiRiconosceDalSuoAspetto(string address, LinkKind expected) =>
        Assert.Equal(expected, _addresses.Classify(address, Places));

    [Fact]
    public void UnIndirizzoSenzaSchemaDiventaHttpsELaPostaPortaLOggetto()
    {
        Assert.Equal("https://pulseoffice.example", _addresses.Web("pulseoffice.example"));
        Assert.Equal("parola", _addresses.Web("parola"));
        var mail = _addresses.Mail(new MailAddress("anna@esempio.it", "Verbale di lunedì"));
        Assert.Equal("mailto:anna@esempio.it?subject=Verbale%20di%20luned%C3%AC", mail);
        Assert.Equal(new MailAddress("anna@esempio.it", "Verbale di lunedì"), _addresses.ReadMail(mail));
        Assert.False(_addresses.IsComplete(LinkKind.Mail, "mailto:anna"));
        Assert.Equal("/tmp/a b.odt", _addresses.ReadFile(_addresses.File("/tmp/a b.odt")));
    }

    [Fact]
    public Task SiApreSulTipoDelCollegamentoEsistenteConLaDestinazioneScelta() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = View(new LinkRequest("t") { Address = "#Scopo|outline", Places = Places, CanRemove = true });
        Assert.Equal(LinkKind.Place, view.Kind);
        Assert.True(view.KindTiles[1].IsChecked);
        Assert.Equal("Scopo", ((LinkPlace)view.Places.SelectedItem!).Label);
        Assert.Equal("#Scopo|outline", view.Address);
        Assert.Equal("Scopo", view.Result()!.Text);
        Assert.Contains(view.Footer!.GetLogicalDescendantsOfButtons(), label => label == Texts["link.remove"]);
    }, CancellationToken.None);

    [Fact]
    public Task IlTestoMostratoSegueLIndirizzoFinchéNonLoSiScrive() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = View(new LinkRequest("t") { Places = Places });
        Assert.False(view.ConfirmButton.IsEnabled);
        view.WebBox.Text = "esempio.it";
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.ConfirmButton.IsEnabled);
        Assert.Equal("https://esempio.it", view.TextBox!.Text);
        view.TextBox.Text = "Il sito";
        Dispatcher.UIThread.RunJobs();
        view.WebBox.Text = "esempio.org";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new LinkChoice(LinkKind.Web, "https://esempio.org", "Il sito"), view.Result());

        view.SelectKind(LinkKind.Mail);
        view.MailToBox.Text = "anna@esempio.it";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("mailto:anna@esempio.it", view.Result()!.Address);
    }, CancellationToken.None);

    [Fact]
    public Task LaRicercaFiltraLAlberoTenendoIGenitori() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = View(new LinkRequest("t") { Places = Places });
        view.SelectKind(LinkKind.Place);
        view.SearchBox.Text = "scop";
        Dispatcher.UIThread.RunJobs();
        var shown = Assert.Single((IReadOnlyList<LinkPlace>)view.Places.ItemsSource!);
        Assert.Equal("Titoli", shown.Label);
        Assert.Equal("Scopo", Assert.Single(Assert.Single(shown.Children).Children).Label);
        view.SearchBox.Text = "nessuno";
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.Places.IsVisible);
    }, CancellationToken.None);

    [Fact]
    public Task ConInvioSiConfermaEIlCollegamentoSenzaTestoNonLoChiede() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var view = View(new LinkRequest("t") { ShowsText = false, Address = "https://a.it" });
        object? result = null;
        view.Completed += (_, value) => result = value;
        var window = new Window { Width = 700, Height = 500, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(view.TextBox);
        view.WebBox.Focus();
        window.KeyPress(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new LinkChoice(LinkKind.Web, "https://a.it", "https://a.it"), result);
        window.Close();
    }, CancellationToken.None);
}
