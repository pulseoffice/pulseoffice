using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Input;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Navigation;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.StylePanels;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class StylesPanelTests
{
    private sealed class Source : IStylesPanelSource
    {
        public List<string> Applied { get; } = [];

        public List<string> Ran { get; } = [];

        public string? CurrentParagraph { get; set; } = "Heading";

        public bool Applicable { get; set; } = true;

        public List<StyleListEntry> Paragraphs { get; } =
        [
            new("Standard", "Predefinito", 0) { InUse = true, Preview = new StylePreview { Font = "Serif", Size = 12 } },
            new("Heading", "Intestazione", 1) { InUse = true, Preview = new StylePreview { Size = 40, Bold = true, Foreground = "#FF2563EB" } },
            new("Mine", "Mio stile", 2) { Custom = true },
            new("Quote", "Citazione", 1),
        ];

        public IReadOnlyList<StyleFamilyChoice> Families =>
        [
            new("p", "Paragrafo") { Icon = "panel.family.paragraph", TracksUsage = true },
            new("c", "Carattere") { Icon = "panel.family.character" },
        ];

        public bool CanApply(string family) => Applicable;

        public event EventHandler? Changed;

        public IReadOnlyList<StyleListEntry> Styles(string family) => family == "p" ? Paragraphs : [new("Emphasis", "Enfasi", 0)];

        public string? Current(string family) => family == "p" ? CurrentParagraph : null;

        public bool Can(StylePanelAction action, string family, string? style) => action switch
        {
            StylePanelAction.New => true,
            StylePanelAction.Update => Current(family) is not null,
            StylePanelAction.Delete => style is "Mine",
            _ => style is not null,
        };

        public void Apply(string family, string style) => Applied.Add(family + ":" + style);

        public Task RunAsync(StylePanelAction action, string family, string? style, CancellationToken ct)
        {
            Ran.Add($"{action}:{family}:{style}");
            if (action == StylePanelAction.Delete && style is not null) Paragraphs.RemoveAll(entry => entry.Name == style);
            return Task.CompletedTask;
        }

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    private static StylesPanelServices Services() => new(new StyleRecents(), new CompositeIconCatalog([new IconCatalog(), new PanelIcons()]), new TextCatalog(UiLanguage.Italian));

    [Fact]
    public void IlModelloSegueLoStileInUsoEMantieneLaGerarchia()
    {
        var source = new Source();
        var model = new StylesPanelModel(source, new StyleRecents());

        Assert.Equal("p", model.Family);
        Assert.Equal("Heading", model.Selected);
        Assert.Equal([0, 1, 2, 1], model.Rows.Select(row => row.Depth));
        Assert.True(model.Rows.Single(row => row.Entry.Name == "Heading").IsCurrent);

        model.Select("Quote");
        source.Raise();
        model.Refresh();
        Assert.Equal("Quote", model.Selected);

        source.CurrentParagraph = "Standard";
        model.Refresh();
        Assert.Equal("Standard", model.Selected);
    }

    [Fact]
    public void IFiltriAppiattisconoESceglono()
    {
        var source = new Source();
        var recents = new StyleRecents();
        var model = new StylesPanelModel(source, recents);

        Assert.Equal([StyleListFilter.All, StyleListFilter.Recent, StyleListFilter.Custom, StyleListFilter.Applied], model.Filters);

        model.SetFilter(StyleListFilter.Custom);
        Assert.Equal(["Mine"], model.Rows.Select(row => row.Entry.Name));
        Assert.All(model.Rows, row => Assert.Equal(0, row.Depth));

        model.SetFilter(StyleListFilter.Applied);
        Assert.Equal(["Standard", "Heading"], model.Rows.Select(row => row.Entry.Name));

        model.SetFilter(StyleListFilter.All);
        model.Select("Quote");
        Assert.True(model.Apply());
        model.Select("Mine");
        Assert.True(model.Apply());
        model.SetFilter(StyleListFilter.Recent);
        Assert.Equal(["Mine", "Quote", "Heading"], model.Rows.Select(row => row.Entry.Name));
        Assert.Equal(["p:Quote", "p:Mine"], source.Applied);

        model.SetFilter(StyleListFilter.All);
        model.SetSearch("cit");
        Assert.Equal(["Quote"], model.Rows.Select(row => row.Entry.Name));

        model.Choose("c");
        Assert.Equal([StyleListFilter.All, StyleListFilter.Recent, StyleListFilter.Custom], model.Filters);
        model.SetFilter(StyleListFilter.Applied);
        Assert.Equal(StyleListFilter.All, model.Filter);
    }

    [Fact]
    public async Task EliminareChiedeConfermaEAzioniRispettanoLoStato()
    {
        var source = new Source();
        var model = new StylesPanelModel(source, new StyleRecents());

        Assert.False(model.Can(StylePanelAction.Delete));
        Assert.False(model.RequestDelete());

        model.Select("Mine");
        Assert.True(model.Can(StylePanelAction.Delete));
        Assert.True(await model.RunAsync(StylePanelAction.Delete, CancellationToken.None));
        Assert.Equal("Mine", model.PendingDelete);
        Assert.Empty(source.Ran);

        model.CancelDelete();
        Assert.Null(model.PendingDelete);

        model.RequestDelete();
        Assert.True(await model.ConfirmDeleteAsync(CancellationToken.None));
        Assert.Equal(["Delete:p:Mine"], source.Ran);
        Assert.DoesNotContain(model.Rows, row => row.Entry.Name == "Mine");

        source.Applicable = false;
        model.Select("Quote");
        Assert.False(model.CanApply);
        Assert.False(model.Apply());
    }

    [Fact]
    public Task IlPannelloMostraFamiglieEAnteprimeEApplicaConInvio() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new Source();
        var panel = new StylesPanel(source, Services());
        var window = new Window { Content = panel, Width = 300, Height = 600 };
        window.Show();

        Assert.Equal(["Standard", "Heading", "Mine", "Quote"], panel.Visible);
        Assert.Equal(["new", "update", "edit", "delete"], panel.Actions.Keys);
        Assert.True(panel.Actions.IsActionEnabled("new"));
        Assert.True(panel.Actions.IsActionEnabled("edit"));
        Assert.False(panel.Actions.IsActionEnabled("delete"));
        Assert.Equal("Modifica lo stile selezionato (F2)", panel.Actions.Tip("edit"));

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        panel.FocusList();
        window.KeyPress(Key.Enter, Avalonia.Input.RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.Equal(["p:Heading"], source.Applied);

        panel.Model.Select("Mine");
        Assert.True(panel.Actions.IsActionEnabled("delete"));
        panel.Press(Key.Delete);
        Assert.True(panel.ConfirmVisible);
        panel.Press(Key.Escape);
        Assert.False(panel.ConfirmVisible);

        var menu = panel.ContextItems();
        Assert.Equal(["Applica", "Modifica…", "Nuovo da questo…", "Aggiorna dalla selezione", "Rinomina…", "Elimina"], menu.Select(item => item.Header));
        Assert.All(menu, item => Assert.True(item.Enabled));

        panel.Choose("c");
        Assert.Equal("c", panel.Family);
        Assert.Equal(["Emphasis"], panel.Visible);
        panel.Choose("inesistente");
        Assert.Equal("c", panel.Family);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task LaBarraDAzioniRispettaAbilitazioneEVisibilita() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var bar = new PanelActionBar(new PanelIcons(), DesignTokens.For(InterfaceDensity.Compact),
        [
            new PanelAction("a", "panel.go", "Vai"),
            new PanelAction("b", "panel.delete", "Elimina") { Trailing = true, StartsGroup = true },
        ]);
        var pressed = new List<string>();
        bar.Invoked += (_, key) => pressed.Add(key);

        Assert.True(bar.Press("a"));
        bar.SetEnabled("a", false);
        Assert.False(bar.Press("a"));
        bar.SetVisible("b", false);
        Assert.False(bar.Press("b"));
        Assert.False(bar.IsActionVisible("b"));
        bar.SetVisible("b", true);
        Assert.True(bar.Press("b"));
        Assert.Equal(["a", "b"], pressed);
        Assert.Equal("Elimina", bar.Tip("b"));
    }, CancellationToken.None);

    private sealed class Navigator : INavigatorSource
    {
        public IReadOnlyList<NavigatorCategory> Categories => [new("headings", "Titoli"), new("tables", "Tabelle")];

        public List<string> Moves { get; } = [];

        public IReadOnlyList<NavigatorItem> Items(string category) => category == "headings"
            ? [new("h1", "Uno", 0) { Movable = true }, new("h2", "Due", 1) { Movable = true }]
            : [new("t1", "Tabella1", 0)];

        public string? Current(string category) => null;

        public void Go(string category, string id) => Moves.Add("go:" + id);

        public bool CanMove(string category, string id, NavigatorMove move) => move != NavigatorMove.Up || id != "h1";

        public void Move(string category, string id, NavigatorMove move) => Moves.Add(move + ":" + id);

        public bool CanDrop(string category, string id, string? beforeId) => false;

        public void Drop(string category, string id, string? beforeId)
        {
        }

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }

    [Fact]
    public Task IlNavigatoreUsaLaBarraAIconeENascondeGliSpostamenti() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new Navigator();
        var panel = new NavigatorPanel(source, new PanelIcons(), new TextCatalog(UiLanguage.Italian));

        Assert.False(panel.Actions.IsActionEnabled("go"));
        panel.Select("h1");
        Assert.True(panel.Actions.IsActionEnabled("go"));
        Assert.False(panel.Actions.IsActionEnabled("up"));
        Assert.True(panel.Actions.Press("down"));
        Assert.True(panel.Actions.Press("go"));
        Assert.Equal(["Down:h1", "go:h1"], source.Moves);
        Assert.Equal(2, panel.Actions.SeparatorsShown);

        panel.Choose("tables");
        Assert.False(panel.Actions.IsActionVisible("up"));
        Assert.True(panel.Actions.IsActionVisible("go"));
        Assert.Equal(0, panel.Actions.SeparatorsShown);
    }, CancellationToken.None);
}
