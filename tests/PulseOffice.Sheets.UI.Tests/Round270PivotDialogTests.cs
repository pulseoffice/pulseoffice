using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round270PivotDialogTests
{
    private static readonly PulseOffice.Localization.ITextCatalog Texts =
        new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English);

    private static readonly PivotEngine PivotEngine = new(new PivotAggregator(), new PivotGrouping(),
        new PivotFormulas(new Engine.Parsing.FormulaParser()));

    private static PivotPromptView Dialog()
    {
        var workbook = Workbook.CreateDefault();
        var data = workbook.Sheets[0];
        data.SetValue(new CellAddress(0, 0), CellValue.Text("Date"));
        data.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        data.SetValue(new CellAddress(1, 0), CellValue.Number(45306));
        data.SetValue(new CellAddress(1, 1), CellValue.Number(5));
        data.SetValue(new CellAddress(2, 0), CellValue.Number(45420));
        data.SetValue(new CellAddress(2, 1), CellValue.Number(3));
        var pivot = new PivotDefinition
        {
            Id = "p", Name = "Pivot1", SourceSheet = data.Name, SourceRange = new CellRange(new(0, 0), new(2, 1)),
            DestinationSheet = "Pivot1", Destination = CellAddress.First,
            Fields =
            [
                new PivotField { SourceName = "Date", SourceColumnIndex = 0, Area = PivotArea.Row, DateGrouping = DateGrouping.Quarters },
                new PivotField { SourceName = "Double", SourceColumnIndex = -1, Area = PivotArea.Value, AreaOrder = 0, Formula = "=Amount*2" },
                new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value, AreaOrder = 1 },
            ],
        };
        var source = new PivotSources().Open(workbook, pivot)!;
        var kit = new DataDialogs.LedgerDialogKit(Texts, System.Reflection.DispatchProxy.Create<PulseOffice.UI.Icons.IIconCatalog, LedgerCommandsAuditTests.Inert>(),
            new PulseOffice.UI.Controls.Dialogs.HelpTextSplitter());
        return new PivotPromptView(source.Headers, "Pivot1", kit, new SourcePivotPreviewer(PivotEngine, source, new LedgerPivotLabels(Texts).Create()), pivot);
    }

    [Fact]
    public void The_edit_dialog_keeps_a_calculated_value_field_in_place_and_edits_its_formula()
    {
        var view = Dialog();
        var calculated = view.Model.In(PivotArea.Value)[0];

        Assert.True(view.Model.IsCalculated(calculated));
        Assert.Equal("Double", view.Model.NameOf(calculated));
        view.Settings.Select(calculated);
        var formula = view.Settings.GetLogicalDescendants().OfType<TextBox>()
            .Single(box => AutomationProperties.GetName(box) == Texts["pivot.calculated.formula"]);
        Assert.Equal("=Amount*2", formula.Text);

        view.Model.Configure(calculated, field => field with { Formula = "=Amount*3", DisplayMode = ValueDisplayMode.PercentOfTotal });
        var fields = view.Model.Fields().Where(field => field.Area == PivotArea.Value).OrderBy(field => field.AreaOrder).ToList();

        Assert.Equal(["Double", "Amount"], fields.Select(field => field.SourceName));
        Assert.Equal("=Amount*3", fields[0].Formula);
        Assert.Equal(ValueDisplayMode.PercentOfTotal, fields[0].DisplayMode);
    }

    [Fact]
    public void The_settings_panel_lists_grouped_items_by_their_labels()
    {
        var view = Dialog();
        view.Settings.Select(0);

        var items = view.Settings.GetLogicalDescendants().OfType<CheckBox>().Select(box => box.Content as string).OfType<string>().ToList();

        Assert.NotEmpty(items);
        Assert.DoesNotContain(items, item => item.Contains("-T", StringComparison.Ordinal));
        Assert.Contains(items, item => item.Contains("2024", StringComparison.Ordinal));
    }
}
