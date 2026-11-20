using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class DeckLayoutActions(
    Presentation deck, UndoStack history, ITextCatalog texts, DeckActionRecorder recorder, ISlideFactory slides,
    ISlideLayoutCatalog catalog, DeckSlideActions slideActions, DeckDesignActions design)
{
    public ISlideLayoutCatalog Catalog => catalog;

    public Slide? AddSlide(SlideMaster master, SlideLayoutTemplate template, int afterIndex)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(template);
        if (recorder.IsReadOnly || !deck.Masters.Contains(master) || !master.Layouts.Contains(template)) return null;
        var index = Math.Clamp(afterIndex + 1, 0, deck.Slides.Count);
        var slide = slides.Create(deck.NextId(), template);
        slide.MasterId = ReferenceEquals(master, deck.Master) ? null : master.Id;

        recorder.Do(texts["deck.action.addSlide"],
            () => { deck.InsertExisting(index, slide); recorder.Focus(slide); },
            () => deck.Remove(slide));
        return slide;
    }

    public void ApplyLayout(Slide slide, SlideMaster master, SlideLayoutTemplate template)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(template);
        if (recorder.IsReadOnly || deck.IndexOf(slide) < 0 || !deck.Masters.Contains(master) || !master.Layouts.Contains(template)) return;
        if (ReferenceEquals(deck.MasterOf(slide), master))
        {
            slideActions.SetLayout(slide, template);
            return;
        }

        using var transaction = history.BeginTransaction(texts["deck.action.layout"]);
        design.AssignMaster([slide], master);
        slideActions.Relayout(slide, template);
    }

    public void ChangeMaster(IReadOnlyList<Slide> targets, SlideMaster master)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(master);
        if (recorder.IsReadOnly || !deck.Masters.Contains(master)) return;
        var moving = targets
            .Where(slide => deck.IndexOf(slide) >= 0 && !ReferenceEquals(deck.MasterOf(slide), master))
            .Select(slide => (Slide: slide, Template: Counterpart(master, deck.MasterOf(slide).Template(slide))))
            .ToList();
        if (moving.Count == 0) return;

        using var transaction = history.BeginTransaction(texts["deck.action.assignMaster"]);
        design.AssignMaster([.. moving.Select(entry => entry.Slide)], master);
        foreach (var (slide, template) in moving) slideActions.Relayout(slide, template);
    }

    public void DeleteMaster(SlideMaster master)
    {
        ArgumentNullException.ThrowIfNull(master);
        if (recorder.IsReadOnly || deck.Masters.ToList().IndexOf(master) <= 0) return;
        using var transaction = history.BeginTransaction(texts["deck.action.deleteMaster"]);
        ChangeMaster([.. deck.Slides.Where(slide => ReferenceEquals(deck.MasterOf(slide), master))], deck.Master);
        design.DeleteMaster(master);
    }

    private SlideLayoutTemplate Counterpart(SlideMaster master, SlideLayoutTemplate current) =>
        master.Layouts.FirstOrDefault(layout => layout.Kind == current.Kind && string.Equals(layout.Name, current.Name, StringComparison.Ordinal))
            ?? master.Template(current.Kind);

    public void RenameLayout(SlideMaster master, string layoutId, string name)
    {
        var renamed = catalog.Rename(master, layoutId, name);
        if (!ReferenceEquals(renamed, master)) Swap(texts["deck.action.renameLayout"], master, renamed);
    }

    public SlideLayoutTemplate? DuplicateLayout(SlideMaster master, string layoutId, string name)
    {
        if (catalog.Find(master, layoutId) is null || recorder.IsReadOnly) return null;
        var duplication = catalog.Duplicate(master, layoutId, name);
        Swap(texts["deck.action.duplicateLayout"], master, duplication.Master);
        return duplication.Copy;
    }

    public void DeleteLayout(SlideMaster master, string layoutId)
    {
        if (recorder.IsReadOnly || !deck.Masters.Contains(master) || catalog.Find(master, layoutId) is not { } layout || catalog.IsStandard(master, layout)) return;
        var reduced = catalog.Remove(master, layoutId);
        var moved = deck.Slides
            .Where(slide => ReferenceEquals(deck.MasterOf(slide), master) && ReferenceEquals(master.Template(slide), layout))
            .Select(slide => (Before: slide, After: slides.Relayout(slide, reduced, reduced.Template(layout.Kind))))
            .ToList();

        recorder.Do(texts["deck.action.deleteLayout"],
            () =>
            {
                deck.ReplaceMaster(reduced);
                foreach (var (before, after) in moved) deck.ReplaceAt(deck.IndexOf(before), after);
                deck.Raise();
            },
            () =>
            {
                deck.ReplaceMaster(master);
                foreach (var (before, after) in moved) deck.ReplaceAt(deck.IndexOf(after), before);
                deck.Raise();
            });
    }

    private void Swap(string label, SlideMaster before, SlideMaster after)
    {
        if (!deck.Masters.Contains(before)) return;
        recorder.Do(label,
            () => { deck.ReplaceMaster(after); deck.Raise(); },
            () => { deck.ReplaceMaster(before); deck.Raise(); });
    }
}
