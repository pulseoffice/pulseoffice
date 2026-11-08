using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckShowActions(Presentation deck, ITextCatalog texts, DeckActionRecorder recorder)
{
    public void SetTransition(Slide slide, SlideTransition transition)
    {
        var before = slide.Transition;
        if (before == transition) return;

        Do(texts["deck.action.transition"],
            () => { slide.Transition = transition; deck.Raise(); },
            () => { slide.Transition = before; deck.Raise(); });
    }

    public void SetTransitions(IReadOnlyList<Slide> targets, SlideTransition transition)
    {
        var before = targets.Select(slide => (slide, slide.Transition)).ToList();
        if (before.All(entry => entry.Transition == transition)) return;
        Do(texts["deck.action.transition"],
            () => { foreach (var slide in targets) slide.Transition = transition; deck.Raise(); },
            () => { foreach (var (slide, previous) in before) slide.Transition = previous; deck.Raise(); });
    }

    public void ReplaceAnimations(Slide slide, IReadOnlyList<AnimationEffect> animations)
    {
        var before = slide.Animations.ToList();
        if (before.SequenceEqual(animations)) return;
        void Set(IReadOnlyList<AnimationEffect> list)
        {
            foreach (var effect in slide.Animations.ToList()) slide.RemoveAnimation(effect.Id);
            for (var index = 0; index < list.Count; index++) slide.InsertAnimation(index, list[index]);
            deck.Raise();
        }
        Do(texts["deck.action.animationEdit"], () => Set(animations), () => Set(before));
    }

    public void AnimateElement(Slide slide, SlideElement element) =>
        AddAnimation(slide, new AnimationEffect
        {
            Id = slide.NextAnimationId(),
            TargetId = element.Id,
            Category = AnimationCategory.Entrance,
            Preset = "fade",
            Trigger = slide.Animations.Count == 0 ? AnimationTrigger.OnClick : AnimationTrigger.AfterPrevious,
            DurationMs = 500,
        });

    public void AddAnimation(Slide slide, AnimationEffect effect)
    {
        var index = slide.Animations.Count;

        Do(texts["deck.action.animationAdd"],
            () => { slide.InsertAnimation(index, effect); deck.Raise(); },
            () => { slide.RemoveAnimation(effect.Id); deck.Raise(); });
    }

    public void RemoveAnimation(Slide slide, AnimationEffect effect)
    {
        var index = slide.Animations.ToList().FindIndex(a => a.Id == effect.Id);
        if (index < 0) return;

        Do(texts["deck.action.animationRemove"],
            () => { slide.RemoveAnimation(effect.Id); deck.Raise(); },
            () => { slide.InsertAnimation(index, effect); deck.Raise(); });
    }

    public void SetCustomShows(IReadOnlyList<CustomShow> shows)
    {
        var before = deck.CustomShows.ToList();
        var after = shows.ToList();
        if (SameShows(before, after)) return;

        Do(texts["deck.action.customShows"],
            () => { ReplaceShows(after); deck.Raise(); },
            () => { ReplaceShows(before); deck.Raise(); });
    }

    private bool SameShows(IReadOnlyList<CustomShow> left, IReadOnlyList<CustomShow> right) =>
        left.Count == right.Count
        && left.Zip(right).All(pair => pair.First.Name == pair.Second.Name && pair.First.SlideIds.SequenceEqual(pair.Second.SlideIds));

    public void ReplaceShows(IReadOnlyList<CustomShow> shows)
    {
        foreach (var existing in deck.CustomShows.ToList())
            deck.RemoveCustomShow(existing.Name);
        foreach (var show in shows)
            deck.AddCustomShow(show);
    }

    public void ApplyRehearsalTimings(IReadOnlyDictionary<string, int> times)
    {
        var before = deck.Slides.ToDictionary(s => s.Id, s => s.Transition);

        Do(texts["deck.action.rehearse"],
            () =>
            {
                foreach (var slide in deck.Slides)
                    if (times.TryGetValue(slide.Id, out var ms))
                        slide.Transition = slide.Transition with
                        {
                            Advance = AdvanceMode.AfterTime,
                            AdvanceAfterMs = ms,
                        };
                deck.Raise();
            },
            () =>
            {
                foreach (var slide in deck.Slides)
                    if (before.TryGetValue(slide.Id, out var transition))
                        slide.Transition = transition;
                deck.Raise();
            });
    }

    public void SetLanguage(string? language)
    {
        var before = deck.Properties.Language;
        if (before == language) return;

        Do(texts["deck.action.language"],
            () => { deck.Properties.Language = language; deck.Raise(); },
            () => { deck.Properties.Language = before; deck.Raise(); });
    }

    public void SetHeaderFooter(HeaderFooterSettings settings)
    {
        var before = deck.HeaderFooter;
        if (before == settings) return;

        Do(texts["deck.action.headerFooter"],
            () => { deck.HeaderFooter = settings; deck.Raise(); },
            () => { deck.HeaderFooter = before; deck.Raise(); });
    }

    public void SetShowSetup(ShowSetup setup)
    {
        var before = deck.Setup;
        if (before == setup) return;

        Do(texts["deck.action.setupShow"],
            () => { deck.Setup = setup; deck.Raise(); },
            () => { deck.Setup = before; deck.Raise(); });
    }

    private void Do(string description, Action redo, Action undo) => recorder.Do(description, redo, undo);
}
