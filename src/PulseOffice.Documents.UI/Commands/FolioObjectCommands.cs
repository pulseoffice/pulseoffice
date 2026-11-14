using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Assets;
using PulseOffice.UI.Commands;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioObjectCommands(
    IActiveFolioContext context,
    IFolioObjectEditing objects,
    IFolioObjectMover mover,
    IFolioObjectDialogs dialogs,
    ICaptions captions,
    IFrameChains chains,
    IFolioObjectCycle cycle) : FolioCommandSet(context)
{
    public const SelectionKind ObjectSelection = SelectionKind.Image | SelectionKind.Shape | SelectionKind.Frame | SelectionKind.Chart;

    private readonly IReadOnlyList<(string Key, ObjectAnchorKind Kind)> _anchors =
    [
        ("page", ObjectAnchorKind.Page),
        ("paragraph", ObjectAnchorKind.Paragraph),
        ("character", ObjectAnchorKind.Character),
        ("asCharacter", ObjectAnchorKind.AsCharacter),
    ];

    private readonly IReadOnlyList<(string Key, WrapMode Mode)> _wraps =
    [
        ("none", WrapMode.None),
        ("parallel", WrapMode.Parallel),
        ("optimal", WrapMode.Optimal),
        ("left", WrapMode.Left),
        ("right", WrapMode.Right),
        ("through", WrapMode.Through),
    ];

    private readonly IReadOnlyList<(string Key, ObjectOrder Order)> _orders =
    [
        ("front", ObjectOrder.Front),
        ("forward", ObjectOrder.Forward),
        ("backward", ObjectOrder.Backward),
        ("back", ObjectOrder.Back),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Action(Viewing("folio.object.selectNext") with
        {
            DefaultGesture = "Ctrl+Alt+Shift+O",
            EnabledWhen = _ => Editor is { } editor && (editor.Document.Objects.All.Count > 1 || editor.Document.Objects.All.Count == 1 && editor.SelectedObjectId is null),
        }, editor => cycle.Step(editor, 1)),
        Action(Contextual("folio.object.delete", _ => true) with { DefaultGesture = null }, editor => editor.DeleteSelectedObject()),
        Action(Contextual("folio.object.originalSize", item => item.Content is EmbeddedContent embedded && embedded.Value.NaturalSize != item.Size), editor =>
        {
            if (objects.Selected(editor) is { Content: EmbeddedContent embedded }) editor.ResizeSelectedObject(embedded.Value.NaturalSize);
        }),
        .. _anchors.Select(anchor => Action(Contextual($"folio.object.anchor.{anchor.Key}", item => anchor.Kind != ObjectAnchorKind.AsCharacter || item.Content is EmbeddedContent) with
        {
            CheckedWhen = _ => Editor is { } editor && objects.Selected(editor) is { } item && Kind(item) == anchor.Kind,
        }, editor =>
        {
            if (editor.SelectedObjectId is { } id) mover.Reanchor(editor, id, anchor.Kind);
        })),
        .. _wraps.Select(wrap => Action(Contextual($"folio.object.wrap.{wrap.Key}", Floating) with
        {
            CheckedWhen = _ => Editor is { } editor && objects.Selected(editor) is { } item && (item.Wrap == wrap.Mode || wrap.Mode == WrapMode.None && item.Wrap == WrapMode.TopAndBottom),
        }, editor => objects.Wrap(editor, wrap.Mode))),
        Toggle("folio.object.wrap.background", Floating, item => item.InBackground, (editor, on) => objects.Change(editor, item => item.InBackground = on)),
        Toggle("folio.object.wrap.contour", item => Floating(item) && item.Content is EmbeddedContent && item.Wrap is not (WrapMode.None or WrapMode.TopAndBottom or WrapMode.Through),
            item => item.WrapContour, (editor, on) => objects.Contour(editor, on)),
        Toggle("folio.object.wrap.firstParagraph", item => Floating(item) && item.Anchor is not PageAnchor, item => item.WrapFirstParagraph, (editor, on) => objects.Change(editor, item => item.WrapFirstParagraph = on)),
        Action(Contextual("folio.object.toForeground", item => Floating(item) && item.InBackground), editor => objects.Change(editor, item => item.InBackground = false)),
        Action(Contextual("folio.object.toBackground", item => Floating(item) && !item.InBackground), editor => objects.Change(editor, item => item.InBackground = true)),
        .. _orders.Select(order => Action(Contextual($"folio.object.order.{order.Key}", item => Floating(item) && objects.CanOrder(Editor!, order.Order)), editor => objects.Order(editor, order.Order))),
        Action(Contextual("folio.object.rotateLeft", _ => true), editor => objects.Rotate(editor, -90, true)),
        Action(Contextual("folio.object.rotateRight", _ => true), editor => objects.Rotate(editor, 90, true)),
        Action(Contextual("folio.object.rotate180", _ => true), editor => objects.Rotate(editor, 180, true)),
        Action(Contextual("folio.object.rotateReset", item => Math.Abs(item.RotationDegrees) > 0.01), editor => objects.Rotate(editor, 0, false)),
        Action(Contextual("folio.object.flipHorizontal", item => objects.CanFlip(Editor!)), editor => objects.Flip(editor, true)),
        Action(Contextual("folio.object.flipVertical", item => objects.CanFlip(Editor!)), editor => objects.Flip(editor, false)),
        Dialog("folio.object.properties", _ => true, (editor, ct) => PropertiesAsync(editor, ObjectDialogPage.Position, ct)),
        Dialog("folio.object.positionSize", _ => true, (editor, ct) => PropertiesAsync(editor, ObjectDialogPage.Position, ct)),
        Dialog("folio.object.wrapDialog", Floating, (editor, ct) => PropertiesAsync(editor, ObjectDialogPage.Wrap, ct)),
        Dialog("folio.object.altText", item => item.Content is EmbeddedContent, (editor, ct) => PropertiesAsync(editor, ObjectDialogPage.Options, ct)),
        Dialog("folio.object.name", _ => true, NameAsync),
        Dialog("folio.object.link", item => item.Content is TextBoxContent && Editor is { } editor && objects.LinkTargets(editor).Count > 0, LinkAsync),
        Action(Contextual("folio.object.unlink", item => Editor is { } editor && chains.Chained(editor.Document, item)), editor => objects.Unlink(editor)),
        Editing("folio.object.caption") with
        {
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && (objects.Selected(editor) is not null || editor.TableAt() is not null),
            Execute = (_, ct) => CaptionAsync(ct),
        },
    ];

    private bool Floating(AnchoredObject item) => item.Anchor is not AsCharacterAnchor;

    private ObjectAnchorKind Kind(AnchoredObject item) => item.Anchor switch
    {
        PageAnchor => ObjectAnchorKind.Page,
        CharacterAnchor => ObjectAnchorKind.Character,
        AsCharacterAnchor => ObjectAnchorKind.AsCharacter,
        _ => ObjectAnchorKind.Paragraph,
    };

    private CommandDefinition Contextual(string id, Func<AnchoredObject, bool> when) =>
        Editing(id) with
        {
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredSelection = ObjectSelection,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && objects.Selected(editor) is { } item && when(item),
        };

    private CommandDefinition Toggle(string id, Func<AnchoredObject, bool> when, Func<AnchoredObject, bool> state, Action<FolioEditor, bool> apply) =>
        Action(Contextual(id, when) with
        {
            CheckedWhen = _ => Editor is { } editor && objects.Selected(editor) is { } item && state(item),
        }, editor =>
        {
            if (objects.Selected(editor) is { } item) apply(editor, !state(item));
        });

    private CommandDefinition Dialog(string id, Func<AnchoredObject, bool> when, Func<FolioEditor, CancellationToken, Task> run) =>
        Contextual(id, when) with
        {
            Execute = async (_, ct) =>
            {
                if (Editor is { } editor && !editor.ReadOnly() && objects.Selected(editor) is not null) await run(editor, ct).ConfigureAwait(true);
            },
        };

    private async Task PropertiesAsync(FolioEditor editor, ObjectDialogPage page, CancellationToken ct)
    {
        if (objects.Selected(editor) is not { } item) return;
        var anchor = Kind(item);
        if (await dialogs.PropertiesAsync(item, anchor, page, ct).ConfigureAwait(true) is not { } choice) return;
        using (editor.History.BeginTransaction("object-properties"))
        {
            var moved = choice.Anchor != anchor && mover.Reanchor(editor, item.Id, choice.Anchor);
            objects.Change(editor, target =>
            {
                target.Size = choice.Size;
                if (!moved) target.Placement = choice.Placement;
                target.RotationDegrees = ((choice.Rotation % 360) + 360) % 360;
                target.Wrap = choice.Wrap;
                target.WrapContour = choice.Contour && target.Content is EmbeddedContent;
                target.WrapFirstParagraph = choice.FirstParagraph;
                target.InBackground = choice.Background;
                target.WrapSpacing = choice.Spacing;
                target.Name = choice.Name;
            });
            if (choice.Contour && item.Contour is null) objects.Contour(editor, true);
            if (item.Content is EmbeddedContent) objects.Replace(editor, content => content with { Title = choice.Title, Description = choice.Description });
        }
    }

    private async Task NameAsync(FolioEditor editor, CancellationToken ct)
    {
        if (objects.Selected(editor) is not { } item) return;
        if (await dialogs.NameAsync(item.Name, ct).ConfigureAwait(true) is not { } name) return;
        objects.Change(editor, target => target.Name = name.Length == 0 ? null : name);
    }

    private async Task LinkAsync(FolioEditor editor, CancellationToken ct)
    {
        if (await dialogs.LinkAsync(objects.LinkTargets(editor), ct).ConfigureAwait(true) is { } target) objects.Link(editor, target);
    }

    private async Task CaptionAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var category = objects.Selected(editor) is { } item ? captions.Category(editor.Document, item.Id) ?? "Illustration" : "Table";
        if (await dialogs.CaptionAsync(category, ct).ConfigureAwait(true) is { } request) objects.Caption(editor, request);
    }
}
