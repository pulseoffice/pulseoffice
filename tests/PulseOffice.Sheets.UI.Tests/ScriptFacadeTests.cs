using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Scripting;
using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI.Scripting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class ScriptFacadeTests
{
    private readonly ServiceProvider _provider;

    private IServiceCollection Services() => new ServiceCollection()
        .AddSheetsEngine()
        .AddSingleton<Engine.Operations.ISheetSnapshots, Engine.Operations.SheetSnapshots>()
        .AddSingleton<Engine.Operations.ISheetFilter, Engine.Operations.SheetFilterService>()
        .AddSingleton<Engine.Operations.IRangeFilterEvaluator, Engine.Operations.RangeFilterEvaluator>()
        .AddSingleton<ISheetRangeFilters, SheetRangeFilters>()
        .AddSingleton<Engine.Operations.IScenarioManager, Engine.Operations.ScenarioManager>()
        .AddSingleton<Engine.Operations.ISheetStructure, Engine.Operations.SheetStructure>()
        .AddSingleton<Engine.Operations.ISheetSort, Engine.Operations.SheetSorter>()
        .AddSingleton<Engine.Operations.ISheetMerge, Engine.Operations.SheetMerge>()
        .AddSingleton<IScriptSheetEdits, ScriptSheetEdits>()
        .AddSingleton<IScriptReferences, ScriptReferences>()
        .AddSingleton<IScriptColors, ScriptColors>()
                .AddSingleton<IScriptProtection, ScriptProtection>()
                .AddSingleton<PulseOffice.Sheets.Engine.Operations.IValidationPlacement, PulseOffice.Sheets.Engine.Operations.ValidationPlacement>()
                .AddSingleton<ISheetEditGuard, SheetEditGuard>()
        .AddSingleton<IScriptOutput, NullOutput>()
        .AddSingleton<PulseOffice.Sheets.Formatting.INumberFormatter>(new PulseOffice.Sheets.Formatting.NumberFormatter(
            System.Globalization.CultureInfo.GetCultureInfo("it-IT"), serial => new DateTime(1899, 12, 30).AddDays(serial)))
        .AddSingleton<PulseOffice.Sheets.Formatting.ICellPresenter, PulseOffice.Sheets.Formatting.CellPresenter>()
        .AddSingleton<PulseOffice.Localization.ITextCatalog>(_texts)
        .AddSingleton<IActiveSheetContext>(_context)
        .AddSingleton<IScriptDispatcher>(new ReadOnlyGuardedDispatcher(new DirectDispatcher(), () => _readOnly));

    private readonly PulseOffice.Localization.TextCatalog _texts = new(PulseOffice.Localization.UiLanguage.English);
    private readonly Workbook _workbook = Workbook.CreateDefault();
    private readonly UndoStack _history = new();
    private readonly ActiveSheetContext _context = new();
    private bool _readOnly;

    public ScriptFacadeTests()
    {
        _provider = Services().BuildServiceProvider();

        _context.Attach(new SheetTarget
        {
            Workbook = _workbook,
            SheetIndex = 0,
            History = _history,
            Engine = _provider.GetRequiredService<ICalculationEngineFactory>().Create(_workbook),
            Selection = () => CellRange.Single(CellAddress.First),
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
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = _ => { },
            WriteSelectedObject = _ => { },
        });
    }

    private Worksheet Sheet => _workbook.Sheets[0];

    private T Create<T>() => ActivatorUtilities.CreateInstance<T>(_provider);

    [Fact]
    public void LeMacroLeggonoEScrivonoLaFormulaNellaLinguaDellInterfaccia()
    {
        var sheet = Create<SheetFacade>();

        sheet.SetFormulaLocal("A1", "=SUM({1,2;3,4},0.5)");
        Assert.Equal("=SUM({1;2|3;4};0.5)", sheet.Formula("A1"));
        Assert.Equal("=SUM({1,2;3,4},0.5)", sheet.FormulaLocal("A1"));

        _texts.Use(PulseOffice.Localization.UiLanguage.Italian);
        Assert.Equal("=SOMMA({1;2|3;4};0,5)", sheet.FormulaLocal("A1"));
        sheet.SetFormulaLocal("A2", "SOMMA(1,5;2)");
        Assert.Equal("=SUM(1.5;2)", sheet.Formula("A2"));
    }

    [Fact]
    public void ReadRestituisceIlTestoComeSiVedeNellaCella()
    {
        var sheet = Create<SheetFacade>();
        sheet.Write("A1", 1.5);
        sheet.Write("A2", "testo");

        Assert.Equal("1,5", sheet.Read("A1"));
        Assert.Equal("testo", sheet.Read("A2"));
        Assert.Equal(string.Empty, sheet.Read("A3"));
    }

    [Fact]
    public void UnRiferimentoNonValidoDaErroreInveceDiNonFareNulla()
    {
        var sheet = Create<SheetFacade>();

        Assert.Throws<ArgumentException>(() => sheet.Read("pippo"));
        Assert.Throws<ArgumentException>(() => sheet.Write("pippo", 1.0));
        Assert.Throws<ArgumentException>(() => sheet.Bold("A1:", true));
        Assert.Throws<ArgumentException>(() => sheet.Range(""));
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void LeMacroNonAggiungonoNeTolgonoOggettiNeScenariSuUnFoglioProtetto()
    {
        var sheet = Create<SheetFacade>();
        var scenarios = Create<ScenarioFacade>();
        var shape = sheet.AddShape("Rectangle", "B2");
        scenarios.Capture("Base", "A1:A2");
        Sheet.Protection = new SheetProtection { IsEnabled = true };

        Assert.Throws<InvalidOperationException>(() => sheet.AddShape("Rectangle", "C3"));
        Assert.Throws<InvalidOperationException>(() => sheet.AddChart("A1:A2", "Bar", "D4"));
        Assert.Throws<InvalidOperationException>(() => sheet.RemoveObject(shape));
        Assert.Throws<InvalidOperationException>(() => scenarios.Capture("Altro", "A1:A2"));
        Assert.Throws<InvalidOperationException>(() => scenarios.Remove("Base"));
        Assert.Single(Sheet.Objects);
        Assert.Single(Sheet.Scenarios);
    }

    [Fact]
    public void AShapeTooLargeForTheSheetIsRefusedInsteadOfOverflowing()
    {
        var sheet = Create<SheetFacade>();

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.AddShape("Rectangle", "B2", 1e12, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.AddShape("Rectangle", "B2", 20, double.PositiveInfinity));
        Assert.Empty(Sheet.Objects);
    }

    [Fact]
    public void ApplicareUnoScenarioSiAnnulla()
    {
        var sheet = Create<SheetFacade>();
        var scenarios = Create<ScenarioFacade>();
        sheet.Write("A1", 1.0);
        Assert.True(scenarios.Capture("Base", "A1:A1"));
        sheet.Write("A1", 5.0);
        Assert.True(scenarios.Apply("Base"));
        Assert.Equal(1, sheet.Number("A1"));

        _history.Undo();

        Assert.Equal(5, sheet.Number("A1"));
    }

    [Fact]
    public void IlFiltroNonSiCambiaInSolaLettura()
    {
        var filter = Create<FilterFacade>();
        Assert.True(filter.Activate("A1:B5"));
        _readOnly = true;

        Assert.Throws<InvalidOperationException>(() => filter.ByCondition(1, ">3"));
        Assert.Empty(Sheet.AutoFilter.Filters);
    }

    [Fact]
    public void UnaConvalidaSuUnaParteSostituisceSoloQuellaParte()
    {
        var validation = Create<ValidationFacade>();
        Assert.True(validation.AddWholeNumber("A1:A10", 1, 10));
        Assert.True(validation.AddList("A3:A4", ["x", "y"]));

        Assert.Equal(3, validation.Count);
        Assert.Equal(ValidationKind.List, Sheet.Validations.Single(rule => rule.Range.Contains(new CellAddress(2, 0))).Kind);

        Assert.Equal(1, validation.Clear("A1:A2"));
        Assert.DoesNotContain(Sheet.Validations, rule => rule.Range.Contains(new CellAddress(0, 0)));
        Assert.Contains(Sheet.Validations, rule => rule.Range.Contains(new CellAddress(9, 0)) && rule.Kind == ValidationKind.WholeNumber);
    }

    [Fact]
    public void UnRiferimentoNonValidoNonCancellaTutteLeConvalide()
    {
        var validation = Create<ValidationFacade>();
        Assert.True(validation.AddWholeNumber("A1:A3", 1, 10));

        Assert.Throws<ArgumentException>(() => validation.Clear("zz"));
        Assert.Equal(1, validation.Count);

        Assert.Equal(1, validation.Clear());
        _history.Undo();
        Assert.Equal(1, validation.Count);
    }

    [Fact]
    public void UnConfrontoSconosciutoERifiutato()
    {
        var rules = Create<FormatRuleFacade>();

        Assert.Throws<ArgumentException>(() => rules.AddCondition("A1:A3", "maggiorissimo", "3"));
        Assert.Empty(Sheet.ConditionalRules);
    }

    [Fact]
    public void LeMacroImpostanoLeDeviazioniStandardDellaMedia()
    {
        var rules = Create<FormatRuleFacade>();

        Assert.True(rules.AddCondition("A1:A9", "aboveAverage", "2"));
        Assert.True(rules.AddCondition("A1:A9", "belowAverage", string.Empty));

        Assert.Equal(2, Sheet.ConditionalRules[0].StandardDeviations);
        Assert.Equal(0, Sheet.ConditionalRules[1].StandardDeviations);
        Assert.Throws<ArgumentException>(() => rules.AddCondition("A1:A9", "aboveAverage", "4"));
        Assert.Throws<ArgumentException>(() => rules.AddCondition("A1:A9", "aboveAverage", "-1"));
        Assert.Equal(2, rules.Count);
    }

    [Fact]
    public void LeMacroUsanoGliOperatoriNuovi()
    {
        var rules = Create<FormatRuleFacade>();

        Assert.True(rules.AddCondition("A1:A9", "date", "thisWeek", "#FFEB84"));
        Assert.True(rules.AddCondition("A1:A9", "topPercent", "20"));
        Assert.True(rules.AddCondition("A1:A9", "duplicate", string.Empty));
        Assert.True(rules.AddCondition("A1:A9", "belowAverage", string.Empty));
        Assert.True(rules.AddCondition("A1:A9", "notContains", "x"));
        Assert.True(rules.AddDataBar("B1:B9", "#5B9BD5", "#C00000", gradient: false));

        var added = Sheet.ConditionalRules;
        Assert.Equal(DatePeriod.ThisWeek, added[0].DatePeriod);
        Assert.Equal(ConditionOperator.DateIs, added[0].Operator);
        Assert.Equal(["20"], added[1].Operands);
        Assert.Equal(ConditionOperator.TopPercent, added[1].Operator);
        Assert.Equal(ConditionOperator.Duplicate, added[2].Operator);
        Assert.Empty(added[2].Operands);
        Assert.Equal(ConditionOperator.BelowAverage, added[3].Operator);
        Assert.Equal(ConditionOperator.NotContainsText, added[4].Operator);
        Assert.Equal("#C00000", added[5].NegativeBarColorHex);
        Assert.False(added[5].Bar.Gradient);

        Assert.Throws<ArgumentException>(() => rules.AddCondition("A1:A9", "date", "someday"));
        Assert.Throws<ArgumentException>(() => rules.AddCondition("A1:A9", "topPercent", "120"));
        Assert.Equal(6, rules.Count);
    }

    [Fact]
    public void LettureESpostamentiFuoriMisuraSonoRifiutati()
    {
        var sheet = Create<SheetFacade>();

        Assert.Throws<ArgumentException>(() => sheet.Range("A1:Z1048576"));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.HideRows(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.RowHeight(1, double.NaN));
    }

    [Fact]
    public void Row_heights_and_column_widths_beyond_what_the_dialog_allows_are_refused()
    {
        var sheet = Create<SheetFacade>();

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.RowHeight(1, 1e9));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.ColumnWidth(1, 501));
        sheet.RowHeight(1, 500);
    }

    [Fact]
    public void Texts_longer_than_a_cell_holds_are_refused_and_change_nothing()
    {
        var sheet = Create<SheetFacade>();
        var tooLong = new string('x', 32_768);

        Assert.Throws<ArgumentException>(() => sheet.Write("A1", tooLong));
        Assert.Throws<ArgumentException>(() => sheet.WriteRange("A1", [[tooLong]]));
        Assert.Throws<ArgumentException>(() => sheet.Comment("A1", tooLong));
        Assert.True(sheet.IsEmpty("A1"));

        sheet.Write("A1", "ab");
        sheet.Write("A2", "ab");
        Assert.Throws<ArgumentException>(() => sheet.Replace("b", new string('y', 32_767)));
        Assert.Equal("ab", sheet.Read("A1"));
        Assert.Equal("ab", sheet.Read("A2"));
        Assert.Equal(2, sheet.Replace("b", "c"));
    }

    [Fact]
    public void SelezionareSiPuoAncheInSolaLettura()
    {
        var sheet = Create<SheetFacade>();
        _readOnly = true;

        sheet.Select("B2");
        Assert.Throws<InvalidOperationException>(() => sheet.Write("A1", 1.0));
    }

    [Fact]
    public void UnireLeCelleSiAnnulla()
    {
        var sheet = Create<SheetFacade>();

        Assert.True(sheet.Merge("A1:B2"));
        Assert.NotEmpty(Sheet.Merges.All);
        _history.Undo();
        Assert.Empty(Sheet.Merges.All);
    }

    private sealed class DirectDispatcher : IScriptDispatcher
    {
        public T Read<T>(Func<T> read) => read();

        public void Change(Action change) => change();

        public void Show(Action show) => show();

        public T Write<T>(Func<T> write) => write();
    }

    private sealed class NullOutput : IScriptOutput
    {
        public void Write(string message) { }

        public IDisposable Capture(Action<string> sink) => new Released();
    }

    private sealed class Released : IDisposable
    {
        public void Dispose() { }
    }
}
