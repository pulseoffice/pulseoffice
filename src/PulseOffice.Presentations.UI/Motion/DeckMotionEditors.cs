using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI.Motion;

public interface IDeckMotionEditors
{
    ITransitionSamples Samples(Presentation deck, Slide slide);

    TransitionEditor Transition(Presentation deck, Slide slide, MotionEditorLayout layout);

    TransitionDialogView TransitionDialog(Presentation deck, Slide slide);

    AnimationPane Animations(AnimationPaneContext context);

    IDeckElementNames Names { get; }
}

public sealed class DeckMotionEditors(
    TransitionEditorServices transitions,
    AnimationPaneServices animations,
    IDeckSlidePictures pictures,
    IDeckElementNames names) : IDeckMotionEditors
{
    public IDeckElementNames Names => names;

    public ITransitionSamples Samples(Presentation deck, Slide slide)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);
        var index = deck.IndexOf(slide);
        var previous = index > 0 ? deck.Slides[index - 1] : null;
        return new SlideTransitionSamples(pictures, deck, previous, slide);
    }

    public TransitionEditor Transition(Presentation deck, Slide slide, MotionEditorLayout layout) =>
        new(slide.Transition, new TransitionEditorContext(deck, Samples(deck, slide)), transitions, layout);

    public TransitionDialogView TransitionDialog(Presentation deck, Slide slide) =>
        new(slide.Transition, new TransitionEditorContext(deck, Samples(deck, slide)), transitions);

    public AnimationPane Animations(AnimationPaneContext context) => new(context, animations);
}
