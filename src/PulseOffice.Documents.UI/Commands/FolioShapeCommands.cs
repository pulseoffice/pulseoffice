using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Documents.UI.Commands;

public sealed record FolioShapeServices(
    IFolioObjectEditing Objects,
    IObjectGrouping Grouping,
    IFolioObjectHits Hits,
    IShapeStyling Styling,
    IFontworkPrompt Fontwork,
    ISignatureLinePrompt Signatures,
    PulseOffice.UI.Controls.Shapes.IShapeStyleEditors Editors,
    IDialogService Dialogs,
    ITextCatalog Texts,
    PulseOffice.UI.Controls.Objects.IBitmapPixels Pixels);

public sealed class FolioShapeCommands(IActiveFolioContext context, FolioShapeServices services) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Style("folio.object.area", 0),
        Style("folio.object.line", 1),
        Style("folio.object.textAttributes", 3),
        Style("folio.object.connectorAttributes", 1, connectors: true),
        Editing("folio.object.fontwork") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Content(editor) is FontworkObject or ShapeObject { Text.Length: > 0 },
            Execute = (_, ct) => FontworkAsync(ct),
        },
        Align("folio.object.alignTop", 0),
        Align("folio.object.alignMiddle", 0.5),
        Align("folio.object.alignBottom", 1),
        Action(Editing("folio.object.group") with
        {
            Surface = CommandSurface.Advanced,
            DefaultGesture = "Ctrl+Shift+G",
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && services.Grouping.CanGroup(editor.Document, editor.SelectedObjectIds) && SamePage(editor) is not null,
        }, editor => Group(editor)),
        Action(Editing("folio.object.ungroup") with
        {
            Surface = CommandSurface.Advanced,
            DefaultGesture = "Ctrl+Alt+Shift+G",
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Content(editor) is ShapeGroupObject,
        }, editor => Ungroup(editor)),
        Action(Editing("folio.object.enterGroup") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Content(editor) is ShapeGroupObject,
        }, editor => _entered = Ungroup(editor)),
        Action(Editing("folio.object.leaveGroup") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && _entered.Count > 1 && _entered.All(id => editor.Document.Objects.Find(id) is not null),
        }, editor =>
        {
            editor.SelectObject(_entered[0]);
            foreach (var id in _entered.Skip(1)) editor.ToggleObject(id);
            _entered = [];
            Group(editor);
        }),
        Editing("folio.object.contour") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && services.Objects.Selected(editor) is { Anchor: not AsCharacterAnchor },
            Execute = (_, ct) => ContourAsync(ct),
        },
        Editing("folio.insert.signatureLine") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => SignatureAsync(ct) },
    ];

    private EmbeddedObject? Content(FolioEditor editor) =>
        services.Objects.Selected(editor) is { Content: EmbeddedContent { Value: var value } } ? value : null;

    private CommandDefinition Style(string id, int section, bool connectors = false) =>
        Editing(id) with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Content(editor) is { } content
                && services.Styling.Editable(content) is not null && (!connectors || content is ConnectorObject),
            Execute = (_, ct) => StyleAsync(section, ct),
        };

    private IReadOnlyList<string> _entered = [];

    private async Task ContourAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || services.Objects.Selected(editor) is not { } item) return;
        var points = item.Contour?.Points.Select(point => new Avalonia.Point(point.X, point.Y)).ToList() ?? [];
        var picture = Content(editor) is PulseOffice.Drawing.Assets.RasterImageObject image ? services.Pixels.Decode(image.Data.ToArray()) : null;
        var edited = await services.Dialogs.ShowAsync<List<Avalonia.Point>>(
            new PulseOffice.UI.Controls.Objects.ContourEditorView(points, picture, services.Texts),
            new DialogOptions { TitleKey = "command.folio.object.contour.title", PreferredWidth = 520 }, ct).ConfigureAwait(true);
        if (edited is null || Editor is not { } current) return;
        var contour = new ObjectContour([.. edited.Select(point => new ContourPoint(Math.Round(point.X, 4), Math.Round(point.Y, 4)))]);
        services.Objects.Change(current, target =>
        {
            target.Contour = contour;
            target.WrapContour = true;
        });
    }

    private (int Page, List<PlacedBox> Boxes)? SamePage(FolioEditor editor)
    {
        var boxes = new List<PlacedBox>();
        var page = -1;
        foreach (var id in editor.SelectedObjectIds)
        {
            if (services.Hits.Find(editor.Layout, id, out var index) is not { } box || page >= 0 && index != page) return null;
            page = index;
            boxes.Add(new PlacedBox(id, box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height));
        }
        return page < 0 ? null : (page, boxes);
    }

    private void Group(FolioEditor editor)
    {
        if (SamePage(editor) is not { } placed) return;
        string? id = null;
        editor.Change(() => id = services.Grouping.Group(editor.Document, editor.History, placed.Boxes, placed.Page));
        if (id is not null) editor.SelectObject(id);
    }

    private IReadOnlyList<string> Ungroup(FolioEditor editor)
    {
        if (editor.SelectedObjectId is not { } id || services.Hits.Find(editor.Layout, id, out var page) is not { } box) return [];
        IReadOnlyList<string> ids = [];
        editor.Change(() => ids = services.Grouping.Ungroup(editor.Document, editor.History, id, new PlacedBox(id, box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height), page));
        if (ids.Count > 0)
        {
            editor.SelectObject(ids[0]);
            foreach (var other in ids.Skip(1)) editor.ToggleObject(other);
        }
        return ids;
    }

    private CommandDefinition Align(string id, double fraction) =>
        Action(Editing(id) with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && services.Objects.Selected(editor) is { Anchor: not AsCharacterAnchor },
        }, editor => AlignVertically(editor, fraction));

    private void AlignVertically(FolioEditor editor, double fraction)
    {
        if (services.Objects.Selected(editor) is not { } item || services.Hits.Find(editor.Layout, item.Id, out var index) is null || index >= editor.Layout.Pages.Count) return;
        var page = editor.Layout.Pages[index];
        var paragraph = item.Anchor is ParagraphAnchor { ParagraphId: var anchored } ? anchored : item.Anchor is CharacterAnchor { ParagraphId: var character } ? character : null;
        var lines = paragraph is null ? [] : page.Lines.Where(line => line.ParagraphId == paragraph).ToList();
        var height = item.Placement.Vertical switch
        {
            VerticalRelation.Page => page.Height,
            VerticalRelation.PageArea or VerticalRelation.Margin => page.Content.Height,
            _ => lines.Count > 0 ? lines.Max(line => line.Top + line.Line.Height) - lines.Min(line => line.Top) : page.Content.Height,
        };
        var y = (height - item.Size.Height.ToPoints()) * fraction;
        services.Objects.Change(editor, target => target.Placement = target.Placement with { Y = Twip.FromPoints(y) });
    }

    private async Task StyleAsync(int section, CancellationToken ct)
    {
        if (Editor is not { } editor || Content(editor) is not { } content || services.Styling.Editable(content) is not { } editable) return;
        var updated = await services.Dialogs.ShowAsync<StyledOutline>(
            services.Editors.Create(editable, section),
            new DialogOptions { TitleKey = "dialog.shapeEdit.title", PreferredWidth = 720 }, ct).ConfigureAwait(true);
        if (updated is null || Editor is not { } current) return;
        services.Objects.Replace(current, value => services.Styling.Apply(value, updated) ?? value);
        if (Math.Abs(updated.RotationDegrees - (services.Objects.Selected(current)?.RotationDegrees ?? 0)) > 0.01)
            services.Objects.Rotate(current, updated.RotationDegrees, relative: false);
    }

    private async Task FontworkAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || Content(editor) is not { } content) return;
        var current = content is FontworkObject art ? art.Warp : FontworkWarp.ArchUp;
        if (await services.Fontwork.ChooseWarpAsync("command.folio.object.fontwork.title", current, ct).ConfigureAwait(true) is not { } warp || Editor is not { } target) return;
        services.Objects.Replace(target, value => value switch
        {
            FontworkObject fontwork => fontwork with { Warp = warp },
            ShapeObject shape => new FontworkObject
            {
                Id = shape.Id,
                Name = shape.Name,
                NaturalSize = shape.NaturalSize,
                Text = shape.Text ?? string.Empty,
                Warp = warp,
                Style = shape.Style with { Fill = shape.Style.Fill.IsVisible ? shape.Style.Fill : ShapeFill.Default },
            },
            _ => value,
        });
    }

    private async Task SignatureAsync(CancellationToken ct)
    {
        if (await services.Signatures.AskAsync("command.folio.insert.signatureLine.title", ct).ConfigureAwait(true) is not { } line || Editor is not { } editor) return;
        services.Objects.Insert(editor, new ObjectInsertion(line with { Id = editor.Document.NextId("o") },
            PulseOffice.Document.Forms.SignatureLineObject.StandardSize, ObjectAnchorKind.AsCharacter, WrapMode.None));
    }
}
