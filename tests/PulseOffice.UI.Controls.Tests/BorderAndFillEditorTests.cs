using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using PulseOffice.Document.Borders;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Borders;
using PulseOffice.UI.Controls.Fills;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class BorderAndFillEditorTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly TextCatalog _texts = new(UiLanguage.Italian);
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);

    private (Window Window, BorderEditor Editor) OpenBorders(BorderFrameScope scope, BorderFrame? initial = null)
    {
        var editor = new BorderEditors(_texts).Create(scope, initial ?? BorderFrame.Empty, _metrics);
        var window = new Window { Width = 700, Height = 500, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, editor);
    }

    private (Window Window, FillEditor Editor) OpenFill(ShapeFill fill, params FillPage[] pages)
    {
        var editor = new FillEditors(_texts).Create(fill, new FillEditorOptions(pages), _metrics);
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, editor);
    }

    [Fact]
    public Task IlClicSulLatoAltoDelloSchemaLoAccende() => Session.Dispatch(() =>
    {
        var (_, editor) = OpenBorders(new BorderFrameScope(false, false, true));
        var area = editor.Schema.Area;

        editor.Schema.Click(new Point(area.Center.X, area.Top + 1));

        Assert.True(editor.Frame.Has(BorderPosition.Top));
        Assert.Single(editor.Frame.Positions);
    }, CancellationToken.None);

    [Fact]
    public Task IlClicAlCentroDiUnIntervalloAccendeGliInterni() => Session.Dispatch(() =>
    {
        var (_, editor) = OpenBorders(new BorderFrameScope(true, true, false));
        var area = editor.Schema.Area;

        editor.Schema.Click(new Point(area.Center.X, area.Top + area.Height / 4));
        editor.Schema.Click(new Point(area.Left + area.Width / 4, area.Center.Y));

        Assert.True(editor.Frame.Has(BorderPosition.InsideVertical));
        Assert.True(editor.Frame.Has(BorderPosition.InsideHorizontal));
    }, CancellationToken.None);

    [Fact]
    public Task LaTastieraScorreILatiELiCommuta() => Session.Dispatch(() =>
    {
        var (window, editor) = OpenBorders(new BorderFrameScope(false, false, false));
        editor.Schema.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");

        Assert.Equal(BorderPosition.Bottom, editor.Schema.FocusedPosition);
        Assert.True(editor.Frame.Has(BorderPosition.Bottom));
    }, CancellationToken.None);

    [Fact]
    public Task LePreimpostazioniEIPulsantiDeiLatiSiAllineano() => Session.Dispatch(() =>
    {
        var (_, editor) = OpenBorders(new BorderFrameScope(true, true, true));

        editor.Model.Apply(editor.Model.Presets.Single(preset => preset.Key == "all"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(8, editor.SideButtons.Count);
        Assert.Equal(6, editor.SideButtons.Count(button => button.IsChecked == true));
        Assert.All(editor.SideButtons, button => Assert.False(string.IsNullOrEmpty(Avalonia.Automation.AutomationProperties.GetName(button))));
    }, CancellationToken.None);

    [Fact]
    public Task CambiareLoSpessoreRistilizzaILatiAccesi() => Session.Dispatch(() =>
    {
        var stroke = new BorderStroke(BorderDash.Solid, 0.75, "#000000");
        var (_, editor) = OpenBorders(new BorderFrameScope(false, false, false), BorderFrame.Empty.With(BorderPosition.Left, stroke));

        editor.Model.SetWidth(3);
        editor.Model.SetDash(BorderDash.Double);

        Assert.Equal(new BorderStroke(BorderDash.Double, 3, "#000000"), editor.Frame.Stroke(BorderPosition.Left));
        Assert.True(editor.Model.IsChanged);
    }, CancellationToken.None);

    [Fact]
    public Task IlRiempimentoCambiaPaginaETipo() => Session.Dispatch(() =>
    {
        var (_, editor) = OpenFill(ShapeFill.Default, FillPage.None, FillPage.Solid, FillPage.Gradient, FillPage.Pattern, FillPage.Hatch);
        ShapeFill? last = null;
        editor.FillChanged += (_, fill) => last = fill;

        Assert.Equal(FillPage.Solid, editor.Model.Page);
        editor.Tabs.Select(3);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(FillPage.Pattern, editor.Model.Page);
        Assert.Equal(ShapeFillKind.Hatch, last?.Kind);
        Assert.NotNull(editor.Model.CurrentPattern);
    }, CancellationToken.None);

    [Fact]
    public Task LaBarraDegliArrestiAggiungeESposta() => Session.Dispatch(() =>
    {
        var gradient = new ShapeFill { Kind = ShapeFillKind.Gradient, Gradient = new ShapeGradient { Stops = [new("#000000", 0), new("#FFFFFF", 1)] } };
        var (window, editor) = OpenFill(gradient, FillPage.Solid, FillPage.Gradient);
        var bar = FindBar(editor);
        Assert.NotNull(bar);

        bar!.Press(new Point(bar.Bar.X + bar.Bar.Width / 2, bar.Bar.Y + 2));
        bar.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);

        Assert.Equal(3, editor.Fill.Gradient.Stops.Count);
        Assert.Equal(1, editor.Model.SelectedStop);
        Assert.InRange(editor.Fill.Gradient.Stops[1].Position, 0.505, 0.52);
    }, CancellationToken.None);

    [Fact]
    public Task LoSfondoDellaCellaOffreSoloNessunoETinta() => Session.Dispatch(() =>
    {
        var (_, editor) = OpenFill(ShapeFill.None, FillPage.None, FillPage.Solid);

        Assert.Equal(FillPage.None, editor.Model.Page);
        editor.Model.ShowPage(FillPage.Gradient);
        editor.Model.ShowPage(FillPage.Solid);
        editor.Model.SetColor("#16A34A");

        Assert.Equal(FillPage.Solid, editor.Model.Page);
        Assert.Equal(ShapeFillKind.Solid, editor.Fill.Kind);
        Assert.Equal("#16A34A", editor.Fill.ColorHex);
    }, CancellationToken.None);

    private GradientStopBar? FindBar(Control root) => Find<GradientStopBar>(root);

    private T? Find<T>(Control root) where T : Control =>
        root is T match ? match : Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>().FirstOrDefault();
}
