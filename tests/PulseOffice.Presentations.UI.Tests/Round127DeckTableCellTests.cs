using PulseOffice.Data.UI;
using PulseOffice.Document.Borders;
using PulseOffice.Document.Commands;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.Foundation.Zoom;
using PulseOffice.Presentations;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round127DeckTableCellTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private readonly ISlideFactory _factory = new SlideFactory();

    private sealed class Scene
    {
        public required Presentation Deck { get; init; }
        public required UndoStack History { get; init; }
        public required DeckFormatCommands Commands { get; init; }
        public required DeckViewOptions Options { get; init; }
        public SlideElement? Selected { get; set; }

        public DataTableObject Table => (DataTableObject)((ObjectElement)Deck.Slides[0].Find("t1")!).Content;

        public CommandDefinition Command(string id) => Commands.GetCommands().Single(command => command.Id.Value == id);

        public CommandContext Context => new(ModuleKind.Presentations, SelectionKind.None, DocumentState.DocumentOpen);
    }

    private DataTableObject Table(bool ownLook) => new()
    {
        Id = "t1",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [new DataColumn("A", DataValueKind.Text), new DataColumn("B", DataValueKind.Number)],
            Rows = [["1", 2.0], ["3", 4.0]],
        },
        Style = new DataTableStyle { HeaderRow = true, OwnLook = ownLook, Grid = !ownLook },
    };

    private Scene Build(bool ownLook)
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var slide = _factory.Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        var element = new ObjectElement { Id = "t1", Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(4000), new Twip(2000)), Content = Table(ownLook) };
        slide.Add(element);
        deck.AddExisting(slide);

        var history = new UndoStack();
        var texts = new FakeTextCatalog();
        var actions = new DeckActions(deck, history, _factory, texts);
        var context = new ActiveDeckContext();
        var seed = new DeckTableLookSeed();
        var cells = new DeckTableCellEditing(new DeckTableCellFormatting(new DeckTableBorders(new BorderFrameResolver(), seed), seed));
        var commands = new DeckFormatCommands(context, new PulseOffice.Text.Rich.RichTextFormatter(),
            new PulseOffice.Text.Scripts.WidthConversion(), new PulseOffice.Text.Scripts.KanaConversion(), new PulseOffice.Text.TextCases(), cells);
        var options = new DeckViewOptions();
        var scene = new Scene { Deck = deck, History = history, Commands = commands, Options = options };
        context.Attach(new DeckTarget(deck, actions, () => deck.Slides[0], () => 0, () => { },
            _ => { }, () => false, () => scene.Selected is { } selected ? deck.Slides[0].Find(selected.Id) : null, () => scene.Selected is { } s ? [s] : [], _ => { }, _ => { }, () => null,
            () => Task.CompletedTask, _ => { }, () => DeckViewMode.Normal, () => null,
            () => new ViewZoom(ViewZoomMode.FitWhole, 1), _ => { }, () => 1, () => false, _ => false,
            options, _ => { }, (_, _) => false, (_, _, _) => false, () => null, () => null, () => null));
        scene.Selected = element;
        return scene;
    }

    [Fact]
    public async Task Italic_applies_to_the_selected_table_cells_in_one_undo_step()
    {
        var scene = Build(ownLook: true);
        scene.Options.SetTableCells(("t1", new TableCellRange(0, 1, 0, 1)));

        Assert.True(scene.Command("deck.format.italic").IsEnabled(scene.Context));
        await scene.Command("deck.format.italic").Execute(scene.Context, CancellationToken.None);

        var looks = new TableCellIndex(scene.Table);
        Assert.True(looks.Format(0, 1)?.Look?.Italic);
        Assert.NotEqual(true, looks.Format(0, 0)?.Look?.Italic);
        Assert.True(scene.Command("deck.format.italic").CheckedWhen!(scene.Context));

        scene.History.Undo();
        Assert.Empty(scene.Table.CellFormats);
    }

    [Fact]
    public async Task Bold_toggles_off_when_the_first_cell_is_bold()
    {
        var scene = Build(ownLook: false);
        scene.Options.SetTableCells(("t1", new TableCellRange(TableCellFormat.HeaderRow, 0, TableCellFormat.HeaderRow, 1)));

        await scene.Command("deck.format.bold").Execute(scene.Context, CancellationToken.None);

        Assert.True(scene.Table.Style.OwnLook);
        var looks = new TableCellIndex(scene.Table);
        Assert.False(looks.Format(TableCellFormat.HeaderRow, 0)?.Look?.Bold);
        Assert.True(looks.Format(0, 0)?.Look is { Bold: false, Top: not null });
        Assert.Equal(CellHorizontalAlign.Right, looks.Format(0, 1)?.Look?.Horizontal);
    }

    [Fact]
    public async Task Underline_strike_colour_and_alignment_reach_a_whole_table_without_cell_selection()
    {
        var scene = Build(ownLook: true);

        await scene.Command("deck.format.underline").Execute(scene.Context, CancellationToken.None);
        await scene.Command("deck.format.strikethrough").Execute(scene.Context, CancellationToken.None);
        await scene.Command("deck.format.colorRed").Execute(scene.Context, CancellationToken.None);
        await scene.Command("deck.paragraph.alignCenter").Execute(scene.Context, CancellationToken.None);

        Assert.All(scene.Table.CellFormats, format => Assert.True(format.Look is { Underline: true, Strikethrough: true, TextHex: "#C0392B", Horizontal: CellHorizontalAlign.Center }));
        Assert.Equal(6, scene.Table.CellFormats.Count);
    }

    [Fact]
    public void The_text_style_of_a_cell_carries_italic_weight_family_and_decorations()
    {
        var styles = new TableCellTextStyles();
        var look = new TableCellLook { Italic = true, Bold = true, Underline = true, Strikethrough = true, FontFamily = "Georgia" };

        var typeface = styles.Typeface(look);

        Assert.Equal(Avalonia.Media.FontStyle.Italic, typeface.Style);
        Assert.Equal(Avalonia.Media.FontWeight.SemiBold, typeface.Weight);
        Assert.Equal("Georgia", typeface.FontFamily.Name);
        Assert.Equal(2, styles.Decorations(look)!.Count);
        Assert.Null(styles.Decorations(TableCellLook.Plain));
    }

    [Fact]
    public void A_row_or_column_added_in_the_table_editor_takes_the_look_of_the_last_one()
    {
        var model = new TableEditModel();
        var look = new TableCellLook { FillHex = "#EEEEEE", Italic = true };
        IReadOnlyList<TableCellFormat> formats = [new TableCellFormat(0, 0, CellVerticalAlign.Center) { Look = look }, new TableCellFormat(TableCellFormat.HeaderRow, 0, CellVerticalAlign.Top) { Look = TableCellLook.Plain }];

        var rows = new TableCellIndex(Table(true) with { CellFormats = model.WithAppendedRow(formats, 1) });
        var columns = new TableCellIndex(Table(true) with { CellFormats = model.WithAppendedColumn(formats, 1) });

        Assert.Equal(look, rows.Format(1, 0)?.Look);
        Assert.Equal(CellVerticalAlign.Center, rows.Format(1, 0)?.VerticalAlign);
        Assert.Equal(look, columns.Format(0, 1)?.Look);
        Assert.Equal(TableCellLook.Plain, columns.Format(TableCellFormat.HeaderRow, 1)?.Look);
    }
}
