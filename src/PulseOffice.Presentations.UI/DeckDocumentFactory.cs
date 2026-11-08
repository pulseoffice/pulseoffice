using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Shell;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI;

public interface IDeckDocumentFactory
{
    OpenDocument CreateEmpty();

    OpenDocument CreateFrom(Presentation deck, string title);
}

public sealed class DeckDocumentFactory(
    ISlideFactory slides,
    IMasterFactory masters,
    IThemeCatalog themes,
    IAnimationScheduler scheduler,
    IDrawingObjectRenderer objectRenderer,
    ITextCatalog texts,
    IContextMenuFactory contextMenus,
    IActiveDeckContext deckContext,
    IDeckConnectors connectors,
    IDeckDrawingState drawing,
    PulseOffice.UI.Controls.Objects.IRasterImageFiles images,
    ICanvasContextChooser contextChooser,
    PulseOffice.Text.Spelling.IAutoCorrect autoCorrect,
    PulseOffice.Text.Spelling.IAutoCorrectSettings autoCorrectSettings,
    IDeckSpelling spelling,
    PulseOffice.Text.Spelling.IAutoSpellSettings autoSpell,
    PulseOffice.Text.Spelling.IUserDictionary userWords,
    PulseOffice.UI.Controls.Forms.IFormDesignMode design,
    PulseOffice.Document.Forms.IFormControlInteraction forms,
    PulseOffice.Document.Forms.IOptionGroups optionGroups,
    Rendering.ITextFrameFitting fitting,
    PulseOffice.UI.Controls.Text.Editing.InlineEditingServices editing,
    IRibbonStateNotifier ribbonState,
    ObjectDialogs.IDeckObjectEditing objectEditing,
    PulseOffice.Text.Spelling.Dictionaries.IDictionaryChanges dictionaryChanges,
    PulseOffice.Text.Rich.IRichTextFormatter formatter,
    PulseOffice.UI.Controls.Zoom.IZoomLadder zoomLadder,
    PulseOffice.Text.Rich.IRichTextInsertion richInsertion,
    ISlideGroupEditing groupEditing,
    Motion.IDeckMotionEditors motionEditors,
    PulseOffice.Drawing.Assets.IImageExtents extents,
    IInterfaceCulture culture) : IDeckDocumentFactory
{
    private int _counter;

    public OpenDocument CreateEmpty()
    {
        var theme = themes.Default;
        var master = masters.CreateStandard(theme);

        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(slides.Create(deck.NextId(), master, SlideLayout.TitleSlide));

        return Build(deck, $"{ModuleIdentity.Presentations.BrandName}{++_counter}");
    }

    public OpenDocument CreateFrom(Presentation deck, string title) =>
        Build(deck, System.IO.Path.GetFileNameWithoutExtension(title));

    private OpenDocument Build(Presentation deck, string title)
    {
        if (string.IsNullOrEmpty(deck.TextLanguage))
            deck.DefaultLanguages = deck.DefaultLanguages with { Western = culture.Culture.Name };
        var history = new UndoStack();
        OpenDocument? document = null;
        var actions = new DeckActions(deck, history, slides, texts, () => document?.IsReadOnly == true, optionGroups: optionGroups, fitting: fitting, groupEditing: groupEditing);
        var renderer = new SlideRenderer(objectRenderer, texts);
        var view = new DeckView(deck, actions, renderer, masters, scheduler, texts, contextMenus, deckContext, connectors, drawing, images, contextChooser, autoCorrect, autoCorrectSettings, spelling, autoSpell, userWords, design, forms, editing, formatter, zoomLadder, extents: extents)
        {
            RibbonState = ribbonState,
            ObjectEditing = objectEditing,
            DictionaryChanges = dictionaryChanges,
            RichInsertion = richInsertion,
            MotionEditors = motionEditors,
        };

        document = new OpenDocument(ModuleKind.Presentations, title, view, history)
        {
            Model = deck,
        };
        document.Changed += (_, _) => view.ApplyReadOnly();

        return document;
    }
}
