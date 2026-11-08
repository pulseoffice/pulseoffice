using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Formats.Pdf;
using PulseOffice.Platform;
using PulseOffice.Printing;
using PulseOffice.Scripting;
using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.UI.Scripting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class LedgerFacadeContractTests
{
    private sealed class DirectDispatcher : IScriptDispatcher
    {
        public T Read<T>(Func<T> read) => read();

        public void Change(Action change) => change();

        public void Show(Action show) => show();

        public T Write<T>(Func<T> write) => write();
    }

    private sealed class Output : IScriptOutput
    {
        public List<string> Lines { get; } = [];

        public void Write(string message) => Lines.Add(message);

        public IDisposable Capture(Action<string> sink) => new Released();
    }

    private sealed class Released : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class Pages(IActiveSheetContext context) : ISheetPrintDocumentBuilder
    {
        public IPrintableDocument? Build(string documentName) => context.Workbook is { } workbook ? Build(workbook, documentName) : null;

        public IPrintableDocument? Build(Workbook workbook, string documentName) => new Printable(workbook.PageStyles.For(workbook.Sheets[0]).Setup);

        public IPrintableDocument? BuildRange(string documentName, CellRange range) => Build(documentName);
    }

    private sealed class Printable(PulseOffice.Foundation.Pages.PageSetup setup) : IPrintableDocument
    {
        public PulseOffice.Foundation.Pages.PageSetup Setup => setup;

        public int PageCount => 1;

        public void PaintPage(int index, IPageCanvas canvas) { }
    }

    private sealed class Exporter : IPdfExporter
    {
        public OperationResult<int> Write(IPrintableDocument document, Stream stream, PdfExportOptions? options = null)
        {
            stream.Write("%PDF-1.7"u8);
            return OperationResult<int>.Ok(document.PageCount);
        }
    }

    private sealed class NoPrinters : IPrintServiceProvider, IPrintService
    {
        public IPrintService Current => this;

        public PrintCapabilities Capabilities { get; } = new(false, false, false, false, true);

        public Task<IReadOnlyList<Printer>> PrintersAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Printer>>([]);

        public Task<OperationResult<bool>> PrintAsync(PrintRequest request, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<bool>.Ok(false));
    }

    private sealed class Identity : PulseOffice.Foundation.Identity.IUserIdentity
    {
        public string Name => "Test";

        public string Initials => "T";
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly ActiveSheetContext _context = new();

        public Fixture()
        {
            Workbook.AddSheet("Second");
            _provider = new ServiceCollection()
                .AddSheetsEngine()
                .AddSingleton<ISheetSnapshots, SheetSnapshots>()
                .AddSingleton<ISheetFilter, SheetFilterService>()
                .AddSingleton<IRangeFilterEvaluator, RangeFilterEvaluator>()
                .AddSingleton<ISheetRangeFilters, SheetRangeFilters>()
                .AddSingleton<IScenarioManager, ScenarioManager>()
                .AddSingleton<ISheetStructure, SheetStructure>()
                .AddSingleton<ISheetSort, SheetSorter>()
                .AddSingleton<ISheetMerge, SheetMerge>()
                .AddSingleton<ISheetRenaming, SheetRenaming>()
                .AddSingleton<IChartSheetReferences, ChartSheetReferences>()
                .AddSingleton<IWorkbookCheckpoints, WorkbookCheckpoints>()
                .AddSingleton<IDefinedNameRules, DefinedNameRules>()
                .AddSingleton<IPivotAggregator, PivotAggregator>()
                .AddSingleton<IPivotGrouping>(_ => new PivotGrouping())
                .AddSingleton<IPivotFormulas, PivotFormulas>()
                .AddSingleton<IPivotEngine, PivotEngine>()
                .AddSingleton<IPivotRenderer, PivotRenderer>()
                .AddSingleton<IPivotCoordinator, PivotCoordinator>()
                .AddSingleton<PulseOffice.Foundation.Pages.IPaperCatalog, PulseOffice.Foundation.Pages.PaperCatalog>()
                .AddSingleton<IPrintServiceProvider, NoPrinters>()
                .AddSingleton<IAtomicFileWriter, AtomicFileWriter>()
                .AddSingleton<PulseOffice.Scripting.IScriptExecutionPermissions>(Permissions)
                .AddSingleton<IPdfExporter, Exporter>()
                .AddSingleton<ISheetPrintDocumentBuilder, Pages>()
                .AddSingleton<PulseOffice.Shell.IDocumentWorkspace, PulseOffice.Shell.DocumentWorkspace>()
                .AddSingleton<IScriptSheetEdits, ScriptSheetEdits>()
                .AddSingleton<IScriptReferences, ScriptReferences>()
                .AddSingleton<IScriptColors, ScriptColors>()
                .AddSingleton<IScriptProtection, ScriptProtection>()
                .AddSingleton<PulseOffice.Sheets.Engine.Operations.IValidationPlacement, PulseOffice.Sheets.Engine.Operations.ValidationPlacement>()
                .AddSingleton<ISheetEditGuard, SheetEditGuard>()
                .AddSingleton<IWorksheetTabPolicy, WorksheetTabPolicy>()
                .AddSingleton<IScriptOutput>(Output)
                .AddSingleton<PulseOffice.Foundation.Identity.IUserIdentity>(new Identity())
                .AddSingleton<PulseOffice.Sheets.Formatting.INumberFormatter>(new PulseOffice.Sheets.Formatting.NumberFormatter(
                    System.Globalization.CultureInfo.InvariantCulture, serial => new DateTime(1899, 12, 30).AddDays(serial)))
                .AddSingleton<PulseOffice.Sheets.Formatting.ICellPresenter, PulseOffice.Sheets.Formatting.CellPresenter>()
                .AddSingleton<PulseOffice.Localization.ITextCatalog>(new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English))
                .AddSingleton<IActiveSheetContext>(_context)
                .AddSingleton<IScriptDispatcher>(new ReadOnlyGuardedDispatcher(new DirectDispatcher(), () => IsReadOnly))
                .BuildServiceProvider();

            Engine = _provider.GetRequiredService<ICalculationEngineFactory>().Create(Workbook);
            _context.Attach(new SheetTarget
            {
                Workbook = Workbook,
                SheetIndex = 0,
                History = History,
                Engine = Engine,
                Selection = () => Selected,
                WriteSelection = range => Selected = range,
                ReadViewOption = _ => false,
                WriteViewOption = (_, _) => { },
                Refresh = () => { },
                FreezePanes = (rows, columns) => Frozen = (rows, columns),
                SplitPanes = (_, _) => { },
                ReadPanesSplit = () => false,
                ApplyRowFilter = (_, rows) => HiddenRows = rows,
                ReadZoom = () => 1,
                WriteZoom = _ => { },
                ReadSelectedObject = () => null,
                ReadSelectedObjects = () => [],
                WriteSelectedObjects = _ => { },
                SelectSheet = index => SelectedSheet = index,
                WriteSelectedObject = _ => { },
            });

            Doc = Create<DocumentFacade>();
            Sheet = Create<SheetFacade>();
            Pivot = Create<PivotFacade>();
            Page = Create<PrintFacade>();
            Rules = Create<FormatRuleFacade>();
            Validation = Create<ValidationFacade>();
            Filter = Create<FilterFacade>();
            Scenarios = Create<ScenarioFacade>();
        }

        public PulseOffice.Scripting.ScriptExecutionPermissions Permissions { get; } = new();

        public Workbook Workbook { get; } = Workbook.CreateDefault();

        public Worksheet First => Workbook.Sheets[0];

        public UndoStack History { get; } = new();

        public CalculationEngine Engine { get; }

        public Output Output { get; } = new();

        public bool IsReadOnly { get; set; }

        public CellRange Selected { get; private set; } = CellRange.Single(CellAddress.First);

        public (int Rows, int Columns)? Frozen { get; private set; }

        public IReadOnlySet<int>? HiddenRows { get; private set; }

        public int? SelectedSheet { get; private set; }

        public DocumentFacade Doc { get; }

        public SheetFacade Sheet { get; }

        public PivotFacade Pivot { get; }

        public PrintFacade Page { get; }

        public FormatRuleFacade Rules { get; }

        public ValidationFacade Validation { get; }

        public FilterFacade Filter { get; }

        public ScenarioFacade Scenarios { get; }

        public string Scratch { get; } = Directory.CreateTempSubdirectory("ledger-contract-").FullName;

        public void Table()
        {
            Sheet.WriteRange("A1", [["Region", "Product", "Amount"], ["North", "Pens", 10.0], ["South", "Pens", 20.0], ["North", "Ink", 5.0]]);
            History.Clear();
        }

        public void Refused(Action write)
        {
            IsReadOnly = true;
            Assert.Equal("The document is read-only.", Assert.Throws<InvalidOperationException>(write).Message);
            IsReadOnly = false;
        }

        public void Invalid(Action use, string? contains = null)
        {
            var failure = Assert.ThrowsAny<ArgumentException>(use);
            if (contains is not null) Assert.Contains(contains, failure.Message, StringComparison.OrdinalIgnoreCase);
        }

        public void Undoable()
        {
            Assert.True(History.CanUndo);
        }

        public void Dispose()
        {
            _provider.Dispose();
            Directory.Delete(Scratch, recursive: true);
        }

        private T Create<T>() => ActivatorUtilities.CreateInstance<T>(_provider);
    }

    private static CellAddress At(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    private static string Text(Worksheet sheet, string a1) => sheet[At(a1)].Value.ToString() ?? string.Empty;

    private readonly IReadOnlyDictionary<string, Action<Fixture>> Cases = new Dictionary<string, Action<Fixture>>
    {
        ["doc.Name"] = f => Assert.Equal(string.Empty, f.Doc.Name),
        ["doc.Path"] = f => Assert.Equal(string.Empty, f.Doc.Path),
        ["doc.IsModified"] = f => Assert.False(f.Doc.IsModified),
        ["doc.SheetCount"] = f => Assert.Equal(2, f.Doc.SheetCount),
        ["doc.SheetNames"] = f => Assert.Equal(f.Workbook.Sheets.Select(sheet => sheet.Name), f.Doc.SheetNames),
        ["doc.ActiveSheet"] = f => Assert.Equal(f.First.Name, f.Doc.ActiveSheet),
        ["doc.Title"] = f =>
        {
            f.Doc.Title = "Budget";
            Assert.Equal("Budget", f.Workbook.Properties.Title);
            f.Refused(() => f.Doc.Title = "Other");
        },
        ["doc.Author"] = f =>
        {
            f.Doc.Author = "Ada";
            Assert.Equal("Ada", f.Doc.Author);
        },
        ["doc.Subject"] = f =>
        {
            f.Doc.Subject = "Plan";
            Assert.Equal("Plan", f.Workbook.Properties.Subject);
        },
        ["doc.Keywords"] = f =>
        {
            f.Doc.Keywords = "a, b";
            Assert.Equal(["a", "b"], f.Workbook.Properties.Keywords);
        },
        ["doc.AddSheet"] = f =>
        {
            Assert.True(f.Doc.AddSheet("Third"));
            Assert.Equal(3, f.Workbook.Sheets.Count);
            Assert.False(f.Doc.AddSheet("Third"));
            Assert.False(f.Doc.AddSheet("Q1:Q2"));
            Assert.Null(f.Workbook.FindSheet("Q1:Q2"));
            f.Refused(() => f.Doc.AddSheet("Fourth"));
        },
        ["doc.RemoveSheet"] = f =>
        {
            Assert.True(f.Doc.RemoveSheet("Second"));
            Assert.Single(f.Workbook.Sheets);
            Assert.False(f.Doc.RemoveSheet("Missing"));
        },
        ["doc.RenameSheet"] = f =>
        {
            Assert.True(f.Doc.RenameSheet("Second", "Data"));
            Assert.NotNull(f.Workbook.FindSheet("Data"));
            Assert.False(f.Doc.RenameSheet("Missing", "X"));
            Assert.False(f.Doc.RenameSheet("Data", "a[b]"));
            Assert.NotNull(f.Workbook.FindSheet("Data"));
        },
        ["doc.Activate"] = f =>
        {
            Assert.True(f.Doc.Activate("Second"));
            Assert.Equal(1, f.SelectedSheet);
            Assert.False(f.Doc.Activate("Missing"));
        },
        ["doc.Undo"] = f =>
        {
            Assert.False(f.Doc.Undo());
            f.Sheet.Write("A1", 1.0);
            Assert.True(f.Doc.Undo());
            Assert.True(f.Sheet.IsEmpty("A1"));
        },
        ["doc.Redo"] = f =>
        {
            f.Sheet.Write("A1", 1.0);
            f.Doc.Undo();
            Assert.True(f.Doc.Redo());
            Assert.Equal(1, f.Sheet.Number("A1"));
        },
        ["doc.Recalculate"] = f =>
        {
            f.Sheet.SetFormula("A1", "=1+1");
            f.Doc.Recalculate();
            Assert.Equal(2, f.Sheet.Number("A1"));
        },
        ["doc.Names"] = f =>
        {
            f.Doc.DefineName("Rate", "$A$1");
            Assert.Contains("Rate", f.Doc.Names);
        },
        ["doc.DefineName"] = f =>
        {
            Assert.True(f.Doc.DefineName("Rate", "$A$1:$A$3"));
            Assert.Equal("$A$1:$A$3", f.Workbook.FindName("Rate")!.Expression);
            f.Invalid(() => f.Doc.DefineName("A1", "$B$1"), "not a valid name");
            f.Invalid(() => f.Doc.DefineName("Bad name", "$B$1"), "not a valid name");
            f.Invalid(() => f.Doc.DefineName("Other", "SUM(("), "not a valid reference");
            f.Invalid(() => f.Doc.DefineName("Other", ""), "empty");
            Assert.Null(f.Workbook.FindName("Other"));
        },
        ["doc.MacroCount"] = f => Assert.Equal(f.Workbook.Macros.All.Count, f.Doc.MacroCount),

        ["sheet.Name"] = f => Assert.Equal(f.First.Name, f.Sheet.Name),
        ["sheet.Index"] = f => Assert.Equal(0, f.Sheet.Index),
        ["sheet.Selection"] = f =>
        {
            f.Sheet.Select("B2:C3");
            Assert.Equal("B2:C3", f.Sheet.Selection);
        },
        ["sheet.UsedRange"] = f =>
        {
            f.Sheet.Write("C4", 1.0);
            Assert.Equal("C4", f.Sheet.UsedRange.Replace("$", string.Empty));
        },
        ["sheet.ObjectCount"] = f =>
        {
            f.Sheet.AddShape("Rectangle", "B2");
            Assert.Equal(1, f.Sheet.ObjectCount);
        },
        ["sheet.Read"] = f =>
        {
            f.Sheet.Write("A1", "text");
            Assert.Equal("text", f.Sheet.Read("A1"));
            f.Invalid(() => f.Sheet.Read("pippo"), "not a cell reference");
        },
        ["sheet.Number"] = f =>
        {
            f.Sheet.Write("A1", 2.5);
            Assert.Equal(2.5, f.Sheet.Number("A1"));
            f.Invalid(() => f.Sheet.Number("A0"));
        },
        ["sheet.Formula"] = f =>
        {
            f.Sheet.SetFormula("A1", "1+2");
            Assert.Equal("=1+2", f.Sheet.Formula("A1"));
            f.Invalid(() => f.Sheet.Formula(""));
        },
        ["sheet.IsEmpty"] = f =>
        {
            Assert.True(f.Sheet.IsEmpty("A1"));
            f.Sheet.Write("A1", 1.0);
            Assert.False(f.Sheet.IsEmpty("A1"));
            f.Invalid(() => f.Sheet.IsEmpty("1A"));
        },
        ["sheet.Write"] = f =>
        {
            f.Sheet.Write("B2", 3.0);
            Assert.Equal("3", Text(f.First, "B2"));
            f.Undoable();
            f.Invalid(() => f.Sheet.Write("pippo", 1.0));
            f.Invalid(() => f.Sheet.Write("B4", "=1+"), "not a valid formula");
            f.Refused(() => f.Sheet.Write("B3", 1.0));
        },
        ["sheet.SetFormula"] = f =>
        {
            f.Sheet.SetFormula("A1", "=2*3");
            Assert.Equal(6, f.Sheet.Number("A1"));
            f.Invalid(() => f.Sheet.SetFormula("A2", "="), "empty");
            f.Invalid(() => f.Sheet.SetFormula("A2", "=SUM(("), "not a valid formula");
            Assert.True(f.Sheet.IsEmpty("A2"));
            f.Invalid(() => f.Sheet.SetFormula("zz", "=1"));
        },
        ["sheet.FormulaLocal"] = f =>
        {
            f.Sheet.SetFormula("A1", "=SUM(1;2)");
            Assert.Equal("=SUM(1,2)", f.Sheet.FormulaLocal("A1"));
            f.Invalid(() => f.Sheet.FormulaLocal("?"));
        },
        ["sheet.SetFormulaLocal"] = f =>
        {
            f.Sheet.SetFormulaLocal("A1", "=SUM(1,2)");
            Assert.Equal("=SUM(1;2)", f.Sheet.Formula("A1"));
            f.Invalid(() => f.Sheet.SetFormulaLocal("?", "=1"));
        },
        ["sheet.Range"] = f =>
        {
            f.Table();
            Assert.Equal("Region", f.Sheet.Range("A1:C2")[0][0]);
            f.Invalid(() => f.Sheet.Range("A1:"));
        },
        ["sheet.Numbers"] = f =>
        {
            f.Table();
            Assert.Equal(10, f.Sheet.Numbers("C2:C3")[0][0]);
            f.Invalid(() => f.Sheet.Numbers("C:"));
        },
        ["sheet.WriteRange"] = f =>
        {
            f.Sheet.WriteRange("B2", [[1.0, 2.0], [3.0, 4.0]]);
            Assert.Equal(4, f.Sheet.Number("C3"));
            f.Invalid(() => f.Sheet.WriteRange("B2", []), "no values");
            f.Invalid(() => f.Sheet.WriteRange("E5", [[1.0, "=SUM(("]]), "not a valid formula");
            Assert.True(f.Sheet.IsEmpty("E5"));
            f.Invalid(() => f.Sheet.WriteRange("nope", [[1.0]]));
        },
        ["sheet.Clear"] = f =>
        {
            f.Table();
            f.Sheet.Clear("A1:C4");
            Assert.True(f.Sheet.IsEmpty("C2"));
            f.Invalid(() => f.Sheet.Clear("x"));
        },
        ["sheet.Select"] = f =>
        {
            f.Sheet.Select("D4");
            Assert.Equal(At("D4"), f.Selected.Start);
            f.Invalid(() => f.Sheet.Select("D"));
        },
        ["sheet.Recalculate"] = f =>
        {
            f.Sheet.SetFormula("A1", "=3");
            f.Sheet.Recalculate();
            Assert.Equal(3, f.Sheet.Number("A1"));
        },
        ["sheet.Log"] = f =>
        {
            f.Sheet.Log("hello");
            Assert.Contains("hello", f.Output.Lines);
        },
        ["sheet.Bold"] = f =>
        {
            f.Sheet.Bold("A1", true);
            Assert.True(f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).Font.Bold);
            f.Invalid(() => f.Sheet.Bold("A1:", true));
        },
        ["sheet.Italic"] = f =>
        {
            f.Sheet.Italic("A1", true);
            Assert.True(f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).Font.Italic);
            f.Invalid(() => f.Sheet.Italic("", true));
        },
        ["sheet.Underline"] = f =>
        {
            f.Sheet.Underline("A1", true);
            Assert.True(f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).Font.Underline);
            f.Invalid(() => f.Sheet.Underline("?", true));
        },
        ["sheet.FontSize"] = f =>
        {
            f.Sheet.FontSize("A1", 18);
            Assert.Equal(18, f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).Font.Size);
            f.Invalid(() => f.Sheet.FontSize("A1", 0), "font size");
            f.Invalid(() => f.Sheet.FontSize("A1", double.NaN), "font size");
        },
        ["sheet.FontName"] = f =>
        {
            f.Sheet.FontName("A1", "Arial");
            Assert.Equal("Arial", f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).Font.Family);
            f.Invalid(() => f.Sheet.FontName("A1", " "), "font family");
        },
        ["sheet.Color"] = f =>
        {
            f.Sheet.Color("A1", "#ff0000");
            Assert.Equal("#FF0000", f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).Font.ColorHex);
            f.Invalid(() => f.Sheet.Color("A1", "red"), "#RRGGBB");
            f.Invalid(() => f.Sheet.Color("A1", "#12345"), "#RRGGBB");
        },
        ["sheet.Background"] = f =>
        {
            f.Sheet.Background("A1", "#00FF00");
            Assert.Equal("#00FF00", f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).BackgroundHex);
            f.Sheet.Background("A1", null);
            Assert.Null(f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).BackgroundHex);
            f.Invalid(() => f.Sheet.Background("A1", "green"), "#RRGGBB");
        },
        ["sheet.Align"] = f =>
        {
            f.Sheet.Align("A1", "right");
            Assert.Equal(PulseOffice.Sheets.Styling.CellHorizontalAlignment.Right, f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).Horizontal);
            f.Invalid(() => f.Sheet.Align("A1", "middle"), "alignment");
        },
        ["sheet.NumberFormat"] = f =>
        {
            f.Sheet.NumberFormat("A1", 10);
            Assert.Equal(10, f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).NumberFormatId);
            f.Invalid(() => f.Sheet.NumberFormat("A1", 999), "number format");
        },
        ["sheet.Wrap"] = f =>
        {
            f.Sheet.Wrap("A1", true);
            Assert.True(f.Workbook.Styles.Get(f.First[CellAddress.First].StyleId).WrapText);
            f.Invalid(() => f.Sheet.Wrap("A", true));
        },
        ["sheet.StyleName"] = f =>
        {
            var style = f.Workbook.NamedStyles.All.Last().Name;
            f.Sheet.Write("A1", 1.0);
            f.Sheet.StyleName("A1", style);
            Assert.Equal(style, f.First[CellAddress.First].StyleName);
            f.Invalid(() => f.Sheet.StyleName("A1", "No such style"), "no cell style");
        },
        ["sheet.InsertRows"] = f =>
        {
            f.Sheet.Write("A1", 1.0);
            f.Sheet.InsertRows(1, 2);
            Assert.Equal(1, f.Sheet.Number("A3"));
            f.Invalid(() => f.Sheet.InsertRows(1, 0), "count");
            f.Invalid(() => f.Sheet.InsertRows(0, 1), "rows go");
        },
        ["sheet.DeleteRows"] = f =>
        {
            f.Sheet.Write("A3", 1.0);
            f.Sheet.DeleteRows(1, 2);
            Assert.Equal(1, f.Sheet.Number("A1"));
            f.Invalid(() => f.Sheet.DeleteRows(1, -1), "count");
        },
        ["sheet.InsertColumns"] = f =>
        {
            f.Sheet.Write("A1", 1.0);
            f.Sheet.InsertColumns(1, 1);
            Assert.Equal(1, f.Sheet.Number("B1"));
            f.Invalid(() => f.Sheet.InsertColumns(0, 1), "columns go");
        },
        ["sheet.DeleteColumns"] = f =>
        {
            f.Sheet.Write("B1", 1.0);
            f.Sheet.DeleteColumns(1, 1);
            Assert.Equal(1, f.Sheet.Number("A1"));
            f.Invalid(() => f.Sheet.DeleteColumns(1, 0), "count");
        },
        ["sheet.RowHeight"] = f =>
        {
            f.Sheet.RowHeight(2, 10);
            Assert.Equal(PulseOffice.Foundation.Units.Twip.FromMillimeters(10), f.First.Layout.RowHeight(1));
            f.Invalid(() => f.Sheet.RowHeight(2, -1));
        },
        ["sheet.ColumnWidth"] = f =>
        {
            f.Sheet.ColumnWidth(2, 30);
            Assert.Equal(PulseOffice.Foundation.Units.Twip.FromMillimeters(30), f.First.Layout.ColumnWidth(1));
            f.Invalid(() => f.Sheet.ColumnWidth(0, 30));
        },
        ["sheet.HideRows"] = f =>
        {
            f.Sheet.HideRows(2, 2);
            Assert.True(f.First.Layout.IsRowHidden(2));
            f.Invalid(() => f.Sheet.HideRows(2, 0), "count");
        },
        ["sheet.ShowRows"] = f =>
        {
            f.Sheet.HideRows(2, 1);
            f.Sheet.ShowRows(2, 1);
            Assert.False(f.First.Layout.IsRowHidden(1));
            f.Invalid(() => f.Sheet.ShowRows(0, 1));
        },
        ["sheet.Freeze"] = f =>
        {
            f.Sheet.Freeze(1, 2);
            Assert.Equal((1, 2), f.Frozen);
            f.Invalid(() => f.Sheet.Freeze(-1, 0), "frozen rows");
        },
        ["sheet.Merge"] = f =>
        {
            Assert.True(f.Sheet.Merge("A1:B2"));
            Assert.NotEmpty(f.First.Merges.All);
            f.Invalid(() => f.Sheet.Merge("A1:"));
        },
        ["sheet.Unmerge"] = f =>
        {
            f.Sheet.Merge("A1:B2");
            Assert.True(f.Sheet.Unmerge("A1:B2"));
            Assert.Empty(f.First.Merges.All);
            f.Invalid(() => f.Sheet.Unmerge("?"));
        },
        ["sheet.Sort"] = f =>
        {
            f.Table();
            f.Sheet.Sort("A1:C4", 3, ascending: false, hasHeader: true);
            Assert.Equal(20, f.Sheet.Number("C2"));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Sheet.Sort("A1:C4", 4));
        },
        ["sheet.DistinctValues"] = f =>
        {
            f.Table();
            Assert.Equal(["North", "South"], f.Sheet.DistinctValues("A1:C4", 1, true).Order());
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Sheet.DistinctValues("A1:C4", 9, true));
        },
        ["sheet.Find"] = f =>
        {
            f.Table();
            Assert.Equal(["A2", "A4"], f.Sheet.Find("north"));
        },
        ["sheet.Replace"] = f =>
        {
            f.Table();
            Assert.Equal(2, f.Sheet.Replace("North", "East"));
            Assert.Equal("East", f.Sheet.Read("A2"));
        },
        ["sheet.Comment"] = f =>
        {
            f.Sheet.Comment("B2", "check");
            Assert.Equal("check", f.First.Comments[At("B2")].Text);
            f.Invalid(() => f.Sheet.Comment("B", "x"));
        },
        ["sheet.AddChart"] = f =>
        {
            f.Table();
            Assert.NotEmpty(f.Sheet.AddChart("A1:C4", "Bar", "E2"));
            Assert.Single(f.First.Objects);
            f.Invalid(() => f.Sheet.AddChart("A1:C4", "Banana", "E2"), "unknown kind");
            f.Invalid(() => f.Sheet.AddChart("A1:C4", "Bar", "E"));
            Assert.Single(f.First.Objects);
        },
        ["sheet.AddShape"] = f =>
        {
            Assert.NotEmpty(f.Sheet.AddShape("Ellipse", "B2", 30, 20));
            Assert.Single(f.First.Objects);
            f.Invalid(() => f.Sheet.AddShape("Blob", "B2"), "unknown kind");
            f.Invalid(() => f.Sheet.AddShape("Ellipse", "B2", 0, 20));
            Assert.Single(f.First.Objects);
        },
        ["sheet.RemoveObject"] = f =>
        {
            var id = f.Sheet.AddShape("Rectangle", "B2");
            Assert.True(f.Sheet.RemoveObject(id));
            Assert.Empty(f.First.Objects);
            Assert.False(f.Sheet.RemoveObject("missing"));
        },

        ["pivot.Names"] = f =>
        {
            f.Table();
            f.Pivot.Create("Sales", "A1:C4", "F1", ["Region"], [], ["Amount"]);
            Assert.Equal(["Sales"], f.Pivot.Names);
        },
        ["pivot.Count"] = f =>
        {
            f.Table();
            f.Pivot.Create("Sales", "A1:C4", "F1", ["Region"], [], ["Amount"]);
            Assert.Equal(1, f.Pivot.Count);
        },
        ["pivot.Create"] = f =>
        {
            f.Table();
            Assert.NotEmpty(f.Pivot.Create("Sales", "A1:C4", "F1", ["Region"], [], ["Amount"]));
            Assert.Single(f.Workbook.Pivots);
            f.Invalid(() => f.Pivot.Create("Other", "A1:C4", "J1", ["Regione"], [], ["Amount"]), "'Regione'");
            f.Invalid(() => f.Pivot.Create("Other", "A1:C4", "J1", ["Region"], [], []), "value field");
            f.Invalid(() => f.Pivot.Create("Other", "A1:C", "J1", ["Region"], [], ["Amount"]));
            f.Invalid(() => f.Pivot.Create("Sales", "A1:C4", "J1", ["Region"], [], ["Amount"]), "already");
            f.Invalid(() => f.Pivot.Create("Other", "A1:C4", "J1", ["Region"], [], ["Amount"], "Banana"), "aggregation");
            Assert.Throws<InvalidOperationException>(() => f.Pivot.Create("Other", "A1:C4", "B2", ["Region"], [], ["Amount"]));
            Assert.Single(f.Workbook.Pivots);
        },
        ["pivot.Refresh"] = f =>
        {
            f.Table();
            f.Pivot.Create("Sales", "A1:C4", "F1", ["Region"], [], ["Amount"]);
            f.Sheet.Write("C2", 100.0);
            Assert.Equal(1, f.Pivot.Refresh());
        },
        ["pivot.Remove"] = f =>
        {
            f.Table();
            f.Pivot.Create("Sales", "A1:C4", "F1", ["Region"], [], ["Amount"]);
            Assert.True(f.Pivot.Remove("Sales"));
            Assert.Empty(f.Workbook.Pivots);
            Assert.False(f.Pivot.Remove("Sales"));
        },

        ["page.Papers"] = f => Assert.Contains("a4", f.Page.Papers),
        ["page.Paper"] = f =>
        {
            f.Page.Paper = "A3";
            Assert.Equal("a3", f.Page.Paper);
            f.Invalid(() => f.Page.Paper = "Napkin", "unknown paper");
            Assert.Equal("a3", f.Page.Paper);
        },
        ["page.Orientation"] = f =>
        {
            f.Page.Orientation = "Landscape";
            Assert.Equal("Landscape", f.Page.Orientation);
            f.Invalid(() => f.Page.Orientation = "Sideways", "orientation");
            Assert.Equal("Landscape", f.Page.Orientation);
        },
        ["page.Margins"] = f =>
        {
            f.Page.Margins(15);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Page.Margins(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Page.Margins(1e9));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Page.Margins(150));
            Assert.Equal(1, f.Page.PageCount);
            f.Undoable();
        },
        ["page.Header"] = f =>
        {
            f.Page.Header("L", "C", "R");
            Assert.Equal("C", f.Workbook.PageStyles.For(f.First).Setup.Header.Center);
        },
        ["page.Footer"] = f =>
        {
            f.Page.Footer("L", "C", "R");
            Assert.Equal("R", f.Workbook.PageStyles.For(f.First).Setup.Footer.Right);
        },
        ["page.FirstHeader"] = f =>
        {
            f.Page.FirstHeader("L", "First", "R");
            var setup = f.Workbook.PageStyles.For(f.First).Setup;
            Assert.Equal("First", setup.FirstHeader.Center);
            Assert.True(setup.DifferentFirstPage);
            f.Undoable();
        },
        ["page.FirstFooter"] = f =>
        {
            f.Page.FirstFooter("L", "C", "Last");
            Assert.Equal("Last", f.Workbook.PageStyles.For(f.First).Setup.FirstFooter.Right);
            Assert.True(f.Page.DifferentFirstPage);
        },
        ["page.EvenHeader"] = f =>
        {
            f.Page.EvenHeader("Even", "", "");
            Assert.Equal("Even", f.Workbook.PageStyles.For(f.First).Setup.EvenHeader.Left);
            Assert.True(f.Page.DifferentOddEven);
        },
        ["page.EvenFooter"] = f =>
        {
            f.Page.EvenFooter("", "Even", "");
            Assert.Equal("Even", f.Workbook.PageStyles.For(f.First).Setup.EvenFooter.Center);
        },
        ["page.DifferentFirstPage"] = f =>
        {
            f.Page.DifferentFirstPage = true;
            Assert.True(f.Page.DifferentFirstPage);
            f.Page.DifferentFirstPage = false;
            Assert.False(f.Workbook.PageStyles.For(f.First).Setup.DifferentFirstPage);
        },
        ["page.DifferentOddEven"] = f =>
        {
            f.Page.DifferentOddEven = true;
            Assert.True(f.Workbook.PageStyles.For(f.First).Setup.DifferentOddEven);
        },
        ["page.FirstPageNumber"] = f =>
        {
            Assert.Equal(0, f.Page.FirstPageNumber);
            f.Page.FirstPageNumber = 5;
            Assert.Equal(5, f.Workbook.PageStyles.For(f.First).Setup.FirstPageNumber);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Page.FirstPageNumber = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Page.FirstPageNumber = 40000);
            f.Page.FirstPageNumber = 0;
            Assert.Null(f.Workbook.PageStyles.For(f.First).Setup.FirstPageNumber);
        },
        ["page.GridLines"] = f =>
        {
            f.Page.GridLines = true;
            Assert.True(f.Page.GridLines);
        },
        ["page.Headings"] = f =>
        {
            f.Page.Headings = true;
            Assert.True(f.Page.Headings);
        },
        ["page.PrintArea"] = f =>
        {
            f.Page.PrintArea("A1:C4");
            Assert.Single(f.First.PrintRanges);
            f.Invalid(() => f.Page.PrintArea("A1:"));
        },
        ["page.RepeatRows"] = f =>
        {
            f.Page.RepeatRows(2);
            Assert.Equal(2, f.First.RepeatedPrintRows);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Page.RepeatRows(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Page.RepeatRows(101));
            Assert.Equal(2, f.First.RepeatedPrintRows);
        },
        ["page.PageCount"] = f => Assert.Equal(1, f.Page.PageCount),
        ["page.Printers"] = f => Assert.Empty(f.Page.Printers),
        ["page.ExportPdf"] = f =>
        {
            var path = Path.Combine(f.Scratch, "out.pdf");
            Assert.Throws<PulseOffice.Scripting.ScriptPermissionException>(() => f.Page.ExportPdf(path));
            Assert.False(File.Exists(path));
            using var trusted = f.Permissions.Enter(PulseOffice.Scripting.ScriptPermissions.Trusted);
            Assert.True(f.Page.ExportPdf(path));
            Assert.True(File.Exists(path));
            Assert.Throws<InvalidOperationException>(() => f.Page.ExportPdf(path));
            f.Invalid(() => f.Page.ExportPdf(Path.Combine(f.Scratch, "out.txt")), ".pdf");
            f.Invalid(() => f.Page.ExportPdf(" "), "empty");
        },

        ["rules.Count"] = f =>
        {
            f.Rules.AddDataBar("A1:A4");
            Assert.Equal(1, f.Rules.Count);
        },
        ["rules.Describe"] = f =>
        {
            f.Rules.AddDataBar("A1:A4");
            Assert.Single(f.Rules.Describe());
        },
        ["rules.AddCondition"] = f =>
        {
            Assert.True(f.Rules.AddCondition("A1:A4", ">", "3", "#FFEB84"));
            Assert.Single(f.First.ConditionalRules);
            f.Invalid(() => f.Rules.AddCondition("A1:A4", ">", "3", "yellow"), "#RRGGBB");
            f.Invalid(() => f.Rules.AddCondition("A1:", ">", "3"));
            f.Invalid(() => f.Rules.AddCondition("A1:A4", "biggest", "3"), "comparison");
            Assert.Single(f.First.ConditionalRules);
        },
        ["rules.AddDataBar"] = f =>
        {
            Assert.True(f.Rules.AddDataBar("A1:A4", "#5B9BD5"));
            Assert.Equal("#5B9BD5", f.First.ConditionalRules[0].BarColorHex);
            f.Invalid(() => f.Rules.AddDataBar("A1:A4", "blue"), "#RRGGBB");
        },
        ["rules.AddColorScale"] = f =>
        {
            Assert.True(f.Rules.AddColorScale("A1:A4", ["#FFFFFF", "#FF0000"]));
            Assert.Equal(["#FFFFFF", "#FF0000"], f.First.ConditionalRules[0].ScaleColors);
            f.Invalid(() => f.Rules.AddColorScale("A1:A4", ["#FFFFFF"]), "2 or 3");
            f.Invalid(() => f.Rules.AddColorScale("A1:A4", ["#FFFFFF", "rosso"]), "#RRGGBB");
            Assert.Single(f.First.ConditionalRules);
        },
        ["rules.AddFormula"] = f =>
        {
            Assert.True(f.Rules.AddFormula("A1:A4", "A1>2", "#FF0000"));
            Assert.Equal("=A1>2", f.First.ConditionalRules[0].Formula);
            f.Invalid(() => f.Rules.AddFormula("A1:A4", ""), "empty");
        },
        ["rules.Clear"] = f =>
        {
            f.Rules.AddDataBar("A1:A4");
            f.Rules.AddDataBar("D1:D4");
            Assert.Equal(1, f.Rules.Clear("A1"));
            Assert.Single(f.First.ConditionalRules);
            f.Invalid(() => f.Rules.Clear("zz"));
            Assert.Single(f.First.ConditionalRules);
        },

        ["validation.Count"] = f =>
        {
            f.Validation.AddWholeNumber("A1:A3", 1, 10);
            Assert.Equal(1, f.Validation.Count);
        },
        ["validation.Describe"] = f =>
        {
            f.Validation.AddWholeNumber("A1:A3", 1, 10);
            Assert.Contains("WholeNumber", Assert.Single(f.Validation.Describe()));
        },
        ["validation.AddList"] = f =>
        {
            Assert.True(f.Validation.AddList("A1:A3", ["a", "b"]));
            Assert.Equal(["a", "b"], f.First.Validations[0].ListValues);
            Assert.Equal(["A1:A3 List a;b"], f.Validation.Describe());
            f.Invalid(() => f.Validation.AddList("A1:", ["a"]));
            f.Invalid(() => f.Validation.AddList("A1:A3", ["a"], true, "Panic"), "alert");
            Assert.Equal(["a", "b"], f.First.Validations[0].ListValues);
        },
        ["validation.AddWholeNumber"] = f =>
        {
            Assert.True(f.Validation.AddWholeNumber("A1:A3", 1, 10, "Warning"));
            Assert.Equal(ValidationAlert.Warning, f.First.Validations[0].Alert);
            f.Invalid(() => f.Validation.AddWholeNumber("A1:A3", 10, 1), "greater than");
            f.Invalid(() => f.Validation.AddWholeNumber("A1:A3", double.NaN, 1));
            Assert.Single(f.First.Validations);
        },
        ["validation.AddDecimal"] = f =>
        {
            Assert.True(f.Validation.AddDecimal("B1", 0.5, 1.5));
            Assert.Equal(ValidationKind.Decimal, f.First.Validations[0].Kind);
            f.Invalid(() => f.Validation.AddDecimal("B1", 2, 1), "greater than");
        },
        ["validation.AddTextLength"] = f =>
        {
            Assert.True(f.Validation.AddTextLength("B1", 1, 5));
            Assert.Equal(ValidationKind.TextLength, f.First.Validations[0].Kind);
            f.Invalid(() => f.Validation.AddTextLength("B", 1, 5));
        },
        ["validation.AddDate"] = f =>
        {
            Assert.True(f.Validation.AddDate("B1", "2026-01-01", "2026-12-31"));
            Assert.Equal(ValidationKind.Date, f.First.Validations[0].Kind);
            f.Invalid(() => f.Validation.AddDate("?", "2026-01-01", "2026-12-31"));
        },
        ["validation.AddFormula"] = f =>
        {
            Assert.True(f.Validation.AddFormula("B1", "B1>0"));
            Assert.Equal("=B1>0", f.First.Validations[0].FirstFormula);
            f.Invalid(() => f.Validation.AddFormula("B1", " "), "empty");
        },
        ["validation.Message"] = f =>
        {
            f.Validation.AddWholeNumber("A1:A3", 1, 10);
            Assert.True(f.Validation.Message("A1:A3", "Hint", "1 to 10"));
            Assert.Equal("Hint", f.First.Validations[0].InputTitle);
            f.Invalid(() => f.Validation.Message("B1", "Hint", "x"), "no validation");
            f.Invalid(() => f.Validation.Message("B", "Hint", "x"));
        },
        ["validation.Clear"] = f =>
        {
            f.Validation.AddWholeNumber("A1:A3", 1, 10);
            f.Invalid(() => f.Validation.Clear("zz"));
            Assert.Equal(1, f.Validation.Clear("A2"));
            Assert.Equal(["A1", "A3"], f.First.Validations.Select(rule => rule.Range.ToString()));
        },

        ["filter.IsActive"] = f =>
        {
            f.Table();
            f.Filter.Activate("A1:C4");
            Assert.True(f.Filter.IsActive);
        },
        ["filter.Range"] = f =>
        {
            f.Table();
            f.Filter.Activate("A1:C4");
            Assert.Equal("A1:C4", f.Filter.Range.Replace("$", string.Empty));
        },
        ["filter.HiddenRows"] = f =>
        {
            f.Table();
            f.Filter.Activate("A1:C4");
            f.Filter.ByValues(1, ["North"]);
            Assert.Equal(1, f.Filter.HiddenRows);
        },
        ["filter.Activate"] = f =>
        {
            f.Table();
            Assert.True(f.Filter.Activate("A1:C4"));
            Assert.True(f.First.AutoFilter.IsActive);
            f.Invalid(() => f.Filter.Activate("A1:"));
        },
        ["filter.Deactivate"] = f =>
        {
            f.Table();
            f.Filter.Activate("A1:C4");
            f.Filter.Deactivate();
            Assert.False(f.First.AutoFilter.IsActive);
        },
        ["filter.ByValues"] = f =>
        {
            f.Table();
            Assert.Throws<InvalidOperationException>(() => f.Filter.ByValues(1, ["North"]));
            f.Filter.Activate("A1:C4");
            Assert.Equal(1, f.Filter.ByValues(1, ["North"]));
            Assert.Single(f.HiddenRows!);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Filter.ByValues(4, ["North"]));
        },
        ["filter.ByCondition"] = f =>
        {
            f.Table();
            Assert.Throws<InvalidOperationException>(() => f.Filter.ByCondition(3, ">8"));
            f.Filter.Activate("A1:C4");
            Assert.Equal(1, f.Filter.ByCondition(3, ">8"));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Filter.ByCondition(0, ">8"));
        },
        ["filter.ByConditions"] = f =>
        {
            f.Table();
            f.Filter.Activate("A1:C4");
            Assert.Equal(2, f.Filter.ByConditions(3, ">8", "<15", "and"));
            Assert.Equal(1, f.Filter.ByConditions(3, ">8", "<0", "or"));
            Assert.Throws<ArgumentException>(() => f.Filter.ByConditions(3, ">8", "<0", "xor"));
        },
        ["filter.ClearColumn"] = f =>
        {
            f.Table();
            f.Filter.Activate("A1:C4");
            f.Filter.ByValues(1, ["North"]);
            Assert.Equal(0, f.Filter.ClearColumn(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Filter.ClearColumn(5));
        },
        ["filter.ClearAll"] = f =>
        {
            f.Table();
            f.Filter.Activate("A1:C4");
            f.Filter.ByValues(1, ["North"]);
            f.Filter.ClearAll();
            Assert.Equal(0, f.Filter.HiddenRows);
        },
        ["filter.Values"] = f =>
        {
            f.Table();
            Assert.Throws<InvalidOperationException>(() => f.Filter.Values(1));
            f.Filter.Activate("A1:C4");
            Assert.Equal(["Ink", "Pens"], f.Filter.Values(2).Order());
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Filter.Values(7));
        },

        ["scenarios.Names"] = f =>
        {
            f.Table();
            f.Scenarios.Capture("Base", "C2:C4");
            Assert.Equal(["Base"], f.Scenarios.Names);
        },
        ["scenarios.Count"] = f =>
        {
            f.Table();
            f.Scenarios.Capture("Base", "C2:C4");
            Assert.Equal(1, f.Scenarios.Count);
        },
        ["scenarios.Capture"] = f =>
        {
            f.Table();
            Assert.True(f.Scenarios.Capture("Base", "C2:C4"));
            Assert.Single(f.First.Scenarios);
            f.Invalid(() => f.Scenarios.Capture("Other", "C2:"));
            f.Invalid(() => f.Scenarios.Capture(" ", "C2:C4"), "empty");
            Assert.Single(f.First.Scenarios);
        },
        ["scenarios.Apply"] = f =>
        {
            f.Table();
            f.Scenarios.Capture("Base", "C2:C4");
            f.Sheet.Write("C2", 99.0);
            Assert.True(f.Scenarios.Apply("Base"));
            Assert.Equal(10, f.Sheet.Number("C2"));
            f.Invalid(() => f.Scenarios.Apply("Missing"), "no scenario");
        },
        ["scenarios.Remove"] = f =>
        {
            f.Table();
            f.Scenarios.Capture("Base", "C2:C4");
            Assert.True(f.Scenarios.Remove("Base"));
            Assert.Empty(f.First.Scenarios);
            Assert.False(f.Scenarios.Remove("Base"));
        },
        ["scenarios.RangeOf"] = f =>
        {
            f.Table();
            f.Scenarios.Capture("Base", "C2:C4");
            Assert.Equal("C2:C4", f.Scenarios.RangeOf("Base").Replace("$", string.Empty));
            f.Invalid(() => f.Scenarios.RangeOf("Missing"), "no scenario");
        },
    };

    [Fact]
    public void Le_macro_e_l_agente_rispettano_la_protezione_del_foglio_e_della_struttura()
    {
        using var f = new Fixture();
        f.Table();
        f.First.SetStyle(At("E2"), f.Workbook.Styles.Intern(PulseOffice.Sheets.Styling.CellStyle.Default with { Locked = false }));
        f.First.Protection = new SheetProtection { IsEnabled = true, AllowSort = false };
        f.Workbook.Protection = new WorkbookProtection { Structure = true };

        Assert.Contains("protected", Assert.Throws<InvalidOperationException>(() => f.Sheet.Write("A2", "x")).Message);
        Assert.Throws<InvalidOperationException>(() => f.Sheet.WriteRange("A5", [["x", "y"]]));
        Assert.Throws<InvalidOperationException>(() => f.Sheet.Clear("A1:C4"));
        Assert.Throws<InvalidOperationException>(() => f.Sheet.Bold("E2", true));
        Assert.Throws<InvalidOperationException>(() => f.Sheet.InsertRows(2));
        Assert.Throws<InvalidOperationException>(() => f.Sheet.Sort("A1:C4", 1, hasHeader: true));
        Assert.Throws<InvalidOperationException>(() => f.Filter.Activate("A1:C4"));
        Assert.Throws<InvalidOperationException>(() => f.Rules.AddDataBar("C2:C4"));
        Assert.Throws<InvalidOperationException>(() => f.Validation.AddWholeNumber("C2:C4", 0, 9));
        Assert.Throws<InvalidOperationException>(() => f.Pivot.Create("P", "A1:C4", "H1", ["Region"], [], ["Amount"]));
        Assert.Contains("structure", Assert.Throws<InvalidOperationException>(() => f.Doc.AddSheet("Nuovo")).Message);
        Assert.Throws<InvalidOperationException>(() => f.Doc.RemoveSheet("Second"));
        Assert.Throws<InvalidOperationException>(() => f.Doc.RenameSheet("Second", "Altro"));
        Assert.Equal(0, f.Sheet.Replace("North", "East"));
        Assert.Equal("North", f.Sheet.Read("A2"));
        Assert.False(f.History.CanUndo);

        f.Sheet.Write("E2", 7.0);
        Assert.Equal(7, f.Sheet.Number("E2"));
    }

    [Fact]
    public void Ogni_membro_del_catalogo_di_Ledger_ha_il_suo_caso()
    {
        var members = new SheetScriptApi().Roots
            .SelectMany(root => root.Members.Select(member => $"{root.Name}.{member.Member}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(members, Cases.Keys.Order(StringComparer.Ordinal));
    }

    public static TheoryData<string> Members() =>
        [.. new SheetScriptApi().Roots.SelectMany(root => root.Members.Select(member => $"{root.Name}.{member.Member}"))];

    [Theory]
    [MemberData(nameof(Members))]
    public void Ogni_membro_di_Ledger_rifiuta_gli_argomenti_sbagliati_e_ha_un_effetto_con_quelli_giusti(string member)
    {
        using var fixture = new Fixture();

        Cases[member](fixture);
    }

    [Fact]
    public void Annullare_la_pulizia_delle_regole_ne_ripristina_l_ordine()
    {
        using var f = new Fixture();
        f.Rules.AddDataBar("A1:A4");
        f.Rules.AddDataBar("C1:C4");
        f.Rules.AddColorScale("A1:A4");
        var before = f.First.ConditionalRules.ToList();

        Assert.Equal(2, f.Rules.Clear("A1"));
        f.History.Undo();

        Assert.Equal(before, f.First.ConditionalRules);
        f.History.Redo();
        Assert.Equal(before[1], Assert.Single(f.First.ConditionalRules));
    }

    [Fact]
    public void Un_foglio_si_rinomina_anche_solo_nelle_maiuscole()
    {
        using var f = new Fixture();
        var name = f.First.Name;

        Assert.True(f.Doc.RenameSheet(name, name.ToUpperInvariant()));
        Assert.Equal(name.ToUpperInvariant(), f.First.Name);
        Assert.False(f.Doc.RenameSheet(f.First.Name, f.First.Name));
    }

    [Fact]
    public void Il_formato_di_una_cella_vuota_parte_dallo_stile_della_colonna()
    {
        using var f = new Fixture();
        f.First.Layout.SetColumnStyle(1, f.Workbook.Styles.Derive(0, style => style with { BackgroundHex = "#FFFF00" }));

        f.Sheet.Bold("B3");

        var style = f.Workbook.Styles.Get(f.First.StyleIdAt(new CellAddress(2, 1)));
        Assert.True(style.Font.Bold);
        Assert.Equal("#FFFF00", style.BackgroundHex);
    }
}
