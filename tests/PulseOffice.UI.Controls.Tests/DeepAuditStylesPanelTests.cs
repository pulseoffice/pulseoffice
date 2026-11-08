using Avalonia.Controls;
using Avalonia.Input;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.StylePanels;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class DeepAuditStylesPanelTests
{
    private sealed class FailingSource : IStylesPanelSource
    {
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;

        private EventHandler? _changed;

        public IReadOnlyList<StyleFamilyChoice> Families => [new("p", "Paragrafo")];

        public IReadOnlyList<StyleListEntry> Styles(string family) => [new("Standard", "Predefinito", 0)];

        public string? Current(string family) => "Standard";

        public bool CanApply(string family) => true;

        public bool Can(StylePanelAction action, string family, string? style) => true;

        public void Apply(string family, string style)
        {
        }

        public Task RunAsync(StylePanelAction action, string family, string? style, CancellationToken ct) =>
            Task.FromException(new InvalidOperationException("rotto"));

        public event EventHandler? Changed
        {
            add
            {
                _changed += value;
            }
            remove
            {
                _changed -= value;
            }
        }
    }

    private static StylesPanelServices Services() =>
        new(new StyleRecents(), new CompositeIconCatalog([new IconCatalog(), new PanelIcons()]), new TextCatalog(UiLanguage.Italian));

    [Fact]
    public Task UnAzioneCheFallisceSiMostraNelPannelloSenzaEccezioni() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var panel = new StylesPanel(new FailingSource(), Services());
        var window = new Window { Content = panel, Width = 300, Height = 600 };
        window.Show();

        panel.Press(Key.F2);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("Operazione non riuscita: rotto", panel.Failure);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task IlPannelloRiattaccatoTornaAdAscoltareLaSorgente() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var source = new FailingSource();
        var panel = new StylesPanel(source, Services());
        var window = new Window { Content = panel, Width = 300, Height = 600 };
        window.Show();
        Assert.Equal(1, source.Subscribers);

        window.Content = null;
        Assert.Equal(0, source.Subscribers);

        window.Content = panel;
        Assert.Equal(1, source.Subscribers);
        window.Close();
    }, CancellationToken.None);
}
