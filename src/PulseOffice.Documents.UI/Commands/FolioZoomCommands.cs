using PulseOffice.Foundation.Zoom;
using PulseOffice.UI.Controls.Zoom;
using PulseOffice.Documents.UI.Canvas;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Prompts;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioZoomCommands(IActiveFolioContext context, IZoomPrompt prompt, IZoomChoice choice) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Zoom("folio.zoom.in", "Ctrl+Plus", target => choice.StepIn(target.ZoomSubject),
            target => choice.CanStepIn(target.ZoomSubject)),
        Zoom("folio.zoom.out", "Ctrl+Minus", target => choice.StepOut(target.ZoomSubject),
            target => choice.CanStepOut(target.ZoomSubject)),
        Zoom("folio.zoom.page", "Ctrl+0", _ => ViewZoom.Whole, target => target.Zoom().Mode != ViewZoomMode.FitWhole),
        Zoom("folio.zoom.width", null, _ => ViewZoom.Width, target => target.Zoom().Mode != ViewZoomMode.FitWidth),
        Zoom("folio.zoom.100", null, _ => ZoomLimits.Pages.At(1), target => !target.Zoom().Matches(1)),
        Prototype("folio.zoom.custom") with
        {
            Surface = CommandSurface.Menu | CommandSurface.Palette,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => CustomAsync(ct),
        },
    ];

    private CommandDefinition Zoom(string id, string? gesture, Func<FolioTarget, ViewZoom> next, Func<FolioTarget, bool> enabled) =>
        Prototype(id) with
        {
            DefaultGesture = gesture,
            EnabledWhen = _ => Context.Target is { } target && enabled(target),
            Execute = (_, _) =>
            {
                if (Context.Target is { } target) target.SetZoom(next(target));
                return Task.CompletedTask;
            },
        };

    private async Task CustomAsync(CancellationToken ct)
    {
        if (Context.Target is not { } target) return;
        await choice.ChooseAsync(target.ZoomSubject, prompt, ct).ConfigureAwait(true);
    }
}
