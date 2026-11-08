using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.Presentations.UI;

public sealed class DeckViewCommands(IActiveDeckContext context) : DeckCommandSet(context)
{
    private static readonly (string Id, DeckViewMode Mode)[] Modes =
    [
        ("normal", DeckViewMode.Normal),
        ("sorter", DeckViewMode.Sorter),
        ("outline", DeckViewMode.Outline),
        ("notes", DeckViewMode.Notes),
        ("master", DeckViewMode.Master),
        ("notesMaster", DeckViewMode.NotesMaster),
        ("handoutMaster", DeckViewMode.HandoutMaster),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. Modes.Select(entry => Prototype($"deck.view.{entry.Id}") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is { } t && t.ViewMode() != entry.Mode,
            CheckedWhen = _ => Context.Target?.ViewMode() == entry.Mode,
            Execute = (_, _) =>
            {
                Context.Target?.SetViewMode(entry.Mode);
                return Task.CompletedTask;
            },
        }),

        Option("deck.view.slidePane", o => o.ToggleSlidePane(), o => o.SlidePane),
        Option("deck.view.notesPane", o => o.ToggleNotesPane(), o => o.NotesPane),
        Option("deck.view.comments", o => o.ToggleComments(), o => o.ShowComments),
        Option("deck.view.qualityColor", o => o.SetQuality(SlideOutputQuality.Color), o => o.Quality == SlideOutputQuality.Color, o => o.Quality != SlideOutputQuality.Color),
        Option("deck.view.qualityGrayscale", o => o.SetQuality(SlideOutputQuality.Grayscale), o => o.Quality == SlideOutputQuality.Grayscale, o => o.Quality != SlideOutputQuality.Grayscale),
        Option("deck.view.qualityBlackWhite", o => o.SetQuality(SlideOutputQuality.BlackAndWhite), o => o.Quality == SlideOutputQuality.BlackAndWhite, o => o.Quality != SlideOutputQuality.BlackAndWhite),
        Grid("deck.view.grid", deck => deck.Grid.ShowGrid = !deck.Grid.ShowGrid, deck => deck.Grid.ShowGrid),
        Grid("deck.view.rulers", deck => deck.Grid.ShowRulers = !deck.Grid.ShowRulers, deck => deck.Grid.ShowRulers),
        Grid("deck.view.snap", deck => deck.Grid.SnapToGrid = !deck.Grid.SnapToGrid, deck => deck.Grid.SnapToGrid),
        Grid("deck.view.guides", deck => deck.Grid.ShowGuides = !deck.Grid.ShowGuides, deck => deck.Grid.ShowGuides),
        Grid("deck.view.gridFront", deck => deck.Grid.GridInFront = !deck.Grid.GridInFront, deck => deck.Grid.GridInFront),
        Grid("deck.view.guidesFront", deck => deck.Grid.GuidesInFront = !deck.Grid.GuidesInFront, deck => deck.Grid.GuidesInFront),
        Grid("deck.view.helplinesMove", deck => deck.Grid.HelplinesWhileMoving = !deck.Grid.HelplinesWhileMoving, deck => deck.Grid.HelplinesWhileMoving),
        Grid("deck.view.snapGuides", deck => deck.Grid.SnapToGuides = !deck.Grid.SnapToGuides, deck => deck.Grid.SnapToGuides),
        Grid("deck.view.snapFrames", deck => deck.Grid.SnapToObjectFrames = !deck.Grid.SnapToObjectFrames, deck => deck.Grid.SnapToObjectFrames),
        Grid("deck.view.snapPoints", deck => deck.Grid.SnapToObjectPoints = !deck.Grid.SnapToObjectPoints, deck => deck.Grid.SnapToObjectPoints),
        Grid("deck.view.snapBorder", deck => deck.Grid.SnapToPageMargins = !deck.Grid.SnapToPageMargins, deck => deck.Grid.SnapToPageMargins),
        Grid("deck.guide.addH", deck => deck.Grid.AddGuide(new SlideGuide(true, deck.Size.Height.Value / 2)), null, deck => deck.Grid.ShowGuides),
        Grid("deck.guide.addV", deck => deck.Grid.AddGuide(new SlideGuide(false, deck.Size.Width.Value / 2)), null, deck => deck.Grid.ShowGuides),
        Grid("deck.guide.clear", deck => deck.Grid.ClearGuides(), null, deck => deck.Grid.ShowGuides && deck.Grid.Guides.Count > 0),
    ];

    private CommandDefinition Option(string id, Action<DeckViewOptions> act, Func<DeckViewOptions, bool> isOn, Func<DeckViewOptions, bool>? enabled = null) =>
        Prototype(id) with
        {
            CheckedWhen = _ => Context.Target is { } t && isOn(t.Options),
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is { } t && (enabled?.Invoke(t.Options) ?? true),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t)
                {
                    act(t.Options);
                    Context.Attach(t);
                }
                return Task.CompletedTask;
            },
        };

    private CommandDefinition Grid(string id, Action<Presentation> act, Func<Presentation, bool>? isOn = null, Func<Presentation, bool>? enabled = null) =>
        Prototype(id) with
        {
            CheckedWhen = isOn is null ? null : _ => Context.Target is { } t && isOn(t.Deck),
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is { } t && (enabled?.Invoke(t.Deck) ?? true),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t)
                {
                    act(t.Deck);
                    t.Deck.Raise();
                    Context.Attach(t);
                }
                return Task.CompletedTask;
            },
        };
}
