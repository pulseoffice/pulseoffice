using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckActions
{
    private readonly UndoStack _history;
    private readonly DeckActionRecorder _recorder;
    private readonly DeckSlideActions _slides;
    private readonly DeckElementActions _elements;
    private readonly DeckTextActions _text;
    private readonly DeckDesignActions _design;
    private readonly DeckShowActions _show;
    private readonly DeckLayoutActions _layouts;

    public DeckActions(
        Presentation deck, UndoStack history, ISlideFactory slides, ITextCatalog texts, Func<bool>? readOnly = null,
        IFreeformRefit? refit = null, PulseOffice.Document.Forms.IOptionGroups? optionGroups = null,
        Rendering.ITextFrameFitting? fitting = null, PulseOffice.Drawing.Shapes.IContentFlipping? flipping = null,
        ITextEditSplice? splice = null, ISlideGroupEditing? groupEditing = null, IElementBehaviourCloning? behaviourCloning = null,
        ISlideLayoutCatalog? layoutCatalog = null, IGraphicStyleRelinker? styleRelinker = null)
    {
        _history = history;
        _recorder = new DeckActionRecorder(deck, history, readOnly, fitting ?? new Rendering.NoTextFrameFitting());
        _recorder.FocusRequested += slide => SlideFocusRequested?.Invoke(this, slide);
        _show = new DeckShowActions(deck, texts, _recorder);
        _slides = new DeckSlideActions(deck, texts, _recorder, slides, _show);
        _elements = new DeckElementActions(deck, texts, _recorder,
            groupEditing ?? new SlideGroupEditing(), flipping ?? new PulseOffice.Drawing.Shapes.ContentFlipping(),
            refit ?? new FreeformRefit(), optionGroups ?? new PulseOffice.Document.Forms.OptionGroups(),
            behaviourCloning ?? new ElementBehaviourCloning());
        _text = new DeckTextActions(deck, texts, _recorder, splice ?? new TextEditSplice());
        _design = new DeckDesignActions(deck, texts, _recorder, styleRelinker ?? new GraphicStyleRelinker());
        _layouts = new DeckLayoutActions(deck, history, texts, _recorder, slides, layoutCatalog ?? new SlideLayoutCatalog(), _slides, _design);
    }

    public DeckLayoutActions Layouts => _layouts;

    public bool IsReadOnly => _recorder.IsReadOnly;

    public UndoStack History => _history;

    public event EventHandler<Slide>? SlideFocusRequested;

    public TextElement FitPreview(Slide slide, TextElement element) => _recorder.Fitted(slide, element);

    public Slide? AddSlide(SlideLayout layout, int afterIndex) =>
        _slides.AddSlide(layout, afterIndex);

    public void DeleteSlide(Slide slide) =>
        _slides.DeleteSlide(slide);

    public Slide? DuplicateSlide(Slide slide) =>
        _slides.DuplicateSlide(slide);

    public void ImportSlides(int afterIndex, IReadOnlyList<Slide> sources) =>
        _slides.ImportSlides(afterIndex, sources);

    public IReadOnlyList<Slide> InsertOutlineSlides(int afterIndex, IReadOnlyList<OutlineSlide> outline, string labelKey) =>
        _slides.InsertOutlineSlides(afterIndex, outline, labelKey);

    public int InsertAlbum(int afterIndex, IReadOnlyList<AlbumPage> pages, IReadOnlyList<PulseOffice.Drawing.Assets.RasterImageObject> photos, bool captions) =>
        _slides.InsertAlbum(afterIndex, pages, photos, captions);

    public void MoveSlide(Slide slide, int to) =>
        _slides.MoveSlide(slide, to);

    public void SetLayout(Slide slide, SlideLayout layout) =>
        _slides.SetLayout(slide, layout);

    public void SetLayout(Slide slide, SlideLayoutTemplate template) =>
        _slides.SetLayout(slide, template);

    public void ApplyTheme(Theme theme, SlideMaster master) =>
        _design.ApplyTheme(theme, master);

    public void SetMasterBackground(ColorRole background) =>
        _design.SetMasterBackground(background);

    public void SetPlaceholderBounds(string layoutId, PlaceholderRole role, DocumentRect bounds) =>
        _design.SetPlaceholderBounds(layoutId, role, bounds);

    public void SetLayoutBackground(SlideLayout kind, ColorRole? background) =>
        _design.SetLayoutBackground(kind, background);

    public void SetPlaceholderBounds(SlideLayout kind, PlaceholderRole role, DocumentRect bounds) =>
        _design.SetPlaceholderBounds(kind, role, bounds);

    public void SetSlideSize(SlideSize size, bool fitContents) =>
        _slides.SetSlideSize(size, fitContents);

    public void SetTransition(Slide slide, SlideTransition transition) =>
        _show.SetTransition(slide, transition);

    public void SetTransitions(IReadOnlyList<Slide> targets, SlideTransition transition) =>
        _show.SetTransitions(targets, transition);

    public void ReplaceAnimations(Slide slide, IReadOnlyList<AnimationEffect> animations) =>
        _show.ReplaceAnimations(slide, animations);

    public void AnimateElement(Slide slide, SlideElement element) =>
        _show.AnimateElement(slide, element);

    public void AddAnimation(Slide slide, AnimationEffect effect) =>
        _show.AddAnimation(slide, effect);

    public void RemoveAnimation(Slide slide, AnimationEffect effect) =>
        _show.RemoveAnimation(slide, effect);

    public void EditText(Slide slide, string elementId, RichText updated) =>
        _text.EditText(slide, elementId, updated);

    public int RewriteTexts(string labelKey, Func<RichText, RichText> text, Func<string, string>? notes = null) =>
        _text.RewriteTexts(labelKey, text, notes);

    public void InsertMedia(Slide slide, MediaElement media) =>
        _elements.InsertMedia(slide, media);

    public void ReplaceElementsWith(Slide slide, IReadOnlyList<string> removed, SlideElement added, string labelKey) =>
        _elements.ReplaceElementsWith(slide, removed, added, labelKey);

    public void SetNotesMaster(NotesMaster master) =>
        _design.SetNotesMaster(master);

    public void SetHandoutMaster(HandoutMaster master) =>
        _design.SetHandoutMaster(master);

    public void AddFormFields(Slide slide, IReadOnlyList<ObjectElement> elements, PulseOffice.Document.Forms.FormDocumentSettings settings) =>
        _elements.AddFormFields(slide, elements, settings);

    public void InsertObject(Slide slide, ObjectElement element) =>
        _elements.InsertObject(slide, element);

    public void EditTable(Slide slide, string elementId, Func<PulseOffice.Document.Embedding.DataTableObject, PulseOffice.Document.Embedding.DataTableObject> change) =>
        _elements.EditTable(slide, elementId, change);

    public void ReplaceTable(Slide slide, string elementId,
        PulseOffice.Document.Embedding.DataSnapshot snapshot, IReadOnlyList<PulseOffice.Document.Embedding.TableMerge> merges,
        IReadOnlyList<PulseOffice.Document.Embedding.TableCellFormat> cellFormats) =>
        _elements.ReplaceTable(slide, elementId, snapshot, merges, cellFormats);

    public void AppendToText(Slide slide, string elementId, string text) =>
        _text.AppendToText(slide, elementId, text);

    public void AppendRun(Slide slide, string elementId, TextRun run) =>
        _text.AppendRun(slide, elementId, run);

    public void InsertElement(Slide slide, SlideElement element) =>
        _elements.InsertElement(slide, element);

    public void DeleteObject(Slide slide, SlideElement element) =>
        _elements.DeleteObject(slide, element);

    public void DeleteElements(Slide slide, IReadOnlyList<SlideElement> elements, string labelKey) =>
        _elements.DeleteElements(slide, elements, labelKey);

    public IReadOnlyList<SlideElement> AddClones(
        Slide slide, IReadOnlyList<SlideElement> sources, int dx, int dy, string labelKey, IReadOnlyList<SlideGroup>? groups = null,
        ElementBehaviourSet? behaviours = null) =>
        _elements.AddClones(slide, sources, dx, dy, labelKey, groups, behaviours);

    public ElementBehaviourSet BehavioursOf(Slide slide, IReadOnlyList<SlideElement> elements) =>
        _elements.BehavioursOf(slide, elements);

    public void EditImage(Slide slide, string elementId,
        Func<PulseOffice.Drawing.Assets.RasterImageObject, PulseOffice.Drawing.Assets.RasterImageObject> change, string labelKey) =>
        _elements.EditImage(slide, elementId, change, labelKey);

    public void FlipElement(Slide slide, string elementId, bool horizontal) =>
        _elements.FlipElement(slide, elementId, horizontal);

    public void ReplaceGroups(Slide slide, IReadOnlyList<SlideGroup> groups, string labelKey) =>
        _elements.ReplaceGroups(slide, groups, labelKey);

    public void TransformGroup(Slide slide, SlideGroup group, DocumentRect bounds, double rotation) =>
        _elements.TransformGroup(slide, group, bounds, rotation);

    public void ConvertGroup(Slide slide, ObjectElement group, SlideGroupLayout layout) =>
        _elements.ConvertGroup(slide, group, layout);

    public void UngroupObject(Slide slide, ObjectElement group, IReadOnlyList<ObjectElement> children) =>
        _elements.UngroupObject(slide, group, children);

    public void TransformElement(Slide slide, string elementId, DocumentRect bounds, double rotation) =>
        _elements.TransformElement(slide, elementId, bounds, rotation);

    public void ReplaceElements(Slide slide, IReadOnlyList<SlideElement> updated, string labelKey) =>
        _elements.ReplaceElements(slide, updated, labelKey);

    public void EditFreeformPath(Slide slide, ObjectElement element, PulseOffice.Drawing.Freeform.FreeformPath path) =>
        _elements.EditFreeformPath(slide, element, path);

    public void AdjustShape(Slide slide, ObjectElement element, PulseOffice.Drawing.Shapes.ShapeObject shape) =>
        _elements.AdjustShape(slide, element, shape);

    public void EditShapeText(Slide slide, ObjectElement element, PulseOffice.Drawing.Shapes.ShapeObject shape, bool format) =>
        _elements.EditShapeText(slide, element, shape, format);

    public void ChangeControl(Slide slide, ObjectElement updated) =>
        _elements.ChangeControl(slide, updated);

    public void ReplaceElement(Slide slide, SlideElement updated, string labelKey) =>
        _elements.ReplaceElement(slide, updated, labelKey);

    public void ReorderElements(Slide slide, IReadOnlyList<(string Id, int ZOrder)> order) =>
        _elements.ReorderElements(slide, order);

    public void AlignElements(Slide slide, IReadOnlyList<(string Id, DocumentRect Bounds)> placements) =>
        _elements.AlignElements(slide, placements);

    public void SetInteraction(Slide slide, string elementId, InteractiveAction? action) =>
        _elements.SetInteraction(slide, elementId, action);

    public void SetCustomShows(IReadOnlyList<CustomShow> shows) =>
        _show.SetCustomShows(shows);

    public void ApplyRehearsalTimings(IReadOnlyDictionary<string, int> times) =>
        _show.ApplyRehearsalTimings(times);

    public void SetLanguage(string? language) =>
        _show.SetLanguage(language);

    public void SetHeaderFooter(HeaderFooterSettings settings) =>
        _show.SetHeaderFooter(settings);

    public void SetShowSetup(ShowSetup setup) =>
        _show.SetShowSetup(setup);

    public void SetSection(Slide slide, string? name) =>
        _slides.SetSection(slide, name);

    public void SetBackgroundImage(Slide slide, PulseOffice.Drawing.Assets.RasterImageObject? image) =>
        _slides.SetBackgroundImage(slide, image);

    public void SetBackgroundFill(Slide slide, PulseOffice.Drawing.Shapes.ShapeFill? fill) =>
        _slides.SetBackgroundFill(slide, fill);

    public void SetBackgrounds(IReadOnlyList<Slide> targets, SlideBackgroundChoice choice) =>
        _slides.SetBackgrounds(targets, choice);

    public void RenameSlide(Slide slide, string? name) =>
        _slides.RenameSlide(slide, name);

    public void AddMaster(SlideMaster master) =>
        _design.AddMaster(master);

    public void DeleteMaster(SlideMaster master) =>
        _design.DeleteMaster(master);

    public void AssignMaster(IReadOnlyList<Slide> targets, SlideMaster master) =>
        _design.AssignMaster(targets, master);

    public void ReplaceMaster(SlideMaster master) =>
        _design.ReplaceMaster(master);

    public void ChangeGraphicStyles(Action<PulseOffice.Drawing.Shapes.GraphicStyleCatalog> change, Func<EmbeddedStyled, EmbeddedStyled?> relink, string labelKey) =>
        _design.ChangeGraphicStyles(change, relink, labelKey);

    public void ApplyGraphicStyle(IEnumerable<PulseOffice.Document.Embedding.EmbeddedObject> contents, PulseOffice.Drawing.Shapes.GraphicStyle style) =>
        _design.ApplyGraphicStyle(contents, style);

    public void SetMasterDisplay(Slide slide, bool background, bool objects) =>
        _slides.SetMasterDisplay(slide, background, objects);

    public void SetHidden(Slide slide, bool hidden) =>
        _slides.SetHidden(slide, hidden);

    public void AddComment(Slide slide, SlideComment comment) =>
        _text.AddComment(slide, comment);

    public void EditComment(Slide slide, SlideComment before, string text) =>
        _text.EditComment(slide, before, text);

    public void RemoveComments(IReadOnlyList<(Slide Slide, SlideComment Comment)> comments) =>
        _text.RemoveComments(comments);

    public void SetNotes(Slide slide, string notes) =>
        _text.SetNotes(slide, notes);

    public void SetSlideText(Slide slide, PlaceholderRole role, string text) =>
        _text.SetSlideText(slide, role, text);

    public void SetSlideTexts(IReadOnlyList<(Slide Slide, PlaceholderRole Role, string Text)> changes) =>
        _text.SetSlideTexts(changes);
}
