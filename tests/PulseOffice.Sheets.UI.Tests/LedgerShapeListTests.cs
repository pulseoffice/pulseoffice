using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.Testing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.TextFormat;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class LedgerShapeListTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private static CommandContext Run => new(PulseOffice.Foundation.Modules.ModuleKind.Sheets, SelectionKind.None, DocumentState.DocumentOpen);

    private sealed class Answering(ListLevelFormat chosen, int level) : IListFormatDialog
    {
        public ListFormatRequest? Asked { get; private set; }

        public Task<ListLevelsAnswer?> AskAsync(ListFormatRequest request, string titleKey, CancellationToken ct)
        {
            Asked = request;
            return Task.FromResult<ListLevelsAnswer?>(new ListLevelsAnswer([.. Enumerable.Repeat(chosen, request.Levels.Count)], level));
        }
    }

    private sealed class Parts
    {
        public Parts()
        {
            var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
            var engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook);
            var status = new PulseOffice.UI.Controls.Services.StatusBarService(
                TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
                new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));
            var presenter = new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(
                System.Globalization.CultureInfo.InvariantCulture, serial => new DateTime(1899, 12, 30).AddDays(serial)));
            var grid = new SheetGridView(engine, presenter, new ConditionalFormattingEvaluator(provider.GetRequiredService<Evaluator>()), new SheetSelection());
            var writer = new LedgerCellWriter(Workbook, engine, History, status, Texts, new PulseOffice.Shell.DocumentWorkspace(), grid, null, null, null, null);
            Objects = new LedgerObjectEditing(Workbook, grid, History, status, Texts, new SheetConnectors(), new FormControlBinding(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant()), writer);
            Sheet.Objects.Add(new PlacedObject
            {
                Id = "Forma",
                Anchor = new CellAddress(1, 1),
                Size = new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(3)),
                Content = new ShapeObject { Id = "Forma", Shape = ShapeKind.Rectangle, Text = "Uno\nDue\nTre" },
            });
            Context.Attach(new SheetTarget
            {
                Workbook = Workbook,
                SheetIndex = 0,
                History = History,
                Engine = engine,
                Selection = () => new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)),
                WriteSelection = _ => { },
                ReadViewOption = _ => false,
                WriteViewOption = (_, _) => { },
                Refresh = () => { },
                FreezePanes = (_, _) => { },
                SplitPanes = (_, _) => { },
                ReadPanesSplit = () => false,
                ApplyRowFilter = (_, _) => { },
                ReadZoom = () => 1,
                WriteZoom = _ => { },
                ReadSelectedObject = () => Sheet.Objects[0],
                ReadSelectedObjects = () => [Sheet.Objects[0]],
                WriteSelectedObjects = _ => { },
                SelectSheet = _ => { },
                WriteSelectedObject = _ => { },
                ShapeText = new ShapeText.SheetShapeText(() => true, () => Styling.CellStyle.Default, (_, _) => { },
                    new ShapeText.SheetShapeParagraphs(
                        () => Paragraphs.First(Shape, -1, -1),
                        (description, change) => Objects.ReplaceShape(Sheet, Sheet.Objects[0], Paragraphs.Apply(Shape, -1, -1, change), description))),
            });
        }

        public PulseOffice.Localization.TextCatalog Texts { get; } = new(PulseOffice.Localization.UiLanguage.English);

        public Workbook Workbook { get; } = Workbook.CreateDefault();

        public UndoStack History { get; } = new();

        public ActiveSheetContext Context { get; } = new();

        public IShapeTextParagraphs Paragraphs { get; } = new ShapeTextParagraphs();

        public LedgerObjectEditing Objects { get; }

        public Worksheet Sheet => Workbook.Sheets[0];

        public ShapeObject Shape => (ShapeObject)Sheet.Objects[0].Content;

        public CommandDefinition Command(IListFormatDialog dialog) =>
            Assert.Single(new LedgerShapeListCommands(Context, dialog, new ParagraphListFormats(), Texts).GetCommands(), command => command.Id.Value == "ledger.shapeText.bulletsNumbering");
    }

    [Fact]
    public Task Il_dialogo_condiviso_mette_un_elenco_a_immagine_nel_testo_della_forma_e_si_annulla() => Session.Dispatch(async () =>
    {
        var parts = new Parts();
        var dialog = new Answering(new ListLevelFormat(ListNumbering.Bullet) { Suffix = string.Empty, Picture = new ListBulletPicture(Png, "image/png", 9, 9) }, 1);
        var command = parts.Command(dialog);

        Assert.True(command.EnabledWhen!(Run));
        await command.Execute(Run, CancellationToken.None);

        Assert.Equal(ListFeatures.Symbol | ListFeatures.Picture | ListFeatures.Affixes | ListFeatures.Start, dialog.Asked!.Features);
        var items = parts.Shape.Lists!;
        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.Equal(1, item!.Level));
        Assert.Equal(Png, items[0]!.Symbol!.Picture!.Data.ToArray());

        parts.History.Undo();
        Assert.Null(parts.Shape.Lists);
        parts.History.Redo();
        Assert.Equal(3, parts.Shape.Lists!.Count);
    }, CancellationToken.None);

    [Fact]
    public Task Right_to_left_shape_text_is_stored_checked_and_undone() => Session.Dispatch(async () =>
    {
        var parts = new Parts();
        var commands = new LedgerShapeListCommands(parts.Context, new Answering(new ListLevelFormat(ListNumbering.None), 0), new ParagraphListFormats(), parts.Texts).GetCommands().ToList();
        var rightToLeft = commands.Single(command => command.Id.Value == "ledger.shapeText.rightToLeft");

        await rightToLeft.Execute(Run, CancellationToken.None);

        Assert.All(parts.Shape.Directions!, direction => Assert.Equal(PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft, direction));
        Assert.True(rightToLeft.CheckedWhen!(Run));
        parts.History.Undo();
        Assert.Null(parts.Shape.Directions);
        Assert.False(rightToLeft.CheckedWhen!(Run));
    }, CancellationToken.None);

    [Fact]
    public Task Numerare_e_poi_togliere_l_elenco_riporta_il_testo_semplice() => Session.Dispatch(async () =>
    {
        var parts = new Parts();
        await parts.Command(new Answering(new ListLevelFormat(ListNumbering.Arabic), 0)).Execute(Run, CancellationToken.None);
        Assert.All(parts.Shape.Lists!, item => Assert.True(item!.Numbered));
        Assert.True(parts.Paragraphs.First(parts.Shape, -1, -1).Numbered);

        await parts.Command(new Answering(new ListLevelFormat(ListNumbering.None), 0)).Execute(Run, CancellationToken.None);
        Assert.Null(parts.Shape.Lists);
        Assert.Equal("Uno\nDue\nTre", parts.Shape.Text);
    }, CancellationToken.None);

    [Fact]
    public Task Il_comando_si_spegne_su_un_foglio_protetto() => Session.Dispatch(() =>
    {
        var parts = new Parts();
        var command = parts.Command(new Answering(new ListLevelFormat(ListNumbering.Bullet), 0));
        parts.Sheet.Protection = parts.Sheet.Protection with { IsEnabled = true };

        Assert.False(command.EnabledWhen!(Run));
    }, CancellationToken.None);

    [Fact]
    public Task Il_dialogo_condiviso_numera_la_forma_con_lettere_affissi_e_inizio() => Session.Dispatch(async () =>
    {
        var parts = new Parts();
        await parts.Command(new Answering(new ListLevelFormat(ListNumbering.UpperRoman) { Prefix = "(", Suffix = ")", Start = 4 }, 0)).Execute(Run, CancellationToken.None);

        var wanted = new ListNumberFormat { Style = ListNumberStyle.UpperRoman, Prefix = "(", Suffix = ")", Start = 4 };
        Assert.All(parts.Shape.Lists!, item => Assert.Equal(wanted, item!.NumberFormat));
        Assert.Equal(["(IV)", "(V)", "(VI)"], new ListLabels().For(new ShapeRichText().ToRich(parts.Shape).Paragraphs));
        var asked = new ParagraphListFormats().Request(parts.Paragraphs.First(parts.Shape, -1, -1));
        Assert.Equal(ListNumbering.UpperRoman, asked.Levels[0].Numbering);
        Assert.Equal(4, asked.Levels[0].Start);
        Assert.Contains(ListNumbering.LowerLetter, asked.Numberings);
    }, CancellationToken.None);
}
