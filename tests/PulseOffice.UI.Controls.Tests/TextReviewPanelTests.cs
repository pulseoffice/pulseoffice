using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Spelling;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class TextReviewPanelTests
{
    private static (TextReviewSession Session, SidePanelView Panel, Window Window) Open()
    {
        var texts = new TextCatalog(UiLanguage.Italian);
        var panel = new SidePanelView(texts);
        var session = new TextReviewSession(panel);
        panel.Register(new SidePanelPage(TextReviewSession.PageKey, () => new TextReviewPanel(session, texts, new IconCatalog())));
        var window = new Window { Content = panel, Width = 360, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (session, panel, window);
    }

    private static TextReviewPanel Shown(Window window) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<TextReviewPanel>().Single();

    [Fact]
    public Task Ortografia_nel_pannello_risponde_con_il_suggerimento_scelto_e_continua_senza_ricostruire() => MacroEventsViewTests.Session.Dispatch(async () =>
    {
        var (session, panel, window) = Open();

        var first = session.AskAsync(new SpellingFinding("perche", "Diapositiva 1", ["perché", "perce"]), "command.deck.spelling.check.title", CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.IsOpen);
        var view = Shown(window);
        Assert.Equal(2, view.Suggestions!.ItemCount);

        view.Actions["replace"].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var answer = await first;
        Assert.Equal(new SpellingAnswer(SpellingDecision.Replace, "perché"), answer);

        var second = session.AskAsync(new SpellingFinding("ciaoo", "Diapositiva 2", []), "command.deck.spelling.check.title", CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(view, Shown(window));
        view.Custom!.Text = "ciao";
        view.Actions["replaceAll"].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(new SpellingAnswer(SpellingDecision.ReplaceAll, "ciao"), await second);

        var third = session.AskAsync(new SpellingFinding("xyz", "A1", []), "command.ledger.spelling.check.title", CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        panel.Close();
        Assert.Equal(SpellingDecision.Stop, (await third).Decision);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Sinonimi_e_lingua_usano_lo_stesso_pannello() => MacroEventsViewTests.Session.Dispatch(async () =>
    {
        var (session, _, window) = Open();

        var synonym = session.AskAsync("bello", [new ThesaurusChoice("carino", "agg."), new ThesaurusChoice("grazioso", "agg.")], "command.folio.spelling.thesaurus.title", CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        var view = Shown(window);
        view.Suggestions!.SelectedIndex = 2;
        view.Actions["apply"].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("grazioso", await synonym);

        var choices = new DictionaryLanguageNames();
        var language = session.AskAsync([choices.For("de-DE"), choices.For("it-IT"), choices.For("fr-FR")], 1, "command.deck.spelling.languageAll.title", CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, view.Languages!.SelectedIndex);
        view.LanguageSearch!.Text = "fr-";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, view.Languages.ItemCount);
        view.Actions["apply"].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(2, await language);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task La_correzione_colore_parte_dai_preset_e_mostra_il_dopo() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var pixels = new BitmapPixels();
        var bitmap = pixels.Create(new BgraImage([.. Enumerable.Repeat((byte)128, 64)], 4, 4));
        var view = new ColorCorrectionView(bitmap, pixels, new ColorCorrectionPresets(), new GalleryNavigator(), new MeasureUnits(), new TextCatalog(UiLanguage.Italian));
        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.Adjustment.IsIdentity);
        Assert.Same(bitmap, view.After.Source);

        view.Presets.Select(1);
        view.Apply(new ColorCorrectionPresets().All[1].Adjustment);
        Assert.Equal(20, view.Adjustment.Brightness);
        Assert.NotSame(bitmap, view.After.Source);

        object? result = null;
        view.Completed += (_, value) => result = value;
        view.Apply(new ColorAdjustment(Contrast: 10, Gamma: 1.2));
        Assert.Equal(1.2, view.Adjustment.Gamma, 3);
        window.Close();
        Assert.Null(result);
    }, CancellationToken.None);

    [Fact]
    public Task Se_il_documento_cambia_mentre_aspetta_la_domanda_si_rifa_e_il_pannello_lo_dice() => MacroEventsViewTests.Session.Dispatch(async () =>
    {
        var (session, _, window) = Open();

        var pending = session.AskAsync(new SpellingFinding("perche", "Paragrafo 1", ["perché"]), "command.folio.spelling.check.title", CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        session.Invalidate();
        Assert.Equal(SpellingDecision.Recheck, (await pending).Decision);

        var again = session.AskAsync(new SpellingFinding("perche", "Paragrafo 1", ["perché"]), "command.folio.spelling.check.title", CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(Shown(window).Notice);
        Shown(window).Actions["replace"].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(SpellingDecision.Replace, (await again).Decision);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(Shown(window).Notice);

        var synonym = session.AskAsync("casa", [new ThesaurusChoice("dimora", "nome")], "command.folio.spelling.thesaurus.title", CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        session.Invalidate();
        Assert.Null(await synonym);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(Shown(window).Notice);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Senza_dizionari_il_pannello_lo_spiega_e_porta_ai_dizionari() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.Italian);
        var panel = new SidePanelView(texts);
        var session = new TextReviewSession(panel);
        var opened = 0;
        panel.Register(new SidePanelPage(TextReviewSession.PageKey, () => new TextReviewPanel(session, texts, new IconCatalog(), openDictionaries: () => opened++)));
        var window = new Window { Content = panel, Width = 360, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        session.ShowMissingDictionaries();
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.IsOpen);
        Assert.IsType<MissingDictionariesRequest>(session.Pending);
        Shown(window).Actions["dictionaries"].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, opened);
        Assert.Null(session.Pending);
        window.Close();
    }, CancellationToken.None);
}
