using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckDesignActions(Presentation deck, ITextCatalog texts, DeckActionRecorder recorder, IGraphicStyleRelinker relinker)
{
    public void ApplyTheme(Theme theme, SlideMaster master)
    {
        var beforeTheme = deck.Theme;
        var beforeMaster = deck.Master;

        Do(texts["deck.action.theme"],
            () => { deck.Theme = theme; deck.Master = master; deck.Raise(); },
            () => { deck.Theme = beforeTheme; deck.Master = beforeMaster; deck.Raise(); });
    }

    public void SetMasterBackground(ColorRole background) =>
        SwapMaster(new MasterEditing().WithBackground(deck.Master, background));

    public void SetLayoutBackground(SlideLayout kind, ColorRole? background) =>
        SwapMaster(new MasterEditing().WithLayoutBackground(deck.Master, kind, background));

    public void SetPlaceholderBounds(SlideLayout kind, PlaceholderRole role, DocumentRect bounds) =>
        SwapMaster(new MasterEditing().WithPlaceholderBounds(deck.Master, kind, role, bounds));

    public void SetPlaceholderBounds(string layoutId, PlaceholderRole role, DocumentRect bounds) =>
        SwapMaster(new MasterEditing().WithPlaceholderBounds(deck.Master, layoutId, role, bounds));

    private void SwapMaster(SlideMaster master)
    {
        var before = deck.Master;
        Do(texts["deck.action.master"],
            () => { deck.Master = master; deck.Raise(); },
            () => { deck.Master = before; deck.Raise(); });
    }

    public void SetNotesMaster(NotesMaster master)
    {
        var before = deck.NotesMaster;
        if (before == master) return;
        Do(texts["deck.action.notesMaster"],
            () => { deck.NotesMaster = master; deck.Raise(); },
            () => { deck.NotesMaster = before; deck.Raise(); });
    }

    public void SetHandoutMaster(HandoutMaster master)
    {
        var before = deck.Handout;
        if (before == master) return;
        Do(texts["deck.action.handoutMaster"],
            () => { deck.Handout = master; deck.Raise(); },
            () => { deck.Handout = before; deck.Raise(); });
    }

    public void AddMaster(SlideMaster master)
    {
        Do(texts["deck.action.newMaster"], () => { deck.AddMaster(master); deck.Raise(); }, () => { deck.RemoveMaster(master.Id); deck.Raise(); });
    }

    public void DeleteMaster(SlideMaster master)
    {
        var index = deck.Masters.ToList().IndexOf(master);
        if (index <= 0) return;
        var users = deck.Slides.Where(slide => slide.MasterId == master.Id).ToList();

        Do(texts["deck.action.deleteMaster"],
            () => { foreach (var slide in users) slide.MasterId = null; deck.RemoveMaster(master.Id); deck.Raise(); },
            () => { deck.InsertMaster(index, master); foreach (var slide in users) slide.MasterId = master.Id; deck.Raise(); });
    }

    public void AssignMaster(IReadOnlyList<Slide> targets, SlideMaster master)
    {
        var primary = ReferenceEquals(master, deck.Master);
        var before = targets.Select(slide => (slide, slide.MasterId)).ToList();
        var target = primary ? null : master.Id;
        if (before.All(entry => entry.MasterId == target)) return;

        Do(texts["deck.action.assignMaster"],
            () => { foreach (var slide in targets) slide.MasterId = target; deck.Raise(); },
            () => { foreach (var (slide, id) in before) slide.MasterId = id; deck.Raise(); });
    }

    public void ReplaceMaster(SlideMaster master)
    {
        if (deck.Masters.FirstOrDefault(existing => existing.Id == master.Id) is not { } before) return;
        Do(texts["deck.action.masterElements"], () => { deck.ReplaceMaster(master); deck.Raise(); }, () => { deck.ReplaceMaster(before); deck.Raise(); });
    }

    public void ChangeGraphicStyles(Action<PulseOffice.Drawing.Shapes.GraphicStyleCatalog> change, Func<EmbeddedStyled, EmbeddedStyled?> relink, string labelKey)
    {
        var beforeStyles = deck.GraphicStyles.All.ToList();
        var beforeElements = deck.Slides.SelectMany(slide => slide.Elements.OfType<ObjectElement>().Select(element => (slide, element))).ToList();
        var beforeMasters = deck.Masters.ToList();

        void Apply()
        {
            change(deck.GraphicStyles);
            relinker.Relink(deck, relink);
            deck.Raise();
        }

        Do(texts[labelKey], Apply, () =>
        {
            deck.GraphicStyles.Restore(beforeStyles);
            foreach (var (slide, element) in beforeElements) if (slide.Find(element.Id) is not null) slide.Replace(element);
            deck.Master = beforeMasters[0];
            foreach (var master in beforeMasters.Skip(1)) deck.ReplaceMaster(master);
            deck.Raise();
        });
    }

    public void ApplyGraphicStyle(IEnumerable<PulseOffice.Document.Embedding.EmbeddedObject> contents, PulseOffice.Drawing.Shapes.GraphicStyle style)
    {
        var selected = contents.ToHashSet<PulseOffice.Document.Embedding.EmbeddedObject>(ReferenceEqualityComparer.Instance);
        if (selected.Count == 0) return;
        ChangeGraphicStyles(_ => { }, styled => selected.Contains(styled.Content) ? styled.With(style.Style, style.Name) : null, "deck.style.applied");
    }

    private void Do(string description, Action redo, Action undo) => recorder.Do(description, redo, undo);
}
