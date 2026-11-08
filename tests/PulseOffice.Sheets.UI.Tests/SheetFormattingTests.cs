using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.UI;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Values;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class SheetFormattingTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSheetsEngine()
            .BuildServiceProvider()
            .GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static (Workbook Workbook, ISheetFormattingService Formatting, UndoStack History)
        Build(CellRange selection)
    {
        var workbook = Workbook.CreateDefault();
        var context = new ActiveSheetContext();
        var history = new UndoStack();

        context.Attach(Target(workbook, history, () => selection));
        return (workbook, Formatting(context), history);
    }

    private static SheetTarget Target(Workbook workbook, UndoStack history, Func<CellRange> selection)
    {
        var zoom = 1.0;

        return new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = history,
            Engine = Engines.Create(workbook),
            Selection = selection,
            WriteSelection = _ => { },
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => zoom,
            WriteZoom = value => zoom = value,
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        };
    }

    [Fact]
    public void SenzaFoglioAttivoNonSiFormattaNulla()
    {
        var context = new ActiveSheetContext();
        var formatting = Formatting(context);

        Assert.False(formatting.CanFormat);

        formatting.ApplyToSelection(style => style with { WrapText = true });
    }

    [Fact]
    public void LoStileSiDerivaInveceDiSostituire()
    {
        var (workbook, formatting, _) = Build(CellRange.Single(A("A1")));

        formatting.ApplyToSelection(style => style with { Font = style.Font with { Italic = true } });
        formatting.ApplyToSelection(style => style with { Font = style.Font with { Bold = true } });

        var style = workbook.Styles.Get(workbook.Sheets[0][A("A1")].StyleId);

        Assert.True(style.Font.Italic);
        Assert.True(style.Font.Bold);
    }

    [Fact]
    public void LaModificaSiApplicaATutteLeCelleDellaSelezione()
    {
        var range = new CellRange(A("A1"), A("C3"));
        var (workbook, formatting, _) = Build(range);

        formatting.ApplyToSelection(style => style with { BackgroundHex = "#FFFF00" });

        foreach (var address in range.Cells())
            Assert.Equal("#FFFF00", workbook.Styles.Get(workbook.Sheets[0][address].StyleId).BackgroundHex);
    }

    [Fact]
    public void CelleFormattateUgualeCondividonoUnSoloStile()
    {
        var range = new CellRange(A("A1"), A("J10"));
        var (workbook, formatting, _) = Build(range);

        var before = workbook.Styles.Styles.Count;

        formatting.ApplyToSelection(style => style with { Font = style.Font with { Bold = true } });

        Assert.Equal(before + 1, workbook.Styles.Styles.Count);

        var ids = range.Cells().Select(address => workbook.Sheets[0][address].StyleId).Distinct();
        Assert.Single(ids);
    }

    [Fact]
    public void LInversioneAccendeSeAnchesoloUnaCellaNonCeLHa()
    {
        var range = new CellRange(A("A1"), A("A3"));
        var (workbook, formatting, _) = Build(range);

        var bold = workbook.Styles.Intern(new CellStyle { Font = CellFont.Default with { Bold = true } });
        workbook.Sheets[0].SetStyle(A("A1"), bold);

        formatting.ToggleOnSelection(
            style => style.Font.Bold,
            (style, value) => style with { Font = style.Font with { Bold = value } });

        foreach (var address in range.Cells())
            Assert.True(workbook.Styles.Get(workbook.Sheets[0][address].StyleId).Font.Bold);
    }

    [Fact]
    public void LInversioneSpegneSoloSeTutteCeLHanno()
    {
        var range = new CellRange(A("A1"), A("A3"));
        var (workbook, formatting, _) = Build(range);

        Func<CellStyle, bool> read = style => style.Font.Bold;
        Func<CellStyle, bool, CellStyle> write =
            (style, value) => style with { Font = style.Font with { Bold = value } };

        formatting.ToggleOnSelection(read, write);
        formatting.ToggleOnSelection(read, write);

        foreach (var address in range.Cells())
            Assert.False(workbook.Styles.Get(workbook.Sheets[0][address].StyleId).Font.Bold);
    }

    [Fact]
    public void LoStileCorrenteEquelloDellaCellaDiPartenza()
    {
        var (workbook, formatting, _) = Build(CellRange.Single(A("B2")));

        var style = workbook.Styles.Intern(new CellStyle
        {
            Horizontal = CellHorizontalAlignment.Center,
        });
        workbook.Sheets[0].SetStyle(A("B2"), style);

        Assert.Equal(CellHorizontalAlignment.Center, formatting.CurrentStyle.Horizontal);
    }

    [Fact]
    public void FormattareUnaCellaVuotaLaConservaComunque()
    {
        var (workbook, formatting, _) = Build(CellRange.Single(A("D4")));

        formatting.ApplyToSelection(style => style with { BackgroundHex = "#00FF00" });

        Assert.Equal(1, workbook.Sheets[0].CellCount);
        Assert.True(workbook.Sheets[0][A("D4")].HasStyle);
    }

    [Fact]
    public void LaFormattazioneSiAnnullaInUnColpoSolo()
    {
        var range = new CellRange(A("A1"), A("C3"));
        var (workbook, formatting, history) = Build(range);

        formatting.ApplyToSelection("Grassetto",
            style => style with { Font = style.Font with { Bold = true } });

        Assert.Equal("Grassetto", history.UndoDescription);
        history.Undo();

        foreach (var address in range.Cells())
            Assert.False(workbook.Styles.Get(workbook.Sheets[0][address].StyleId).Font.Bold);

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void UnaFormattazioneCheNonCambiaNullaNonEntraNellaCronologia()
    {
        var (_, formatting, history) = Build(CellRange.Single(A("A1")));

        formatting.ApplyToSelection(style => style with { WrapText = true });
        formatting.ApplyToSelection(style => style with { WrapText = true });

        history.Undo();
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void LaFormattazioneNonTraccaIlContenuto()
    {
        var (workbook, formatting, _) = Build(CellRange.Single(A("A1")));

        workbook.Sheets[0].SetValue(A("A1"), CellValue.Number(42));
        formatting.ApplyToSelection(style => style with { Font = style.Font with { Bold = true } });

        Assert.Equal(42, workbook.Sheets[0][A("A1")].Value.AsNumber());
    }

    [Fact]
    public void ChiudereUnDocumentoInSecondoPianoNonAzzeraIlContesto()
    {
        var context = new ActiveSheetContext();
        var attivo = Workbook.CreateDefault();
        var altro = Workbook.CreateDefault();

        context.Attach(Target(attivo, new UndoStack(), () => CellRange.Single(CellAddress.First)));
        context.Detach(altro);

        Assert.Same(attivo, context.Workbook);
    }

    [Fact]
    public void FormattareUnaColonnaInteraVestaAncheLeCelleScritteDopo()
    {
        var column = new CellRange(new CellAddress(0, 1), new CellAddress(CellAddress.MaxRows - 1, 1));
        var (workbook, formatting, history) = Build(column);
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("B2"), CellValue.Number(1));

        formatting.ToggleOnSelection(style => style.Font.Bold, (style, on) => style with { Font = style.Font with { Bold = on } });

        Assert.True(workbook.Styles.Get(sheet.StyleIdAt(A("B2"))).Font.Bold);
        sheet.SetValue(A("B900"), CellValue.Number(2));
        Assert.True(workbook.Styles.Get(sheet[A("B900")].StyleId).Font.Bold);
        Assert.False(workbook.Styles.Get(sheet.StyleIdAt(A("C900"))).Font.Bold);

        history.Undo();

        Assert.Null(sheet.Layout.ColumnStyle(1));
        Assert.False(workbook.Styles.Get(sheet.StyleIdAt(A("B2"))).Font.Bold);
    }

    [Fact]
    public void TogliereIlGrassettoAUnaCellaDellaColonnaLaLasciaSemplice()
    {
        var (workbook, _, _) = Build(new CellRange(new CellAddress(0, 1), new CellAddress(CellAddress.MaxRows - 1, 1)));
        var sheet = workbook.Sheets[0];
        sheet.Layout.SetColumnStyle(1, workbook.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Bold = true } }));
        var single = Single(workbook, A("B3"));

        single.ToggleOnSelection(style => style.Font.Bold, (style, on) => style with { Font = style.Font with { Bold = on } });

        Assert.False(workbook.Styles.Get(sheet.StyleIdAt(A("B3"))).Font.Bold);
        sheet.SetValue(A("B3"), CellValue.Number(4));
        Assert.False(workbook.Styles.Get(sheet.StyleIdAt(A("B3"))).Font.Bold);
        Assert.True(workbook.Styles.Get(sheet.StyleIdAt(A("B4"))).Font.Bold);
    }

    [Fact]
    public void AnnullareSuUnaCellaEreditataLaRestituisceAllaColonna()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var bold = workbook.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Bold = true } });
        sheet.Layout.SetColumnStyle(1, bold);
        var history = new UndoStack();
        var context = new ActiveSheetContext();
        context.Attach(Target(workbook, history, () => new CellRange(A("B3"), A("B3"))));
        var formatting = Formatting(context);

        formatting.ApplyToSelection(_ => CellStyle.Default);
        Assert.Equal(StyleCatalog.DefaultStyleId, sheet.StyleIdAt(A("B3")));

        history.Undo();

        Assert.Null(sheet.OwnStyleAt(A("B3")));
        sheet.Layout.SetColumnStyle(1, null);
        Assert.Equal(StyleCatalog.DefaultStyleId, sheet.StyleIdAt(A("B3")));
    }

    private static ISheetFormattingService Single(Workbook workbook, CellAddress address)
    {
        var context = new ActiveSheetContext();
        context.Attach(Target(workbook, new UndoStack(), () => new CellRange(address, address)));
        return Formatting(context);
    }

    private static SheetFormattingService Formatting(ActiveSheetContext context)
    {
        var workspace = new PulseOffice.Shell.DocumentWorkspace();
        return new SheetFormattingService(
            context,
            new PulseOffice.Sheets.Addressing.SelectionWorkArea(),
            new StyleBandFormatter(),
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English),
            new SheetEditGuard(),
            workspace,
            new PulseOffice.UI.Controls.Services.StatusBarService(
                TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
                new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System)));
    }

    [Fact]
    public async Task InserireUnaTabellaCreaAreaDatiConIntestazioneEStileESiAnnullaInUnPasso()
    {
        var workbook = Workbook.CreateDefault();
        var history = new UndoStack();
        var selection = CellRange.Single(A("B2"));
        var context = new ActiveSheetContext();
        context.Attach(Target(workbook, history, () => selection) with { WriteSelection = range => selection = range });
        var texts = new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);
        var status = new PulseOffice.UI.Controls.Services.StatusBarService(
            TimeProvider.System, new PulseOffice.UI.Controls.Notifications.NotificationCenter(TimeProvider.System),
            new PulseOffice.UI.Controls.Notifications.NotificationHistory(TimeProvider.System));
        var commands = new LedgerTableInsertCommands(context, new Engine.Operations.SheetRegions(), new Engine.Operations.SheetSnapshots(),
            new PulseOffice.UI.Controls.Tables.Insert.TableInsertPrompt(null!, new PulseOffice.Document.Tables.TableStylePresets(), null!, null!, texts),
            new LedgerTableLooks(Formatting(context)), status, texts);
        var insert = commands.GetCommands().Single();
        Assert.IsType<PulseOffice.UI.Commands.TableGridSpec>(insert.RibbonControl);

        await insert.Execute(new PulseOffice.UI.Commands.CommandContext(PulseOffice.Foundation.Modules.ModuleKind.Sheets, PulseOffice.UI.Commands.SelectionKind.None, PulseOffice.UI.Commands.DocumentState.DocumentOpen)
        {
            Argument = new PulseOffice.UI.Commands.RibbonTableSize(3, 2),
        }, CancellationToken.None);

        var sheet = workbook.Sheets[0];
        var table = Assert.Single(workbook.DatabaseRanges.All);
        Assert.Equal("Table1", table.Name);
        Assert.Equal("B2:C4", table.Range.ToString());
        Assert.True(table.HasHeader);
        Assert.Equal("Column 1", sheet[A("B2")].Value.ToString());
        Assert.Equal("Column 2", sheet[A("C2")].Value.ToString());
        Assert.True(workbook.Styles.Get(sheet.StyleIdAt(A("B2"))).Font.Bold);
        Assert.False(workbook.Styles.Get(sheet.StyleIdAt(A("B3"))).Font.Bold);

        history.Undo();
        Assert.Empty(workbook.DatabaseRanges.All);
        Assert.True(sheet[A("B2")].Value.IsEmpty);
        Assert.False(workbook.Styles.Get(sheet.StyleIdAt(A("B2"))).Font.Bold);
    }

    [Fact]
    public void UnFoglioProtettoSenzaPermessoNonSiFormatta()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Protection = new SheetProtection { IsEnabled = true };
        var context = new ActiveSheetContext();
        context.Attach(Target(workbook, new UndoStack(), () => new CellRange(A("B3"), A("B3"))));

        Formatting(context).ApplyToSelection(style => style with { Font = style.Font with { Bold = true } });

        Assert.False(workbook.Styles.Get(sheet.StyleIdAt(A("B3"))).Font.Bold);
    }

    [Fact]
    public void FormattingAlmostAllWholeRowsStaysOneRunAndUndoesInOneStep()
    {
        var selection = new CellRange(new CellAddress(1, 0), new CellAddress(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1));
        var (workbook, formatting, history) = Build(selection);
        var layout = workbook.Sheets[0].Layout;
        var italic = workbook.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Italic = true } });
        layout.SetRowStyles(0, CellAddress.MaxRows - 1, italic);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        formatting.ApplyToSelection(style => style with { Font = style.Font with { Bold = true } });

        clock.Stop();
        Assert.True(clock.ElapsedMilliseconds < 2000, $"took {clock.ElapsedMilliseconds} ms");
        Assert.Equal(2, layout.RowStyles.Runs.Count);
        var bold = workbook.Styles.Get(layout.RowStyle(CellAddress.MaxRows - 1)!.Value).Font;
        Assert.True(bold.Bold && bold.Italic);
        Assert.False(workbook.Styles.Get(layout.RowStyle(0)!.Value).Font.Bold);

        history.Undo();

        Assert.Single(layout.RowStyles.Runs);
    }

    [Fact]
    public void BoldOnWholeRowsOverThreeFormattedColumnsCreatesNoCells()
    {
        var selection = new CellRange(new CellAddress(1, 0), new CellAddress(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1));
        var (workbook, formatting, history) = Build(selection);
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Number(1));
        sheet.SetValue(A("E5"), CellValue.Number(2));
        var red = workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#FF0000" });
        var green = workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#00FF00" });
        sheet.Layout.SetColumnStyle(1, red);
        sheet.Layout.SetColumnStyle(2, red);
        sheet.Layout.SetColumnStyle(7, green);
        var cellsBefore = sheet.CellCount;
        var memoryBefore = GC.GetAllocatedBytesForCurrentThread();
        var clock = System.Diagnostics.Stopwatch.StartNew();

        formatting.ToggleOnSelection(style => style.Font.Bold, (style, on) => style with { Font = style.Font with { Bold = on } });

        clock.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - memoryBefore;
        var created = sheet.CellCount - cellsBefore;
        Console.WriteLine($"style-bands measure: cells created {created}, {clock.ElapsedMilliseconds} ms, {allocated / 1024} KiB allocated");
        Assert.True(created <= 20, $"created {created} cells in {clock.ElapsedMilliseconds} ms, {allocated / 1024} KiB");
        var deep = workbook.Styles.Get(sheet.StyleIdAt(new CellAddress(CellAddress.MaxRows - 1, 1)));
        Assert.True(deep.Font.Bold);
        Assert.Equal("#FF0000", deep.BackgroundHex);
        Assert.Equal("#00FF00", workbook.Styles.Get(sheet.StyleIdAt(new CellAddress(500_000, 7))).BackgroundHex);
        Assert.True(workbook.Styles.Get(sheet.StyleIdAt(new CellAddress(500_000, 7))).Font.Bold);
        Assert.False(workbook.Styles.Get(sheet.StyleIdAt(new CellAddress(0, 1))).Font.Bold);
        Assert.Equal("#FF0000", workbook.Styles.Get(sheet.StyleIdAt(new CellAddress(0, 1))).BackgroundHex);
        Assert.Null(sheet.OwnStyleAt(new CellAddress(500_000, 1)));

        history.Undo();

        Assert.Equal(cellsBefore, sheet.CellCount);
        Assert.False(workbook.Styles.Get(sheet.StyleIdAt(new CellAddress(500_000, 1))).Font.Bold);
        Assert.Equal(red, sheet.StyleIdAt(new CellAddress(500_000, 1)));

        history.Redo();

        Assert.True(workbook.Styles.Get(sheet.StyleIdAt(new CellAddress(500_000, 2))).Font.Bold);
        Assert.Equal(cellsBefore + created, sheet.CellCount);
    }
}
