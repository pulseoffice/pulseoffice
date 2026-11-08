using Avalonia;
using Avalonia.Controls;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Ribbon;

namespace PulseOffice.Presentations.UI.Motion;

public sealed class TransitionGalleryPainter(
    TransitionStage stage, ITransitionCatalog catalog, ITransitionSamples samples, Func<SlideTransition> template) : IThumbnailPainter<TransitionKind>
{
    public Control Create(TransitionKind item, Size size) =>
        new TransitionThumbnail(stage, Transition(item), samples, size);

    public void Repaint(Control thumbnail, TransitionKind item)
    {
        if (thumbnail is TransitionThumbnail transition) transition.Show(Transition(item));
    }

    private SlideTransition Transition(TransitionKind kind) => catalog.Choose(template(), kind);
}

public sealed record TransitionRibbonThumbnail(SlideTransition Transition) : RibbonThumbnail;

public sealed class TransitionRibbonThumbnailPainter(TransitionStage stage) : IRibbonThumbnailPainter
{
    private readonly StylizedTransitionSamples _samples = new();

    public bool Accepts(RibbonThumbnail thumbnail) => thumbnail is TransitionRibbonThumbnail;

    public Control Paint(RibbonThumbnail thumbnail, double width, double height) =>
        new TransitionThumbnail(stage, ((TransitionRibbonThumbnail)thumbnail).Transition, _samples, new Size(width, height));
}
