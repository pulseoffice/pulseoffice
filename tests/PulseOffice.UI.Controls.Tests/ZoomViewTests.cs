using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Prompts;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ZoomViewTests
{
    private static (ZoomView View, Window Window) Open(ZoomRequest request)
    {
        var view = new ZoomView(request, new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.Italian), new MeasureUnits(), new IconCatalog(), CultureInfo.GetCultureInfo("it-IT"));
        var window = new Window { Content = view, Width = 640, Height = 420 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    [Fact]
    public Task La_percentuale_corrente_accende_il_campione_uguale() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var (view, window) = Open(new ZoomRequest(150, 10, 400));

        Assert.Equal("150", view.Percentages.SelectedKey);
        Assert.Null(view.Fits);
        Assert.Equal(150, view.Answer.Percent);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Un_campione_scrive_il_campo_e_spegne_l_adattamento() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var (view, window) = Open(new ZoomRequest(100, 10, 400) { Fits = [ZoomFit.Page, ZoomFit.Width], CurrentFit = ZoomFit.Width });

        Assert.Equal(ZoomFit.Width, view.Answer.Fit);
        Assert.Equal(-1, view.Percentages.SelectedIndex);

        view.Percentages.SelectKey("75");

        Assert.Equal(75, view.Percent.Value);
        Assert.Equal(-1, view.Fits!.SelectedIndex);
        Assert.Equal(ZoomAnswer.At(75), view.Answer);
        Assert.Equal(75, view.Glyph.ShownPercent);

        view.Fits.SelectKey(nameof(ZoomFit.Page));
        Assert.Equal(-1, view.Percentages.SelectedIndex);
        Assert.Equal(ZoomAnswer.Fitting(ZoomFit.Page), view.Answer);
        Assert.Equal(ZoomFit.Page, view.Glyph.ShownFit);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Una_percentuale_libera_spegne_i_campioni_e_conferma() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var (view, window) = Open(new ZoomRequest(100, 25, 400));
        object? result = null;
        view.Completed += (_, value) => result = value;

        view.Percent.StepBy(1, large: false);

        Assert.Equal(105, view.Percent.Value);
        Assert.Equal(-1, view.Percentages.SelectedIndex);

        view.Confirm();
        Assert.Equal(ZoomAnswer.At(105), result);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task I_campioni_fuori_dai_limiti_non_compaiono() => MacroEventsViewTests.Session.Dispatch(() =>
    {
        var (view, window) = Open(new ZoomRequest(100, 60, 160));

        Assert.Equal(3, view.Percentages.Items.Count);
        window.Close();
    }, CancellationToken.None);
}
