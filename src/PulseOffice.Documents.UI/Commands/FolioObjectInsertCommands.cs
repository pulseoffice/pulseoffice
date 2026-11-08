using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioObjectInsertCommands(
    IActiveFolioContext context,
    IFolioObjectEditing objects,
    IDialogService dialogs,
    ITextCatalog texts,
    IFontworkPrompt fontwork,
    PulseOffice.UI.Controls.Prompts.IQrCodePrompt qrCodes,
    PulseOffice.UI.Controls.Charts.IEmbeddedChartPrompt charts,
    PulseOffice.Formulas.Editor.IFormulaPrompt formulas,
    PulseOffice.UI.Controls.Objects.IPackagedFilePrompt packages,
    IFormControlCatalog controls,
    IFormControlNaming naming,
    IAssetLibrary assets,
    PulseOffice.UI.Controls.Forms.IFormDesignMode design,
    IDimensionLineTemplate dimensions) : FolioCommandSet(context)
{
    private readonly IReadOnlyList<(string Key, IReadOnlyList<ShapeKind> Kinds)> _families =
    [
        ("basicShapes", ShapeFamilies.Basic),
        ("arrowShapes", ShapeFamilies.Arrows),
        ("symbolShapes", ShapeFamilies.Symbols),
        ("starShapes", ShapeFamilies.Stars),
        ("calloutShapes", ShapeFamilies.Callouts),
        ("flowChartShapes", ShapeFamilies.FlowChart),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Action(Insertion("folio.insert.textFrame"), editor => objects.InsertTextFrame(editor, Size(8, 3))),
        Asynchronous("folio.insert.shape", ct => ShapeAsync(null, ct)),
        .. _families.Select(family => Asynchronous($"folio.insert.{family.Key}", ct => ShapeAsync(family.Kinds, ct)) with { Surface = CommandSurface.Advanced }),
        Action(Insertion("folio.insert.line") with { Surface = CommandSurface.Advanced }, editor =>
            objects.Insert(editor, new ObjectInsertion(new ShapeObject { Id = Id(editor), Shape = ShapeKind.Line }, Size(6, 0.5), ObjectAnchorKind.Paragraph, WrapMode.Through))),
        Action(Insertion("folio.insert.dimensionLine") with { Surface = CommandSurface.Advanced }, editor =>
            objects.Insert(editor, new ObjectInsertion(dimensions.Create(Id(editor)), dimensions.Size, ObjectAnchorKind.Paragraph, WrapMode.Through))),
        Asynchronous("folio.insert.chart", ChartAsync),
        Asynchronous("folio.insert.formula", FormulaAsync),
        Asynchronous("folio.insert.fontwork", FontworkAsync),
        Asynchronous("folio.insert.qrCode", QrAsync) with { Surface = CommandSurface.Advanced },
        Asynchronous("folio.insert.media", ct => PackageAsync(PackagedObjectKind.Media, ct)),
        Asynchronous("folio.insert.object", ct => PackageAsync(PackagedObjectKind.File, ct)) with { Surface = CommandSurface.Advanced },
        Asynchronous("folio.insert.gallery", GalleryAsync) with { Surface = CommandSurface.Advanced },
        .. controls.All.Select(descriptor => Action(Insertion($"folio.form.insert.{Slug(descriptor.Kind)}") with
        {
            Surface = CommandSurface.Advanced,
            IconKey = $"ledger.form.insert.{Slug(descriptor.Kind)}",
        }, editor => Control(editor, descriptor))),
    ];

    private CommandDefinition Insertion(string id) => Editing(id) with { Surface = CommandSurface.Common };

    private CommandDefinition Asynchronous(string id, Func<CancellationToken, Task> run) => Insertion(id) with { Execute = (_, ct) => run(ct) };

    private DocumentSize Size(double widthCm, double heightCm) => new(Twip.FromCentimeters(widthCm), Twip.FromCentimeters(heightCm));

    private string Id(FolioEditor editor) => editor.Document.NextId("emb");

    private string Slug(FormControlKind kind) => char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    private async Task ShapeAsync(IReadOnlyList<ShapeKind>? kinds, CancellationToken ct)
    {
        var chosen = await dialogs.ShowAsync<object>(
            new ShapeGalleryView(texts, kinds),
            new DialogOptions { TitleKey = "dialog.shapeGallery.title", PreferredWidth = 560 },
            ct).ConfigureAwait(true);
        if (chosen is not ShapeKind kind || Editor is not { } editor) return;
        objects.Insert(editor, new ObjectInsertion(new ShapeObject { Id = Id(editor), Shape = kind }, Size(4, 3), ObjectAnchorKind.Paragraph, WrapMode.Parallel));
    }

    private async Task ChartAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || await charts.CreateAsync(Id(editor), editor.Services.Cultures.For(editor.Document.Language), ct).ConfigureAwait(true) is not { } chart) return;
        objects.Insert(editor, new ObjectInsertion(chart, Size(14, 9), ObjectAnchorKind.Paragraph, WrapMode.None));
    }

    private async Task FormulaAsync(CancellationToken ct)
    {
        if (await formulas.AskAsync(string.Empty, "command.folio.insert.formula.title", ct).ConfigureAwait(true) is not { } expression || Editor is not { } editor) return;
        objects.Insert(editor, new ObjectInsertion(
            new FormulaObject { Id = Id(editor), Expression = expression, NaturalSize = FormulaObject.StandardSize },
            FormulaObject.StandardSize, ObjectAnchorKind.AsCharacter, WrapMode.None));
    }

    private async Task FontworkAsync(CancellationToken ct)
    {
        if (await fontwork.CreateAsync("command.folio.insert.fontwork.title", ct).ConfigureAwait(true) is not { } art || Editor is not { } editor) return;
        objects.Insert(editor, new ObjectInsertion(art with { Id = Id(editor) }, Size(12, 4), ObjectAnchorKind.Paragraph, WrapMode.TopAndBottom));
    }

    private async Task QrAsync(CancellationToken ct)
    {
        if (await qrCodes.CreateAsync("command.folio.insert.qrCode.title", ct).ConfigureAwait(true) is not { } code || Editor is not { } editor) return;
        objects.Insert(editor, new ObjectInsertion(code with { Id = Id(editor) }, Size(4, 4), ObjectAnchorKind.AsCharacter, WrapMode.None));
    }

    private async Task PackageAsync(PackagedObjectKind kind, CancellationToken ct)
    {
        if (Editor is not { } editor || await packages.PickAsync(kind, Id(editor), ct).ConfigureAwait(true) is not { } packaged) return;
        var size = kind == PackagedObjectKind.Media ? Size(8, 4.5) : Size(5, 2);
        objects.Insert(editor, new ObjectInsertion(packaged with { NaturalSize = size }, size, ObjectAnchorKind.AsCharacter, WrapMode.None));
    }

    private async Task GalleryAsync(CancellationToken ct)
    {
        var chosen = await dialogs.ShowAsync<VectorAsset>(
            new AssetPickerView(assets, texts),
            new DialogOptions { TitleKey = "command.folio.insert.gallery.title", PreferredWidth = 620 }, ct).ConfigureAwait(true);
        if (chosen is null || Editor is not { } editor) return;
        objects.Insert(editor, new ObjectInsertion(new VectorGraphicObject { Id = Id(editor), Asset = chosen, RecolorHex = chosen.IsRecolorable ? "#334155" : null },
            Size(3, 3), ObjectAnchorKind.AsCharacter, WrapMode.None));
    }

    private void Control(FolioEditor editor, FormControlDescriptor descriptor)
    {
        var taken = editor.Document.Objects.All.Select(item => item.Content).OfType<EmbeddedContent>().Select(content => content.Value)
            .OfType<FormControlObject>().Select(control => control.Properties.Name);
        var name = naming.NextName(descriptor.Kind, taken);
        var control = new FormControlObject
        {
            Id = Id(editor),
            ControlKind = descriptor.Kind,
            NaturalSize = descriptor.DefaultSize,
            Properties = controls.Create(descriptor.Kind, name),
        };
        objects.Insert(editor, new ObjectInsertion(control, descriptor.DefaultSize, ObjectAnchorKind.AsCharacter, WrapMode.None));
        design.Set(true);
    }
}
