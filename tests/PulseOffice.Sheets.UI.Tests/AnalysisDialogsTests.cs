using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.UI.Dialogs;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class AnalysisDialogsTests
{
    private readonly SheetReferenceReader _reader = new(new SheetRangeReferences());

    private static Workbook Book()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Base");
        workbook.AddSheet("Dati 2");
        return workbook;
    }

    [Fact]
    public void ReaderAcceptsRangesOnOtherSheets()
    {
        var read = _reader.Read(Book(), "'Dati 2'.A1:B3", 0, ReferenceShape.Range);
        Assert.NotNull(read);
        Assert.Equal(1, read[0].SheetIndex);
        Assert.Equal(6, read[0].Range.CellCount);
    }

    [Fact]
    public void CellShapeRejectsRanges()
    {
        Assert.Null(_reader.Read(Book(), "A1:B2", 0, ReferenceShape.Cell));
        Assert.NotNull(_reader.Read(Book(), "$C$4", 0, ReferenceShape.Cell));
    }

    [Fact]
    public void RangeListNeedsEveryPartValid()
    {
        Assert.Equal(2, _reader.Read(Book(), "A1:A3; C1", 0, ReferenceShape.RangeList)!.Count);
        Assert.Null(_reader.Read(Book(), "A1:A3;zz", 0, ReferenceShape.RangeList));
        Assert.Null(_reader.Read(Book(), "", 0, ReferenceShape.Range));
    }

    [Fact]
    public void ScenarioNamesMustBeUniqueAndPresent()
    {
        var planner = new ScenarioPlanner();
        var range = CellRange.Single(CellAddress.First);
        Assert.Equal([1, 2], planner.InvalidRows(
        [
            new ScenarioRow(null, "Base", string.Empty, range),
            new ScenarioRow(null, " base ", string.Empty, range),
            new ScenarioRow(null, "  ", string.Empty, range),
        ]));
        Assert.Equal("Scenario 2", planner.NextName(["Scenario 1"], "Scenario"));
    }

    [Fact]
    public void ScenarioPlanRenamesExistingAndCapturesNew()
    {
        var range = CellRange.Single(CellAddress.First);
        var existing = new Scenario { Name = "Vecchio", Range = range, Values = new Dictionary<CellAddress, CellValue>() };
        var outcome = new ScenarioPlanner().Build(
        [
            new ScenarioRow(existing, "Rinominato", " nota ", range),
            new ScenarioRow(null, "Nuovo", string.Empty, range, Apply: true),
        ],
        (captured, name, comment) => new Scenario { Name = name, Comment = comment, Range = captured, Values = new Dictionary<CellAddress, CellValue>() });

        Assert.Equal("Rinominato", outcome.Scenarios[0].Name);
        Assert.Equal("nota", outcome.Scenarios[0].Comment);
        Assert.Same(outcome.Scenarios[1], outcome.Apply);
    }

    [Fact]
    public void TextToColumnsSeparatorsFromChoice()
    {
        var choice = new TextToColumnsChoice(true, false, true, false, "|;", false, TextQualifier.None, true);
        Assert.Equal(['\t', ',', '|', ';'], choice.Separators());
        Assert.Null(choice.Request(CellRange.Single(CellAddress.First)).Quote);
    }

    [Fact]
    public void ValidationLayoutFollowsKindAndOperator()
    {
        var rules = new ValidationRules();
        var between = rules.Layout(ValidationKind.WholeNumber, ValidationOperator.Between);
        Assert.Equal(("validation.minimum", "validation.maximum"), (between.FirstLabelKey, between.SecondLabelKey));
        Assert.Null(rules.Layout(ValidationKind.Date, ValidationOperator.Greater).SecondLabelKey);
        Assert.True(rules.Layout(ValidationKind.List, ValidationOperator.Greater).List);
        Assert.False(rules.Layout(ValidationKind.Custom, ValidationOperator.Between).Operator);
    }

    [Fact]
    public void ValidationListFromValuesOrRange()
    {
        var rules = new ValidationRules();
        var range = CellRange.Single(CellAddress.First);

        var values = rules.Build(new ValidationDraft { Kind = ValidationKind.List, ListValues = [" Nord ", "", "Sud"], First = "ignored" }, range)!;
        Assert.Equal(["Nord", "Sud"], values.ListValues);
        Assert.Null(values.FirstFormula);

        var fromRange = rules.Build(new ValidationDraft { Kind = ValidationKind.List, ListFromRange = true, ListRange = "$B$1:$B$4" }, range)!;
        Assert.Empty(fromRange.ListValues);
        Assert.Equal("$B$1:$B$4", fromRange.FirstFormula);
        Assert.True(rules.From(fromRange).ListFromRange);
    }

    [Fact]
    public void ValidationSingleValueDropsSecondAndAnyRemovesRule()
    {
        var rules = new ValidationRules();
        var range = CellRange.Single(CellAddress.First);
        var rule = rules.Build(new ValidationDraft
        {
            Kind = ValidationKind.Decimal, Operator = ValidationOperator.Greater, First = "5", Second = "9",
            ErrorMessage = " Troppo piccolo ", Alert = ValidationAlert.Warning,
        }, range)!;

        Assert.Null(rule.SecondFormula);
        Assert.Equal("Troppo piccolo", rule.ErrorMessage);
        Assert.Equal(ValidationAlert.Warning, rules.From(rule).Alert);
        Assert.Null(rules.Build(new ValidationDraft(), range));
    }
}
