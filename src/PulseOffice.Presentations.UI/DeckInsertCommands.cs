using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Links;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Tables.Insert;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckInsertCommands(
    IActiveDeckContext context, IDialogService dialogs, ITextCatalog texts,
    IDeckFileInsertion insertion,
    PulseOffice.UI.Controls.Prompts.IQrCodePrompt qrCodes,
    PulseOffice.UI.Controls.Charts.IEmbeddedChartPrompt charts,
    PulseOffice.UI.Controls.Objects.IPackagedFilePrompt packages,
    IFreeAreaPlacement placement,
    ITableInsertPrompt tables,
    IDeckTableLooks tableLooks,
    IHyperlinkPrompt hyperlinks,
    PulseOffice.Foundation.Identity.IUserIdentity identity,
    IDimensionLineTemplate dimensions,
    PulseOffice.Text.Rich.ITextCultures cultures) : DeckCommandSet(context)
{
    private static readonly (string Id, ShapeKind Kind)[] Shapes =
    [
        ("rectangle", ShapeKind.Rectangle),
        ("roundedRectangle", ShapeKind.RoundedRectangle),
        ("ellipse", ShapeKind.Ellipse),
        ("triangle", ShapeKind.Triangle),
        ("diamond", ShapeKind.Diamond),
        ("hexagon", ShapeKind.Hexagon),
        ("star5", ShapeKind.Star5),
        ("arrowRight", ShapeKind.ArrowRight),
        ("chevron", ShapeKind.Chevron),
        ("callout", ShapeKind.Callout),
        ("line", ShapeKind.Line),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. Shapes.Select(entry => Insert($"deck.insert.shape.{entry.Id}",
            _ => new ShapeObject { Id = NewId(), Shape = entry.Kind }, 8, 5)),
        Insert("deck.insert.shape.dimensionLine", _ => dimensions.Create(NewId()),
            dimensions.Size.Width.ToCentimeters(), dimensions.Size.Height.ToCentimeters()),

        Prototype("deck.insert.textBox") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide) InsertTextBox(t, slide);
                return Task.CompletedTask;
            },
        },

        Prototype("deck.insert.shape.gallery") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => GalleryAsync(ct),
        },
        Gallery("deck.insert.symbolShapes", ShapeFamilies.Symbols),
        Gallery("deck.insert.flowChartShapes", ShapeFamilies.FlowChart),

        Prototype("deck.insert.image") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => insertion.InsertImageAsync(ct),
        },

        Prototype("deck.insert.formula") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, _) => Context.Target?.InsertFormula() ?? Task.CompletedTask,
        },

        Prototype("deck.insert.chart") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => ChartAsync(ct),
        },

        Prototype("deck.insert.table") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            RibbonControl = new TableGridSpec(TableLimits.MaxRows, TableLimits.MaxColumns),
            Execute = (command, ct) => TableAsync(command.Argument as RibbonTableSize, ct),
        },

        Prototype("deck.insert.qr") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => QrAsync(ct),
        },

        Prototype("deck.insert.object") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => InsertObjectAsync(ct),
        },

        Prototype("deck.insert.hyperlink") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is not null,
            Execute = (_, ct) => HyperlinkAsync(ct),
        },

        Prototype("deck.insert.comment") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => CommentAsync(ct),
        },

        Prototype("deck.object.delete") with
        {
            Surface = CommandSurface.Common,
            DefaultGesture = "Delete",
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t
                && t.Selected() is ObjectElement or MediaElement,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide
                    && t.Selected() is { } element and (ObjectElement or MediaElement))
                    t.Actions.DeleteElements(slide,
                        [.. t.SelectedMany().Where(selected => selected is ObjectElement or MediaElement).DefaultIfEmpty(element).Distinct()],
                        "deck.action.deleteObject");
                return Task.CompletedTask;
            },
        },
    ];

    private string NewId() => Guid.NewGuid().ToString("N");

    private async Task InsertObjectAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is null) return;
        if (await packages.PickAsync(PackagedObjectKind.File, NewId(), ct).ConfigureAwait(true) is not { } packaged) return;
        if (Context.Target is { } target && target.CurrentSlide() is { } slide) InsertContent(target, slide, packaged, 6, 4);
    }

    private async Task CommentAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;

        var form = new FormPromptView(
            texts["deck.comment.explanation"],
            [new TextField("text", texts["deck.comment.text"], string.Empty)],
            texts);

        var values = await dialogs.ShowAsync<Dictionary<string, object>>(
            form, new DialogOptions { TitleKey = "command.deck.insert.comment.title", PreferredWidth = 460 }, ct)
            .ConfigureAwait(true);
        if (values is null) return;

        if ((values["text"] as string ?? string.Empty).Trim() is not { Length: > 0 } text) return;

        t.Actions.AddComment(slide, new PulseOffice.Presentations.SlideComment
        {
            Id = NewId(),
            Author = identity.Name,
            Text = text,
            X = PulseOffice.Foundation.Units.Twip.FromCentimeters(1),
            Y = PulseOffice.Foundation.Units.Twip.FromCentimeters(1),
        });
    }

    private readonly IReadOnlyList<(string Address, InteractionKind Kind, string Key)> _jumps =
    [
        ("deck:first", InteractionKind.FirstSlide, "first"),
        ("deck:previous", InteractionKind.PreviousSlide, "previous"),
        ("deck:next", InteractionKind.NextSlide, "next"),
        ("deck:last", InteractionKind.LastSlide, "last"),
    ];

    private async Task HyperlinkAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide || t.Selected() is not { } element) return;

        var current = slide.Interaction(element.Id);
        var request = new LinkRequest("command.deck.insert.hyperlink.title")
        {
            Address = Address(current),
            ShowsText = false,
            Places = Places(t.Deck),
            CanRemove = current is not null && current.Kind != InteractionKind.None,
        };
        if (await hyperlinks.AskAsync(request, ct).ConfigureAwait(true) is not { } choice) return;
        t.Actions.SetInteraction(slide, element.Id, choice.Remove ? null : Interaction(element.Id, choice));
    }

    private string Address(InteractiveAction? action) => action switch
    {
        null => string.Empty,
        { Kind: InteractionKind.Hyperlink } => action.Reference ?? string.Empty,
        { Kind: InteractionKind.GoToSlide } => action.Reference ?? string.Empty,
        _ => _jumps.FirstOrDefault(jump => jump.Kind == action.Kind).Address ?? string.Empty,
    };

    private InteractiveAction Interaction(string elementId, LinkChoice choice)
    {
        if (choice.Kind != LinkKind.Place)
            return new InteractiveAction { TargetId = elementId, Kind = InteractionKind.Hyperlink, Reference = choice.Address };
        var jump = _jumps.FirstOrDefault(entry => entry.Address == choice.Address);
        return jump.Address is null
            ? new InteractiveAction { TargetId = elementId, Kind = InteractionKind.GoToSlide, Reference = choice.Address }
            : new InteractiveAction { TargetId = elementId, Kind = jump.Kind };
    }

    private IReadOnlyList<LinkPlace> Places(Presentation deck)
    {
        var slides = deck.Slides.Select((slide, index) => new LinkPlace(SlideTitle(slide, index), slide.Id, "link.place.slide")).ToList();
        return
        [
            .. _jumps.Select(jump => new LinkPlace(texts["link.place." + jump.Key], jump.Address, "link.place." + jump.Key)),
            new LinkPlace(texts["link.place.slides"], string.Empty, "link.place.slide") { Selectable = false, Children = slides },
        ];
    }

    private string SlideTitle(Slide slide, int index)
    {
        var number = (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
        return slide.Placeholder(PlaceholderRole.Title)?.Text.PlainText.Trim() is { Length: > 0 } title
            ? texts.Format("link.place.slide", number, title)
            : texts.Format("link.place.slide.untitled", number);
    }

    private CommandDefinition Gallery(string id, IReadOnlyList<ShapeKind> kinds) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => GalleryAsync(ct, kinds),
        };

    private async Task QrAsync(CancellationToken ct)
    {
        if (await qrCodes.CreateAsync("command.deck.insert.qr.title", ct).ConfigureAwait(true) is not { } code) return;
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;

        InsertContent(t, slide, code with { Id = NewId() }, 6, 6);
    }

    private async Task GalleryAsync(CancellationToken ct, IReadOnlyList<ShapeKind>? kinds = null)
    {
        var chosen = await dialogs.ShowAsync<object>(
            new ShapeGalleryView(texts, kinds),
            new DialogOptions { TitleKey = "dialog.shapeGallery.title", PreferredWidth = 560 },
            ct).ConfigureAwait(true);

        if (chosen is not ShapeKind kind) return;
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;

        InsertContent(t, slide, new ShapeObject { Id = NewId(), Shape = kind }, 8, 5);
    }

    private async Task ChartAsync(CancellationToken ct)
    {
        if (Context.Target is not { } deck) return;
        if (await charts.CreateAsync(NewId(), cultures.For(deck.Deck.TextLanguage), ct).ConfigureAwait(true) is not { } chart) return;
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;

        InsertContent(t, slide, chart, 14, 9);
    }

    private TableInsertLimits TableLimits { get; } = new(100, 30);

    private async Task TableAsync(RibbonTableSize? quick, CancellationToken ct)
    {
        if (Context.Target?.CurrentSlide() is null) return;
        var choice = quick is { } size
            ? tables.Quick(size.Rows, size.Columns, TableLimits)
            : await tables.AskAsync(new TableInsertRequest("command.deck.insert.table.title") { Limits = TableLimits }, ct).ConfigureAwait(true);
        if (choice is null || Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        var body = Math.Max(choice.HeaderRow ? choice.Rows - 1 : choice.Rows, 0);
        var table = new DataTableObject
        {
            Id = NewId(),
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot
            {
                Columns = [.. Enumerable.Range(1, choice.Columns).Select(index => new DataColumn($"{texts["deck.table.column"]} {index}", DataValueKind.Text))],
                Rows = [.. Enumerable.Range(0, body).Select(_ => (IReadOnlyList<object?>)[.. Enumerable.Repeat<object?>(string.Empty, choice.Columns)])],
            },
        };
        InsertContent(t, slide, tableLooks.Apply(table, choice.Look, choice.HeaderRow, choice.BandedRows), 16, 0);
    }

    private CommandDefinition Insert(string id, Func<DeckTarget, EmbeddedObject> build, double widthCm, double heightCm) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide)
                    InsertContent(t, slide, build(t), widthCm, heightCm);
                return Task.CompletedTask;
            },
        };

    private void InsertTextBox(DeckTarget t, Slide slide)
    {
        var width = Twip.FromCentimeters(10);
        var height = Twip.FromCentimeters(1.6);

        var element = new TextElement
        {
            Id = NewId(),
            Bounds = FreeArea(t, slide, width, height),
            ZOrder = slide.Elements.Count,
            Text = new RichText(PlaceholderRole.Free, [new TextParagraph(string.Empty) { FontSize = 18 }])
            {
                Frame = TextFrame.Default with { Autofit = TextAutofit.ResizeShape },
            },
        };

        t.Actions.InsertElement(slide, element);
        t.Select([element]);
        t.EditText(element.Id);
    }

    private DocumentRect FreeArea(DeckTarget t, Slide slide, Twip width, Twip height) =>
        placement.Place(
            new DocumentSize(t.Deck.Size.Width, t.Deck.Size.Height),
            new DocumentSize(width, height),
            slide.Elements.Select(element => element.Bounds));

    private void InsertContent(DeckTarget t, Slide slide, EmbeddedObject content, double widthCm, double heightCm)
    {
        if (heightCm <= 0 && content is DataTableObject table)
            heightCm = TableMetrics.TotalHeight(table) / 72.0 * 2.54;
        var w = Twip.FromCentimeters(widthCm);
        var h = Twip.FromCentimeters(heightCm);

        var element = new ObjectElement
        {
            Id = content.Id,
            Bounds = FreeArea(t, slide, w, h),
            ZOrder = slide.Elements.Count,
            Content = content,
        };

        t.Actions.InsertObject(slide, element);
        t.Select([element]);
    }
}
