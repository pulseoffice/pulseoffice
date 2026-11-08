using PulseOffice.Testing;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PulseOffice.Document.Pages;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class LivePreviewTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly PageSketchLayout _layout = new();

    private sealed class ManualTimers : IDebounceTimerFactory
    {
        public ManualTimer? Last { get; private set; }

        public IDebounceTimer Create(TimeSpan delay, Action action) => Last = new ManualTimer(delay, action);
    }

    private sealed class ManualTimer(TimeSpan delay, Action action) : IDebounceTimer
    {
        public TimeSpan Delay => delay;

        public int Kicks { get; private set; }

        public bool IsPending { get; private set; }

        public void Kick()
        {
            Kicks++;
            IsPending = true;
        }

        public void Flush()
        {
            IsPending = false;
            action();
        }

        public void Dispose() => IsPending = false;
    }

    private sealed class Source : IPreviewSource
    {
        public int Calls { get; private set; }

        public bool Fails { get; set; }

        public bool Missing { get; set; }

        public Control? Render()
        {
            Calls++;
            if (Fails) throw new InvalidOperationException("broken");
            return Missing ? null : new TextBlock { Text = $"render {Calls}" };
        }
    }

    [Fact]
    public Task Il_riquadro_rinvia_gli_aggiornamenti_e_mostra_lo_stato_non_disponibile() => Session.Dispatch(() =>
    {
        var timers = new ManualTimers();
        var source = new Source();
        var pane = new LivePreviewPane(source, timers, new LivePreviewOptions { Backdrop = PreviewBackdrop.Checkerboard, UnavailableText = "Anteprima non disponibile" }, DesignTokens.For(InterfaceDensity.Compact));
        var window = new Window { Width = 300, Height = 200, Content = pane };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TimeSpan.FromMilliseconds(60), timers.Last!.Delay);
        Assert.True(pane.IsAvailable);
        Assert.Equal(1, source.Calls);
        Assert.Contains(pane.GetLogicalDescendants().OfType<Viewbox>(), _ => true);

        pane.Invalidate();
        pane.Invalidate();
        pane.Invalidate();
        Assert.True(pane.IsPending);
        Assert.Equal(1, source.Calls);
        timers.Last.Flush();
        Assert.Equal(2, source.Calls);
        Assert.False(pane.IsPending);

        source.Fails = true;
        pane.RefreshNow();
        Assert.False(pane.IsAvailable);
        Assert.Contains(pane.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "Anteprima non disponibile");

        source.Fails = false;
        source.Missing = true;
        pane.RefreshNow();
        Assert.False(pane.IsAvailable);

        source.Missing = false;
        pane.RefreshNow();
        Assert.True(pane.IsAvailable);
        pane.Dispose();
        window.Close();
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public void La_pagina_si_adatta_con_margini_intestazione_piè_e_colonne()
    {
        var a4 = new PageSketch(210, 297)
        {
            Margins = new PageSketchMargins(20, 25, 20, 25),
            Header = new PageSketchBand(10, 5, Center: "Titolo"),
            Footer = new PageSketchBand(8, 4, Right: "1"),
            Columns = 2,
            ColumnGap = 10,
        };

        var geometry = _layout.Fit(a4, 220, 307, 5);
        Assert.Equal(1, geometry.Scale, 6);
        Assert.Equal(new SketchRect(5, 5, 210, 297), geometry.Page);
        Assert.Equal(new SketchRect(25, 30, 170, 247), geometry.Content);
        Assert.Equal(new SketchRect(25, 30, 170, 10), geometry.Header);
        Assert.Equal(new SketchRect(25, 269, 170, 8), geometry.Footer);
        Assert.Equal(new SketchRect(25, 45, 170, 220), geometry.Body);
        Assert.Equal([new SketchRect(25, 45, 80, 220), new SketchRect(115, 45, 80, 220)], geometry.Columns);

        var landscape = _layout.Fit(a4 with { Orientation = PageSketchOrientation.Landscape }, 307, 220, 5);
        Assert.Equal(297, landscape.Page.Width, 6);
        Assert.Equal(210, landscape.Page.Height, 6);

        var slide = _layout.Fit(new PageSketch(254, 142.875) { Orientation = PageSketchOrientation.Landscape, Body = PageSketchBody.Blank }, 100, 100, 0);
        Assert.Equal(100, slide.Page.Width, 6);
        Assert.Equal(100 * 142.875 / 254, slide.Page.Height, 6);
        Assert.Equal(slide.Page, slide.Content);
        Assert.Null(slide.Header);
        Assert.Single(slide.Columns);

        var cramped = _layout.Fit(a4 with { Margins = new PageSketchMargins(150, 200, 150, 200) }, 220, 307, 5);
        Assert.True(cramped.Content.Width >= 0 && cramped.Content.Height >= 0);
        Assert.All(cramped.Columns, column => Assert.True(column.Width >= 0));
    }
}
