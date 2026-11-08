using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerGalleryCommands(
    IActiveSheetContext context,
    IDialogService dialogs,
    ITextCatalog texts,
    PulseOffice.Formulas.Editor.IFormulaPrompt formulas,
    PulseOffice.UI.Controls.Shapes.IFontworkPrompt fontwork,
    PulseOffice.UI.Controls.Prompts.IQrCodePrompt qrCodes,
    IAssetLibrary assets,
    ISidePanelHost panel,
    ISheetImageInsertion insertion) : LedgerCommandSet(context)
{
    private static readonly ShapeKind[] Callouts = [ShapeKind.Callout];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Gallery("ledger.gallery.symbolShapes", ShapeFamilies.Symbols),
        Gallery("ledger.gallery.calloutShapes", Callouts),
        Gallery("ledger.gallery.flowChartShapes", ShapeFamilies.FlowChart),

        Insert("ledger.insert.textBox", _ => TextBox()),
        Insert("ledger.insert.fontwork", ct => FontworkAsync(ct)),
        Insert("ledger.insert.qrCode", ct => QrAsync(ct)),
        Insert("ledger.insert.formula", ct => FormulaAsync(ct)),

        Prototype("ledger.view.gallery") with
        {
            MenuPath = "insert/gallery",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => GalleryPanelAsync(ct),
        },

        Prototype("ledger.view.navigator") with
        {
            MenuPath = "view/panels",
            DefaultGesture = "F5",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                panel.Show("navigator");
                return Task.CompletedTask;
            },
        },
    ];

    private CommandDefinition Gallery(string id, IReadOnlyList<ShapeKind> kinds) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = "insert/shape",
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => Context.HasTarget,
        Execute = async (_, ct) =>
        {
            var names = kinds.Select(kind => texts[$"shape.kind.{Lower(kind.ToString())}"]).ToList();

            var values = await dialogs.ShowAsync<Dictionary<string, object>>(
                new FormPromptView(
                    texts["gallery.explanation"],
                    [new ChoiceField("shape", texts["gallery.shape"], names)],
                    texts),
                new DialogOptions { TitleKey = $"command.{id}.title" }, ct).ConfigureAwait(true);

            if (values is null) return;

            var index = (int)Convert.ToInt64(values["shape"], System.Globalization.CultureInfo.InvariantCulture);

            Place(new ShapeObject { Id = Guid.NewGuid().ToString("N"), Shape = kinds[index] }, 4, 3);
        },
    };

    private CommandDefinition Insert(string id, Func<CancellationToken, Task> run) => Prototype(id) with
    {
        IconKey = id,
        MenuPath = "insert/object",
        Surface = CommandSurface.Advanced,
        RequiredState = DocumentState.DocumentOpen,
        ForbiddenState = DocumentState.ReadOnly,
        EnabledWhen = _ => Context.HasTarget,
        Execute = (_, ct) => run(ct),
    };

    private Task TextBox()
    {
        Place(
            new ShapeObject
            {
                Id = Guid.NewGuid().ToString("N"),
                Shape = ShapeKind.Rectangle,
                Text = texts["textBox.placeholder"],
                Style = ShapeStyle.Default with
                {
                    Fill = ShapeStyle.Default.Fill with { Kind = ShapeFillKind.None },
                    Line = ShapeStyle.Default.Line with { ColorHex = "#94A3B8" },
                },
            },
            5,
            1.5);

        return Task.CompletedTask;
    }

    private async Task FontworkAsync(CancellationToken ct)
    {
        if (await fontwork.CreateAsync("command.ledger.insert.fontwork.title", ct).ConfigureAwait(true) is { } art)
            Place(art, 12, 4);
    }

    private async Task QrAsync(CancellationToken ct)
    {
        if (await qrCodes.CreateAsync("command.ledger.insert.qrCode.title", ct).ConfigureAwait(true) is { } code)
            Place(code, 4, 4);
    }

    private async Task FormulaAsync(CancellationToken ct)
    {
        var existing = Context.SelectedObject?.Content is Document.Embedding.FormulaObject chosen
            ? chosen.Expression
            : string.Empty;

        if (await formulas.AskAsync(existing, "command.ledger.insert.formula.title", ct).ConfigureAwait(true) is not { } trimmed) return;

        if (Context.SelectedObject is { Content: Document.Embedding.FormulaObject formula } placed
            && Context.Workbook is { } book)
        {
            var sheet = book.Sheets[Context.SheetIndex];
            var index = sheet.Objects.IndexOf(placed);

            if (index >= 0)
            {
                var replacement = placed with { Content = formula with { Expression = trimmed } };

                sheet.Objects[index] = replacement;

                Context.History?.Push(new Document.Commands.DelegateAction(
                    texts["history.formula.edit"],
                    redo: () => sheet.Objects[index] = replacement,
                    undo: () => sheet.Objects[index] = placed));

                Context.SelectObject(replacement);
                Context.RequestRefresh();

                return;
            }
        }

        var id = Guid.NewGuid().ToString("N");

        Place(
            new Document.Embedding.FormulaObject
            {
                Id = id,
                Expression = trimmed,
                NaturalSize = Document.Embedding.FormulaObject.StandardSize,
            },
            4,
            1.5);
    }

    private async Task GalleryPanelAsync(CancellationToken ct)
    {
        var chosen = await dialogs.ShowAsync<VectorAsset>(
            new PulseOffice.UI.Controls.Shapes.AssetPickerView(assets, texts),
            new DialogOptions { TitleKey = "command.ledger.view.gallery.title", PreferredWidth = 620 }, ct).ConfigureAwait(true);
        if (chosen is null) return;

        Place(new VectorGraphicObject
        {
            Id = Guid.NewGuid().ToString("N"),
            Asset = chosen,
            RecolorHex = chosen.IsRecolorable ? "#334155" : null,
        }, 3, 3);
    }

    private void Place(Document.Embedding.EmbeddedObject content, double width, double height) =>
        insertion.Insert(new PlacedObject
        {
            Id = content.Id,
            Content = content,
            Anchor = Context.Selection.Start,
            Size = new DocumentSize(Twip.FromCentimeters(width), Twip.FromCentimeters(height)),
        }, texts["history.shape.insert"]);

    private static string Lower(string value) => char.ToLowerInvariant(value[0]) + value[1..];
}
