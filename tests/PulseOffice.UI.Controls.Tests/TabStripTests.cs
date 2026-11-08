using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class TabStripTests
{
    private sealed class Names : TabStrip<string>
    {
        public Names(DesignTokens.Metrics metrics) : base(metrics) => Height = 28;

        public List<int> Picked { get; } = [];

        public int Measures { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Measures++;
            return base.MeasureOverride(availableSize);
        }

        protected override TabGeometry Geometry => new(0, 8, 2, 13);

        public void Show(IReadOnlyList<string> names) => Present(names, 0);

        protected override string Title(string item) => item;

        protected override bool Visible(string item) => !item.StartsWith('-');

        protected override void DrawTab(DrawingContext context, string item, Rect bounds, FormattedText text, bool selected, bool hovered) =>
            context.DrawText(text, bounds.TopLeft);

        protected override void Chosen(int index, string item) => Picked.Add(index);
    }

    [Fact]
    public Task Un_clic_sul_margine_di_una_scheda_la_sceglie_e_la_striscia_si_rimisura_perche_il_grassetto_cambia_le_larghezze() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var strip = new Names(DesignTokens.For(InterfaceDensity.Compact)) { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        strip.Show(["iiii", "-nascosta", "WWWWWWWWWWWW"]);
        var window = new Window { Width = 800, Height = 100, Content = strip };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var right = strip.DesiredSize.Width;
        var measures = strip.Measures;

        window.MouseDown(new Point(right - 2, 26), MouseButton.Left);
        window.MouseUp(new Point(right - 2, 26), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([2], strip.Picked);
        Assert.True(strip.Measures > measures);
        window.Close();
    }, TestContext.Current.CancellationToken);
}
