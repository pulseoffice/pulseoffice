using Avalonia.Input;
using PulseOffice.Foundation.Zoom;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Prompts;
using PulseOffice.UI.Controls.Rulers;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Zoom;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class SharedObjectViewTests
{
    private readonly IParagraphRuler _ruler = new ParagraphRuler();
    private readonly IObjectNudgeKeys _nudges = new ObjectNudgeKeys();

    [Fact]
    public void IMarcatoriDelParagrafoSeguonoRientriETabulazioni()
    {
        var markers = _ruler.Markers(new ParagraphRulerFrame(72, 500, -18, 36, [new TabStop(100) { Alignment = TabAlignment.Center }], 72));

        Assert.Equal([54.0, 72, 464, 172], markers.Select(marker => marker.Position));
        Assert.Equal(RulerMarkerKind.TabCenter, markers[3].Kind);
        Assert.Equal(ParagraphRulerHandle.FirstLine, _ruler.HandleOf(markers[0]));
        Assert.Equal(ParagraphRulerHandle.Tab, _ruler.HandleOf(markers[3]));
    }

    [Fact]
    public void LeTabulazioniRestanoOrdinate()
    {
        IReadOnlyList<TabStop> tabs = [new TabStop(50), new TabStop(100)];
        var marker = _ruler.Markers(new ParagraphRulerFrame(0, 400, null, null, tabs, 0))[3];

        var moved = _ruler.MoveTab(tabs, marker, 150);
        var added = _ruler.AddTab(tabs, 75);

        Assert.Equal([100.0, 150], moved.Select(tab => tab.Position));
        Assert.Equal([50.0, 75, 100], added.Select(tab => tab.Position));
        Assert.Same(tabs, _ruler.AddTab(tabs, -3));
        Assert.Equal([100.0], _ruler.RemoveTab(tabs, marker).Select(tab => tab.Position));
    }

    [Fact]
    public void LeFrecceSpostanoAPassoPiccoloOGrande()
    {
        var steps = new NudgeSteps(1, 10);

        Assert.Equal((-1.0, 0.0), _nudges.Read(Key.Left, KeyModifiers.None)!.Value.Distance(steps));
        Assert.Equal((0.0, 10.0), _nudges.Read(Key.Down, KeyModifiers.Shift)!.Value.Distance(steps));
        Assert.Null(_nudges.Read(Key.Left, KeyModifiers.Control));
        Assert.Null(_nudges.Read(Key.A, KeyModifiers.None));
    }

    [Fact]
    public async Task LaSceltaDelloZoomRispettaILimitiDelModulo()
    {
        var current = ViewZoom.Whole;
        var subject = new ZoomSubject(() => current, () => 1, zoom => current = zoom, ZoomLimits.Slides);
        var choice = new ZoomChoice(new ZoomLadder());

        await choice.ChooseAsync(subject, new FixedZoomPrompt(ZoomAnswer.At(900)), CancellationToken.None);
        Assert.Equal(ZoomLimits.Slides.Maximum, current.Factor);

        await choice.ChooseAsync(subject, new FixedZoomPrompt(ZoomAnswer.Fitting(ZoomFit.Width)), CancellationToken.None);
        Assert.Equal(ViewZoomMode.FitWidth, current.Mode);
        Assert.True(choice.CanStepIn(subject));
        Assert.True(choice.StepIn(subject).Factor > 1);
    }

    private sealed class FixedZoomPrompt(ZoomAnswer answer) : IZoomPrompt
    {
        public Task<ZoomAnswer?> AskAsync(ZoomRequest request, CancellationToken ct) => Task.FromResult<ZoomAnswer?>(answer);
    }
}
