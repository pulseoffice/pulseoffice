using PulseOffice.Document.Embedding;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Exchange;

public sealed record DeckNative(IReadOnlyList<SlideElement> Elements)
{
    public IReadOnlyList<SlideGroup> Groups { get; init; } = [];

    public ElementBehaviourSet? Behaviours { get; init; }
}

public sealed class DeckExchange(
    IActiveDeckContext context,
    IDeckSelectionPicture pictures,
    IDeckTextInsertion insertion,
    IDeckRichTextTransfer richText,
    ICellGridConversions grids,
    IDeckSelectionPackages packages,
    PulseOffice.UI.Embedding.ISheetObjectFactory sheets,
    IDeckPortableTextBox textBoxes,
    PulseOffice.Shell.IHtmlTableGrids htmlTables,
    IFreeAreaPlacement placement,
    ISlideGroupEditing groupEditing,
    ISlideGroupDecomposition decomposition,
    IDeckGrouping grouping,
    ISlideGroupUnits units) : IExchangeParticipant
{
    private const int PasteOffset = 200;
    private const double FreeFontSize = 18;

    public ModuleKind Module => ModuleKind.Presentations;

    public bool CanCapture => context.Target is { } target && target.CurrentSlide() is not null && target.SelectedMany().Count > 0;

    public bool CanReceive => context.Target is { } target && target.CurrentSlide() is not null && !target.Actions.IsReadOnly;

    public ExchangePackage? Capture(string id, bool cut)
    {
        if (context.Target is not { } target || target.CurrentSlide() is not { } slide) return null;
        var selection = target.SelectedMany();
        if (selection.Count == 0) return null;

        var groups = groupEditing.Within(slide.Groups, [.. selection.Select(element => element.Id)]);
        var objects = Portables(slide, selection, groups);
        var text = string.Join("\n", selection.Select(Text).Where(part => part.Length > 0));
        var (html, rtf) = richText.Export([.. selection.OfType<TextElement>().SelectMany(element => element.Text.Paragraphs)]);
        if (html is not { Length: > 0 } && selection is [ObjectElement { Content: DataTableObject table }])
            html = grids.Html(grids.Grid(table, System.Globalization.CultureInfo.CurrentCulture));
        var representations = new List<ExchangeRepresentation>();
        if (text.Length > 0) representations.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Text, text));
        if (html is { Length: > 0 }) representations.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Html, html));
        if (rtf is { Length: > 0 }) representations.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Rtf, rtf));
        if (pictures.Render(target.Deck, slide, selection) is { Length: > 0 } png) representations.Add(new ExchangeRepresentation(ExchangeFlavor.Png, png));
        var behaviours = target.Actions.BehavioursOf(slide, selection);
        representations.AddRange(packages.Packages(target.Deck, slide, selection, behaviours));

        var package = new ExchangePackage
        {
            Id = id,
            Origin = Module,
            Content = objects.Count > 0 ? new ObjectsContent(objects) : null,
            Native = new DeckNative([.. selection]) { Groups = groups, Behaviours = behaviours },
            Representations = representations,
        };
        if (cut && !target.Actions.IsReadOnly)
        {
            target.Actions.DeleteElements(slide, selection, "deck.action.cut");
            target.Select([]);
        }
        return package;
    }

    public IReadOnlyList<ExchangePasteMode> Modes(ExchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var modes = new List<ExchangePasteMode>();
        if (package.NativeFrom<DeckNative>(Module) is not null) modes.Add(ExchangePasteMode.Native);
        else if (package.Content is ObjectsContent) modes.Add(ExchangePasteMode.Objects);
        if (package.Content is CellRangeContent)
        {
            modes.Add(ExchangePasteMode.Table);
            if (package.Find(ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Spreadsheet) is not null) modes.Add(ExchangePasteMode.SheetObject);
        }
        else if (htmlTables.Grid(package) is not null) modes.Add(ExchangePasteMode.Table);
        if (Paragraphs(package) is not null) modes.Add(ExchangePasteMode.FormattedText);
        if (package.TextOf(ExchangeFlavor.Text) is { Length: > 0 }) modes.Add(ExchangePasteMode.UnformattedText);
        if (package.Find(ExchangeFlavor.Png) is not null) modes.Add(ExchangePasteMode.Image);
        return modes;
    }

    public bool Receive(ExchangePackage package, ExchangePasteMode mode)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (context.Target is not { } target || target.CurrentSlide() is not { } slide || target.Actions.IsReadOnly) return false;
        switch (mode)
        {
            case ExchangePasteMode.Native when package.NativeFrom<DeckNative>(Module) is { } native:
                return Place(target, slide, native.Elements, PasteOffset, native.Groups, native.Behaviours);
            case ExchangePasteMode.Objects when package.Content is ObjectsContent content:
                return PlaceForeign(target, slide, Elements(target.Deck, slide, content.Objects));
            case ExchangePasteMode.Table when (package.Content as CellRangeContent ?? htmlTables.Grid(package)) is { } grid:
                var table = grids.Table(grid, target.Deck.NextId());
                return Place(target, slide, Elements(target.Deck, slide, [new ExchangedObject(table, table.NaturalSize)]), 0, []);
            case ExchangePasteMode.SheetObject when package.Content is CellRangeContent grid && package.Find(ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Spreadsheet) is { } ods:
                var sheet = sheets.Create(target.Deck.NextId(), grid, ods);
                return Place(target, slide, Elements(target.Deck, slide, [new ExchangedObject(sheet, sheet.NaturalSize)]), 0, []);
            case ExchangePasteMode.FormattedText when Paragraphs(package) is { } paragraphs:
                insertion.Insert(target, paragraphs, FreeFontSize);
                return true;
            case ExchangePasteMode.UnformattedText when package.TextOf(ExchangeFlavor.Text) is { Length: > 0 } text:
                if (target.IsTyping() && target.TypeAtCaret(text)) return true;
                insertion.Insert(target, new TextRun(text), FreeFontSize);
                return true;
            case ExchangePasteMode.Image when package.BytesOf(ExchangeFlavor.Png) is { } png:
                return target.InsertImageBytes(png, "image.png");
            default:
                return false;
        }
    }

    private IReadOnlyList<TextParagraph>? Paragraphs(ExchangePackage package) =>
        package.Content is ObjectsContent ? null : richText.Import(package.TextOf(ExchangeFlavor.Rtf), package.TextOf(ExchangeFlavor.Html));

    private List<SlideElement> Elements(Presentation deck, Slide slide, IReadOnlyList<ExchangedObject> objects)
    {
        var size = deck.Size;
        DocumentRect? first = null;
        return [.. objects.Select((item, index) =>
        {
            var extent = item.Size.IsEmpty ? item.Object.NaturalSize : item.Size;
            var width = Math.Min(extent.Width.Value, size.Width.Value);
            var height = Math.Min(extent.Height.Value, size.Height.Value);
            first ??= placement.Place(new DocumentSize(size.Width, size.Height), new DocumentSize(new Twip(width), new Twip(height)), slide.Elements.Select(element => element.Bounds));
            var shift = PasteOffset * index;
            return (SlideElement)new ObjectElement
            {
                Id = item.Object.Id,
                Content = item.Object,
                Bounds = new DocumentRect(new Twip(first.Value.Left.Value + shift), new Twip(first.Value.Top.Value + shift), new Twip(width), new Twip(height)),
            };
        })];
    }

    private bool PlaceForeign(DeckTarget target, Slide slide, IReadOnlyList<SlideElement> elements)
    {
        var leaves = new List<SlideElement>();
        var groups = new List<SlideGroup>();
        foreach (var element in elements)
        {
            if (element is not ObjectElement { Content: Drawing.Shapes.ShapeGroupObject } placed)
            {
                leaves.Add(element);
                continue;
            }
            var layout = decomposition.Decompose(placed);
            leaves.AddRange(layout.Leaves);
            groups.AddRange(layout.Groups);
        }
        return Place(target, slide, leaves, 0, groups);
    }

    private List<ExchangedObject> Portables(Slide slide, IReadOnlyList<SlideElement> selection, IReadOnlyList<SlideGroup> groups)
    {
        var result = new List<ExchangedObject>();
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groupEditing.Outermost(groups))
        {
            var leaves = units.Leaves(slide, group).Select(slide.Find).ToList();
            if (leaves.Any(leaf => leaf is not ObjectElement) || grouping.Group([.. leaves.OfType<ObjectElement>()], group.Id) is not { } composed) continue;
            result.Add(new ExchangedObject(composed.Content, new DocumentSize(composed.Bounds.Width, composed.Bounds.Height)));
            foreach (var leaf in leaves) covered.Add(leaf!.Id);
        }
        result.AddRange(selection.Where(element => !covered.Contains(element.Id)).Select(Portable).OfType<ExchangedObject>());
        return result;
    }

    private bool Place(DeckTarget target, Slide slide, IReadOnlyList<SlideElement> elements, int offset, IReadOnlyList<SlideGroup> groups,
        ElementBehaviourSet? behaviours = null)
    {
        var clones = target.Actions.AddClones(slide, elements, offset, offset, "deck.action.paste", groups, behaviours);
        if (clones.Count == 0) return false;
        target.Select(clones);
        return true;
    }

    private ExchangedObject? Portable(SlideElement element) => element switch
    {
        ObjectElement placed => new ExchangedObject(placed.Content, new DocumentSize(placed.Bounds.Width, placed.Bounds.Height)),
        TextElement text when textBoxes.Portable(text) is { } shape => new ExchangedObject(shape, new DocumentSize(text.Bounds.Width, text.Bounds.Height)),
        _ => null,
    };

    private string Text(SlideElement element) => element switch
    {
        TextElement text => text.Text.PlainText,
        ObjectElement { Content: Drawing.Shapes.ShapeObject { Text: { Length: > 0 } shape } } => shape,
        ObjectElement { Content: DataTableObject table } => grids.Tabular(grids.Grid(table, System.Globalization.CultureInfo.CurrentCulture)).TrimEnd('\n'),
        _ => string.Empty,
    };
}
