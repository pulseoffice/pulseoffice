using PulseOffice.Foundation.Zoom;
using PulseOffice.UI.Controls.Prompts;

namespace PulseOffice.UI.Controls.Zoom;

public sealed record ZoomSubject(Func<ViewZoom> Current, Func<double> Effective, Action<ViewZoom> Apply, ZoomLimits Limits)
{
    public double? PageAspect { get; init; }
}

public interface IZoomChoice
{
    Task ChooseAsync(ZoomSubject subject, IZoomPrompt prompt, CancellationToken ct);

    ViewZoom StepIn(ZoomSubject subject);

    ViewZoom StepOut(ZoomSubject subject);

    bool CanStepIn(ZoomSubject subject);

    bool CanStepOut(ZoomSubject subject);
}

public sealed class ZoomChoice(IZoomLadder ladder) : IZoomChoice
{
    public ViewZoom StepIn(ZoomSubject subject) => subject.Limits.At(ladder.StepIn(subject.Effective()));

    public ViewZoom StepOut(ZoomSubject subject) => subject.Limits.At(ladder.StepOut(subject.Effective()));

    public bool CanStepIn(ZoomSubject subject) => subject.Limits.CanGrow(subject.Effective());

    public bool CanStepOut(ZoomSubject subject) => subject.Limits.CanShrink(subject.Effective());

    public async Task ChooseAsync(ZoomSubject subject, IZoomPrompt prompt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(prompt);

        var request = new ZoomRequest(subject.Effective() * 100, subject.Limits.Minimum * 100, subject.Limits.Maximum * 100)
        {
            Fits = [ZoomFit.Page, ZoomFit.Width],
            CurrentFit = subject.Current().Mode switch
            {
                ViewZoomMode.FitWhole => ZoomFit.Page,
                ViewZoomMode.FitWidth => ZoomFit.Width,
                _ => null,
            },
        };
        if (subject.PageAspect is { } aspect) request = request with { PageAspect = aspect };

        switch (await prompt.AskAsync(request, ct).ConfigureAwait(true))
        {
            case { Fit: ZoomFit.Page }:
                subject.Apply(ViewZoom.Whole);
                break;
            case { Fit: ZoomFit.Width }:
                subject.Apply(ViewZoom.Width);
                break;
            case { Percent: { } percent }:
                subject.Apply(subject.Limits.At(percent / 100));
                break;
        }
    }
}
