using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using PulseOffice.Sheets.Styling;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.Drawing.Assets;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerShapeCommands(
    ISheetImageInsertion images,
    ISheetFormattingService formatting,
    IActiveSheetContext context,
    IDialogService dialogs,
    IStatusBarService status,
    IFileDialogService files,
    IAssetLibrary assets,
    IObjectOrdering ordering,
    IObjectAlignment alignment,
    ISheetConnectors connectors,
    IQueryRefreshRunner queryRefresh,
    ISheetGrouping grouping,
    IShapeStyling styling,
    PulseOffice.UI.Controls.Shapes.IShapeStyleEditors shapeEditors,
    ITextCatalog texts,
    PulseOffice.UI.Controls.Objects.IChosenFileReader reader,
    IDimensionLineTemplate dimensions) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() => ShapeCommands();

    private static readonly ShapeKind[] QuickShapes =
    [
        ShapeKind.Rectangle, ShapeKind.RoundedRectangle, ShapeKind.Ellipse,
        ShapeKind.Triangle, ShapeKind.ArrowRight, ShapeKind.Star5, ShapeKind.Line,
    ];

    private IEnumerable<CommandDefinition> ShapeCommands() =>
    [
        .. QuickShapes.Select(kind => Shape(
            $"ledger.shape.{ShapeGalleryView.Key(kind)}", kind, CommandSurface.Common)),

        Shape("ledger.shape.dimensionLine", ShapeKind.Line, CommandSurface.Common) with
        {
            Execute = (_, _) =>
            {
                InsertObject(dimensions.Create(Guid.NewGuid().ToString("N")), dimensions.Size);
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.shape.gallery") with
        {
            IconKey = "ledger.shape.gallery",
            MenuPath = "insert/shape",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => formatting.CanFormat,
            Execute = (_, ct) => InsertFromGalleryAsync(ct),
        },

        Prototype("ledger.image.gallery") with
        {
            IconKey = "ledger.image.gallery",
            MenuPath = "insert/image",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => formatting.CanFormat,
            Execute = (_, ct) => InsertFromLibraryAsync(ct),
        },

        Prototype("ledger.image.svg") with
        {
            IconKey = "ledger.image.svg",
            MenuPath = "insert/image",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => formatting.CanFormat,
            Execute = (_, ct) => InsertVectorAsync(ct),
        },

        Prototype("ledger.image.raster") with
        {
            IconKey = "ledger.image.raster",
            MenuPath = "insert/image",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => formatting.CanFormat,
            Execute = (_, ct) => InsertRasterAsync(ct),
        },

        Prototype("ledger.shape.edit") with
        {
            MenuPath = "insert/shape",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => ObjectsEditable && Context.SelectedObject?.Content
                is ShapeObject or Drawing.Freeform.FreeformObject,
            Execute = (_, ct) => EditShapeAsync(ct),
        },

        Prototype("ledger.object.group") with
        {
            IconKey = "ledger.object.group",
            DefaultGesture = "Ctrl+Shift+G",
            MenuPath = "insert/object",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ManyObjectsSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => ObjectsEditable,
            Execute = (_, _) =>
            {
                GroupSelection();
                return Task.CompletedTask;
            },
        },

        Prototype("ledger.object.ungroup") with
        {
            IconKey = "ledger.object.ungroup",
            DefaultGesture = "Ctrl+Alt+Shift+G",
            MenuPath = "insert/object",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Common | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectGroupSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => ObjectsEditable,
            Execute = (_, _) =>
            {
                UngroupSelection();
                return Task.CompletedTask;
            },
        },

        Connector("ledger.connector.elbow", ConnectorKind.Elbow),
        Connector("ledger.connector.straight", ConnectorKind.Straight),
        Connector("ledger.connector.curved", ConnectorKind.Curved),

        Align("ledger.object.alignLeft", ObjectAlignmentEdge.Left),
        Align("ledger.object.alignCentre", ObjectAlignmentEdge.HorizontalCentre),
        Align("ledger.object.alignRight", ObjectAlignmentEdge.Right),
        Align("ledger.object.alignTop", ObjectAlignmentEdge.Top),
        Align("ledger.object.alignMiddle", ObjectAlignmentEdge.VerticalMiddle),
        Align("ledger.object.alignBottom", ObjectAlignmentEdge.Bottom),
        Distribute("ledger.object.spreadHorizontally", ObjectDistribution.Horizontal),
        Distribute("ledger.object.spreadVertically", ObjectDistribution.Vertical),

        Order("ledger.object.toFront", ObjectOrderMove.ToFront),
        Order("ledger.object.forward", ObjectOrderMove.Forward),
        Order("ledger.object.backward", ObjectOrderMove.Backward),
        Order("ledger.object.toBack", ObjectOrderMove.ToBack),

        Prototype("ledger.data.refresh") with
        {
            IconKey = "ledger.data.refresh",
            MenuPath = "data/external",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => queryRefresh.CanRefresh(Context.Workbook),
            RunsDocumentQueries = true,
            Execute = (_, ct) => queryRefresh.RefreshAsync(ct),
        },

        Prototype("ledger.object.delete") with
        {
            MenuPath = "insert/object",
            ContextPaths = ["cell/object"],
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Workbook is { } protectedBook
                && Context.SheetIndex >= 0 && Context.SheetIndex < protectedBook.Sheets.Count
                && !protectedBook.Sheets[Context.SheetIndex].Protection.IsEnabled,
            Execute = (_, _) =>
            {
                if (Context.Workbook is not { } workbook) return Task.CompletedTask;

                var sheet = workbook.Sheets[Context.SheetIndex];
                if (sheet.Protection.IsEnabled) return Task.CompletedTask;
                var removed = Context.SelectedObjects
                    .Where(sheet.Objects.Contains)
                    .Select(placed => (Placed: placed, Index: sheet.Objects.IndexOf(placed)))
                    .OrderBy(entry => entry.Index)
                    .ToList();
                if (removed.Count == 0) return Task.CompletedTask;

                foreach (var (placed, _) in removed) sheet.Objects.Remove(placed);

                Context.History?.Push(new Document.Commands.DelegateAction(
                    texts["history.object.delete"],
                    redo: () =>
                    {
                        foreach (var (placed, _) in removed) sheet.Objects.Remove(placed);
                    },
                    undo: () =>
                    {
                        foreach (var (placed, index) in removed) sheet.Objects.Insert(Math.Min(index, sheet.Objects.Count), placed);
                    }));

                Context.SelectObject(null);
                Context.RequestRefresh();
                return Task.CompletedTask;
            },
        },
    ];

    private CommandDefinition Connector(string id, ConnectorKind kind) =>
    Prototype(id) with
    {
        IconKey = id,
        MenuPath = "insert/connector",
        ContextPaths = ["cell/arrange"],
        Surface = CommandSurface.Common | CommandSurface.ContextMenu,
        RequiredState = DocumentState.DocumentOpen | DocumentState.ManyObjectsSelected,
        ForbiddenState = DocumentState.ReadOnly,
        Execute = (_, _) =>
        {
            if (Context.Workbook is not { } workbook) return Task.CompletedTask;

            var selected = Context.SelectedObjects;
            if (selected.Count < 2) return Task.CompletedTask;

            var sheet = workbook.Sheets[Context.SheetIndex];

            var placed = connectors.Connect(
                sheet, selected[0], selected[1], Guid.NewGuid().ToString("N"), kind);

            if (placed is null) return Task.CompletedTask;

            sheet.Objects.Add(placed);

            Context.History?.Push(new Document.Commands.DelegateAction(
                texts["history.connector.insert"],
                redo: () => sheet.Objects.Add(placed),
                undo: () => sheet.Objects.Remove(placed)));

            Context.SelectObject(placed);
            Context.RequestRefresh();
            return Task.CompletedTask;
        },
    };

    private CommandDefinition Align(string id, ObjectAlignmentEdge edge) =>
        ObjectLayout(id, sheet => alignment.Align(sheet, Context.SelectedObjects, edge));

    private CommandDefinition Distribute(string id, ObjectDistribution direction) =>
        ObjectLayout(id, sheet => alignment.Distribute(sheet, Context.SelectedObjects, direction));

    private CommandDefinition ObjectLayout(
        string id, Func<Worksheet, IReadOnlyList<PlacedObject>> arrange) =>
    Prototype(id) with
    {
        IconKey = id,
        MenuPath = "insert/align",
        ContextPaths = ["cell/arrange"],
        Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
        RequiredState = DocumentState.DocumentOpen | DocumentState.ManyObjectsSelected,
        ForbiddenState = DocumentState.ReadOnly,
        Execute = (_, _) =>
        {
            if (Context.Workbook is not { } workbook) return Task.CompletedTask;

            var sheet = workbook.Sheets[Context.SheetIndex];
            var moved = arrange(sheet);

            if (moved.Count == 0) return Task.CompletedTask;

            var byId = moved.ToDictionary(placed => placed.Id, placed => placed);

            var updated = ordering.Drawn(sheet)
                .Select(placed => byId.GetValueOrDefault(placed.Id, placed))
                .ToList();

            Replace(sheet, updated, texts["history.object.align"], moved);
            return Task.CompletedTask;
        },
    };

    private CommandDefinition Order(string id, ObjectOrderMove move) =>
    Prototype(id) with
    {
        IconKey = id,
        MenuPath = "insert/order",
        ContextPaths = ["cell/order"],
        Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
        RequiredState = DocumentState.DocumentOpen | DocumentState.ObjectSelected,
        ForbiddenState = DocumentState.ReadOnly,
        Execute = (_, _) =>
        {
            Reorder(move);
            return Task.CompletedTask;
        },
    };

    private void Reorder(ObjectOrderMove move)
    {
        if (Context.Workbook is not { } workbook) return;

        var selected = Context.SelectedObjects;
        if (selected.Count == 0) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var reordered = ordering.Reordered(sheet, selected, move);

        Replace(sheet, reordered, texts["history.object.order"], selected);
    }

    private void Replace(
        Worksheet sheet, IReadOnlyList<PlacedObject> updated, string description,
        IReadOnlyList<PlacedObject> previouslySelected)
    {
        var previous = sheet.Objects.ToList();

        void Apply(IReadOnlyList<PlacedObject> objects)
        {
            sheet.Objects.Clear();
            sheet.Objects.AddRange(objects);
        }

        var next = updated.ToList();

        Apply(next);

        Context.History?.Push(new Document.Commands.DelegateAction(
            description,
            redo: () => Apply(next),
            undo: () => Apply(previous)));

        var byId = next.ToDictionary(placed => placed.Id, placed => placed);

        Context.SelectObjects(previouslySelected
            .Select(placed => byId.GetValueOrDefault(placed.Id))
            .Where(placed => placed is not null)
            .Select(placed => placed!));

        Context.RequestRefresh();
    }

    private void GroupSelection()
    {
        if (Context.Workbook is not { } workbook || !ObjectsEditable) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var selected = Context.SelectedObjects.ToList();

        if (grouping.Group(sheet, selected, Guid.NewGuid().ToString("N")) is not { } group) return;

        var remaining = ordering.Drawn(sheet).Where(placed => !selected.Contains(placed)).ToList();
        remaining.Add(group);

        var updated = remaining.Select((placed, index) => placed with { ZOrder = index }).ToList();

        Replace(sheet, updated, texts["history.object.group"], [group]);
        Context.SelectObject(updated[^1]);
    }

    private void UngroupSelection()
    {
        if (Context.Workbook is not { } workbook || !ObjectsEditable) return;
        if (Context.SelectedObject is not { } placed) return;

        var sheet = workbook.Sheets[Context.SheetIndex];

        if (grouping.Ungroup(sheet, placed) is not { } released) return;

        var updated = new List<PlacedObject>();

        foreach (var existing in ordering.Drawn(sheet))
        {
            if (ReferenceEquals(existing, placed)) updated.AddRange(released);
            else updated.Add(existing);
        }

        var ordered = updated.Select((item, index) => item with { ZOrder = index }).ToList();

        Replace(sheet, ordered, texts["history.object.ungroup"], released);
    }

    private CommandDefinition Shape(string id, ShapeKind kind, CommandSurface surface) =>
    new()
    {
        Id = new CommandId(id),
        TitleOverride = null,
        Module = ModuleKind.Sheets,
        MenuPath = "insert/shape",
        RibbonPath = surface.HasFlag(CommandSurface.Ribbon) ? "insert/shapes" : null,
        Surface = surface,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => formatting.CanFormat,
        Execute = (_, _) =>
        {
            InsertShape(kind);
            return Task.CompletedTask;
        },
    };

    private void InsertShape(ShapeKind kind) =>
        InsertObject(
            new ShapeObject { Id = Guid.NewGuid().ToString("N"), Shape = kind, Style = ShapeStyle.Default },
            new Foundation.Units.DocumentSize(Foundation.Units.Twip.FromCentimeters(4), Foundation.Units.Twip.FromCentimeters(3)));

    private void InsertObject(ShapeObject shape, Foundation.Units.DocumentSize size) =>
        images.Insert(new PlacedObject
        {
            Id = shape.Id,
            Content = shape,
            Anchor = Context.Selection.Start,
            Size = size,
        }, texts["history.shape.insert"]);

    private async Task InsertFromGalleryAsync(CancellationToken ct)
    {
        var chosen = await dialogs.ShowAsync<ShapeKind>(
            new ShapeGalleryView(texts),
            new DialogOptions { TitleKey = "dialog.shapeGallery.title", PreferredWidth = 560 }, ct)
            .ConfigureAwait(true);

        if (chosen is { } kind) InsertShape(kind);
    }

    private async Task InsertFromLibraryAsync(CancellationToken ct)
    {
        var chosen = await dialogs.ShowAsync<Drawing.Assets.VectorAsset>(
            new AssetPickerView(assets, texts),
            new DialogOptions { TitleKey = "dialog.assetPicker.title", PreferredWidth = 620 }, ct)
            .ConfigureAwait(true);

        if (chosen is null) return;

        InsertVector(new Drawing.Assets.VectorGraphicObject
        {
            Id = chosen.Id,
            Asset = chosen,
            RecolorHex = chosen.IsRecolorable ? "#334155" : null,
        });
    }

    private void InsertVector(Drawing.Assets.VectorGraphicObject vector) =>
        InsertObject(vector, new Foundation.Units.DocumentSize(
            Foundation.Units.Twip.FromCentimeters(3),
            Foundation.Units.Twip.FromCentimeters(3)));

    private void InsertObject(
        Document.Embedding.EmbeddedObject content, Foundation.Units.DocumentSize size) =>
        images.InsertObject(content, size, texts["history.image.insert"]);

    private async Task InsertRasterAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var chosen = await files.OpenFileAsync(
            texts["dialog.insertImage.title"],
            [new FileFilter(texts["file.image"], [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff"])],
            ct).ConfigureAwait(true);

        if (chosen is null) return;

        var content = await reader.ReadAsync(chosen, reader.ImageLimit, ct).ConfigureAwait(true);
        if (Unread(content, reader.ImageLimit)) return;

        if (!images.InsertRaster(content.Bytes, Path.GetFileNameWithoutExtension(chosen.Name), chosen.Name))
            status.Report(texts["status.image.unreadable"], NotificationSeverity.Error);
    }

    private async Task InsertVectorAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;

        var chosen = await files.OpenFileAsync(
            texts["dialog.insertImage.title"],
            [new FileFilter(texts["file.svg"], [".svg"])], ct).ConfigureAwait(true);

        if (chosen is null) return;

        var content = await reader.ReadAsync(chosen, reader.VectorLimit, ct).ConfigureAwait(true);
        if (Unread(content, reader.VectorLimit)) return;

        string svg;
        using (var text = new StreamReader(new MemoryStream(content.Bytes)))
            svg = await text.ReadToEndAsync(ct).ConfigureAwait(true);

        if (!svg.Contains("<svg", StringComparison.Ordinal))
        {
            status.Report(texts["status.image.notSvg"], NotificationSeverity.Error);
            return;
        }

        var name = Path.GetFileNameWithoutExtension(chosen.Name);
        var id = Guid.NewGuid().ToString("N");

        InsertVector(new Drawing.Assets.VectorGraphicObject
        {
            Id = id,
            Asset = new Drawing.Assets.VectorAsset
            {
                Id = id,
                Name = name,
                SourceKind = Drawing.Assets.VectorSourceKind.InlineSvg,
                SvgContent = svg,
            },
        });
    }

    private bool Unread(PulseOffice.UI.Controls.Objects.ChosenFileContent content, long limit)
    {
        if (content.Succeeded) return false;

        status.Report(content.Status == PulseOffice.UI.Controls.Objects.ChosenFileReadStatus.TooLarge
            ? texts.Format("status.image.tooLarge", limit / (1024 * 1024))
            : texts["status.image.unreadable"], NotificationSeverity.Error);
        return true;
    }

    private async Task EditShapeAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook || !ObjectsEditable) return;
        if (Context.SelectedObject is not { } placed) return;

        var editable = styling.Editable(placed.Content);
        if (editable is null) return;

        var updated = await dialogs.ShowAsync<StyledOutline>(
            shapeEditors.Create(editable),
            new DialogOptions { TitleKey = "dialog.shapeEdit.title", PreferredWidth = 720 }, ct)
            .ConfigureAwait(true);

        if (updated is null) return;

        var sheet = workbook.Sheets[Context.SheetIndex];
        var index = sheet.Objects.IndexOf(placed);
        if (index < 0) return;

        var content = styling.Apply(placed.Content, updated);
        if (content is null) return;

        var replacement = placed with { Content = content };

        sheet.Objects[index] = replacement;

        Context.History?.Push(new Document.Commands.DelegateAction(
            texts["history.shape.edit"],
            redo: () => sheet.Objects[index] = replacement,
            undo: () => sheet.Objects[index] = placed));

        Context.SelectObject(replacement);
        Context.RequestRefresh();
    }
}
