using PulseOffice.Foundation.Zoom;
using System.Globalization;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Zoom;

namespace PulseOffice.Presentations.UI;

public sealed class DeckZoomCommands(
    IActiveDeckContext context,
    PulseOffice.UI.Controls.Prompts.IZoomPrompt zoom,
    IZoomChoice choice) : DeckCommandSet(context)
{
    private static readonly (string Id, double Factor)[] Presets =
    [
        ("deck.zoom.50", 0.5),
        ("deck.zoom.75", 0.75),
        ("deck.zoom.100", 1),
        ("deck.zoom.150", 1.5),
        ("deck.zoom.200", 2),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Zoom("deck.zoom.in", "Ctrl+Plus", t => choice.StepIn(t.ZoomSubject),
            t => choice.CanStepIn(t.ZoomSubject)),
        Zoom("deck.zoom.out", "Ctrl+Minus", t => choice.StepOut(t.ZoomSubject),
            t => choice.CanStepOut(t.ZoomSubject)),
        Zoom("deck.zoom.fit", "Ctrl+0", _ => ViewZoom.Whole,
            t => t.Zoom().Mode != ViewZoomMode.FitWhole),
        Zoom("deck.zoom.width", null, _ => ViewZoom.Width,
            t => t.Zoom().Mode != ViewZoomMode.FitWidth),
        .. Presets.Select(preset => Zoom(preset.Id, null, _ => ZoomLimits.Slides.At(preset.Factor),
            t => !t.Zoom().Matches(preset.Factor))),
        Prototype("deck.zoom.previous") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is { } t && t.Options.ZoomHistory.CanGoBack,
            Execute = (_, _) => History(back: true),
        },
        Prototype("deck.zoom.next") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is { } t && t.Options.ZoomHistory.CanGoForward,
            Execute = (_, _) => History(back: false),
        },
        Prototype("deck.zoom.object") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is { } t && t.SelectedMany().Count > 0,
            Execute = (_, _) => { Context.Target?.Options.ZoomToSelection?.Invoke(); return Task.CompletedTask; },
        },
        Tool("deck.zoom.pan", CanvasPointerTool.Pan),
        Tool("deck.zoom.tool", CanvasPointerTool.Zoom),
        Prototype("deck.zoom.custom") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => CustomAsync(ct),
        },
    ];

    private CommandDefinition Zoom(
        string id, string? gesture, Func<DeckTarget, ViewZoom> next, Func<DeckTarget, bool> enabled) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            DefaultGesture = gesture,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is { } t && enabled(t),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t) t.SetZoom(next(t));
                return Task.CompletedTask;
            },
        };

    private CommandDefinition Tool(string id, CanvasPointerTool tool) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is not null,
            CheckedWhen = _ => Context.Target?.Options.Tool == tool,
            Execute = (_, _) => { Context.Target?.Options.SetTool(tool); return Task.CompletedTask; },
        };

    private Task History(bool back)
    {
        if (Context.Target is not { } t) return Task.CompletedTask;
        var current = t.Zoom();
        if ((back ? t.Options.ZoomHistory.Back(current) : t.Options.ZoomHistory.Forward(current)) is { } zoom)
            t.RestoreZoom(zoom);
        return Task.CompletedTask;
    }

    private async Task CustomAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;

        await choice.ChooseAsync(t.ZoomSubject, zoom, ct).ConfigureAwait(true);
    }
}
