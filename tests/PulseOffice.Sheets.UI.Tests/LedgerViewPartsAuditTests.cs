using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.UI.LedgerViewParts;
using PulseOffice.UI.Controls.Forms;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class LedgerViewPartsAuditTests
{
    private sealed class Parts
    {
        public Parts()
        {
            var provider = new ServiceCollection().AddSheetsEngine().BuildServiceProvider();
            Engine = provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook);
            var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
            var status = new PulseOffice.UI.Controls.Services.StatusBarService(
                TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
                new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));
            var presenter = new PulseOffice.Sheets.Formatting.CellPresenter(new PulseOffice.Sheets.Formatting.NumberFormatter(
                System.Globalization.CultureInfo.InvariantCulture, serial => new DateTime(1899, 12, 30).AddDays(serial)));
            Grid = new SheetGridView(Engine, presenter, new ConditionalFormattingEvaluator(provider.GetRequiredService<Evaluator>()), new SheetSelection());
            var writer = new LedgerCellWriter(Workbook, Engine, History, status, texts, new PulseOffice.Shell.DocumentWorkspace(), Grid,
                null, null, null, null);
            Objects = new LedgerObjectEditing(Workbook, Grid, History, status, texts, new SheetConnectors(), new FormControlBinding(PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale.Invariant()), writer);
        }

        public Workbook Workbook { get; } = Workbook.CreateDefault();

        public UndoStack History { get; } = new();

        public CalculationEngine Engine { get; }

        public SheetGridView Grid { get; }

        public LedgerObjectEditing Objects { get; }

        public Worksheet Sheet => Workbook.Sheets[0];
    }

    private static PlacedObject Placed(PulseOffice.Document.Embedding.EmbeddedObject content) => new()
    {
        Id = content.Id,
        Anchor = new CellAddress(1, 1),
        Size = new PulseOffice.Foundation.Units.DocumentSize(Twip.FromPoints(40), Twip.FromPoints(30)),
        Content = content,
    };

    [Fact]
    public void Disegni_forme_e_testi_delle_forme_non_aggirano_la_protezione_del_foglio()
    {
        var parts = new Parts();
        var shape = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle };
        var freeform = new FreeformObject { Id = "f1", Path = new FreeformPath([]) };
        var placedShape = Placed(shape);
        var placedFreeform = Placed(freeform);
        parts.Sheet.Objects.Add(placedShape);
        parts.Sheet.Objects.Add(placedFreeform);
        parts.Sheet.Protection = new SheetProtection { IsEnabled = true };

        parts.Objects.InsertFreeform(new PulseOffice.Sheets.UI.FreeformCompletedEventArgs(
            new FreeformPath([]), new CellAddress(0, 0), Twip.Zero, Twip.Zero, placedShape.Size, false));
        parts.Objects.ApplyFreeform(new PulseOffice.Sheets.UI.FreeformEditedEventArgs(placedFreeform, new FreeformPath([])));
        parts.Objects.ApplyAdjustment(new PulseOffice.Sheets.UI.ShapeAdjustedEventArgs(placedShape, shape with { RotationDegrees = 30 }));
        var replaced = parts.Objects.ReplaceShape(parts.Sheet, placedShape, shape with { RotationDegrees = 45 }, "text");

        Assert.Null(replaced);
        Assert.Equal(2, parts.Sheet.Objects.Count);
        Assert.Same(placedShape, parts.Sheet.Objects[0]);
        Assert.Same(placedFreeform, parts.Sheet.Objects[1]);
        Assert.False(parts.History.CanUndo);
    }

    [Fact]
    public void Il_filtro_nasconde_e_mostra_le_righe_del_suo_foglio_senza_fissarne_l_altezza()
    {
        var parts = new Parts();
        parts.Workbook.AddSheet("Second");
        var first = parts.Workbook.Sheets[0];
        var second = parts.Workbook.Sheets[1];
        first.Layout.SetRowHeight(5, Twip.FromPoints(30));
        var viewport = new PulseOffice.Sheets.UI.GridParts.GridViewport(parts.Engine,
            PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact), 0, () => new Avalonia.Size(800, 600), new PulseOffice.Sheets.UI.Outline.OutlineGutter());

        viewport.ApplyRowFilter(first, new HashSet<int> { 3, 5 });
        Assert.True(first.Layout.IsRowHidden(3));
        Assert.True(first.Layout.IsRowHidden(5));

        viewport.SheetIndex = 1;
        viewport.ApplyRowFilter(second, null);
        Assert.True(first.Layout.IsRowHidden(3));
        Assert.False(second.Layout.RowHeights.ContainsKey(3));

        viewport.SheetIndex = 0;
        viewport.ApplyRowFilter(first, null);
        Assert.False(first.Layout.RowHeights.ContainsKey(3));
        Assert.Equal(Twip.FromPoints(30), first.Layout.RowHeight(5));
    }

    [Fact]
    public void Il_rettangolo_di_un_unione_lunga_un_milione_di_righe_si_calcola_senza_scorrerle()
    {
        var parts = new Parts();
        var viewport = new PulseOffice.Sheets.UI.GridParts.GridViewport(parts.Engine,
            PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact), 0, () => new Avalonia.Size(800, 600), new PulseOffice.Sheets.UI.Outline.OutlineGutter());
        parts.Sheet.Layout.SetRowHeight(10, Twip.FromPoints(40));
        var region = new CellRange(new CellAddress(0, 0), new CellAddress(CellAddress.MaxRows - 1, 2));

        var rect = viewport.MergedRect(region, default);

        var geometry = viewport.Geometry;
        var expected = 0d;
        for (var row = 0; row < CellAddress.MaxRows; row++) expected += geometry.RowHeight(row);
        Assert.Equal(expected, rect.Height, 3);
        Assert.Equal(geometry.ColumnWidth(0) * 3, rect.Width, 3);
    }

    [Fact]
    public void I_pennelli_e_le_penne_della_griglia_si_riusano_fra_le_celle()
    {
        var paints = new PulseOffice.Sheets.UI.GridParts.Painting.GridPaints();

        Assert.Same(paints.Brush("#FF0000"), paints.Brush("#ff0000"));
        Assert.Same(paints.Brush("BLUE"), paints.Brush(Avalonia.Media.Colors.Blue));
        Assert.Null(paints.Brush("not a colour"));
        Assert.Null(paints.Brush((string?)null));
        Assert.Same(paints.Pen(Avalonia.Media.Colors.Black, 1, Avalonia.Media.DashStyle.Dash), paints.Pen(Avalonia.Media.Colors.Black, 1, Avalonia.Media.DashStyle.Dash));
        Assert.NotSame(paints.Pen(Avalonia.Media.Colors.Black, 1), paints.Pen(Avalonia.Media.Colors.Black, 2));
    }

    [Fact]
    public void Modifiche_successive_partite_dallo_stesso_oggetto_si_applicano_tutte()
    {
        var parts = new Parts();
        var shape = new ShapeObject { Id = "s1", Shape = ShapeKind.Rectangle };
        var placed = Placed(shape);
        parts.Sheet.Objects.Add(placed);

        parts.Objects.ApplyAdjustment(new PulseOffice.Sheets.UI.ShapeAdjustedEventArgs(placed, shape with { RotationDegrees = 10 }));
        parts.Objects.ApplyAdjustment(new PulseOffice.Sheets.UI.ShapeAdjustedEventArgs(placed, shape with { RotationDegrees = 20 }));

        Assert.Equal(20, Assert.IsType<ShapeObject>(Assert.Single(parts.Sheet.Objects).Content).RotationDegrees);
        parts.History.Undo();
        Assert.Equal(10, Assert.IsType<ShapeObject>(Assert.Single(parts.Sheet.Objects).Content).RotationDegrees);
    }

    private sealed class ObservingFormEvents(Worksheet sheet) : IFormEventRunner
    {
        public List<string?> SeenLabels { get; } = [];

        public Task<bool> RaiseAsync(PulseOffice.Document.Forms.FormControlProperties control, PulseOffice.Document.Forms.FormControlEvent raised, CancellationToken ct = default)
        {
            SeenLabels.Add(sheet.Objects.Select(placed => placed.Content).OfType<PulseOffice.Document.Forms.FormControlObject>().Single().Properties.Label);
            if (sheet.Objects.All(placed => placed.Id != "inserted")) sheet.Objects.Insert(0, Placed(new ShapeObject { Id = "inserted", Shape = ShapeKind.Rectangle }));
            return Task.FromResult(true);
        }
    }

    [Fact]
    public void Form_control_macros_run_after_the_change_is_applied()
    {
        var parts = new Parts();
        var form = new PulseOffice.Document.Forms.FormControlObject
        {
            Id = "c1",
            ControlKind = PulseOffice.Document.Forms.FormControlKind.PushButton,
            Properties = new PulseOffice.Document.Forms.FormControlProperties { Label = "old" },
        };
        var placed = Placed(form);
        parts.Sheet.Objects.Add(placed);
        var events = new ObservingFormEvents(parts.Sheet);
        parts.Objects.FormEvents = events;

        parts.Objects.ApplyFormControl(new FormControlInteractionEventArgs(placed, form.Properties with { Label = "new" }));

        Assert.Equal("new", Assert.Single(events.SeenLabels));
        Assert.Equal(2, parts.Sheet.Objects.Count);
        Assert.Contains(parts.Sheet.Objects, item => item.Id == "inserted");
        Assert.Equal("new", Assert.IsType<PulseOffice.Document.Forms.FormControlObject>(parts.Sheet.Objects.Single(item => item.Id == "c1").Content).Properties.Label);
    }
}
