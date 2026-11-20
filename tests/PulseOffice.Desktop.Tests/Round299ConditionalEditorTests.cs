using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.UI.Conditional;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round299ConditionalEditorTests(HeadlessDesktopSession headless)
{
    private readonly ITextCatalog _texts = new TextCatalog(UiLanguage.English);

    private sealed class NoReferences : IReferenceSource
    {
        public PulseOffice.UI.Controls.References.IReferencePicker? Picker => null;

        public bool IsValid(string text) => true;
    }

    private ConditionalRuleEditorView Editor(ConditionalRule rule) => new(rule, string.Empty, new NoReferences(),
        new ConditionalEditorServices(
            new ConditionalRuleCatalog(), new ConditionalPainting(new PulseOffice.Drawing.ConditionalIcons.ConditionalIconCatalog(), new IconPainter()),
            new GalleryNavigator(), new MeasureUnits(), new IconCatalog()),
        _texts);

    [Fact]
    public Task A_custom_format_change_keeps_the_other_attributes_of_the_rule_style() => headless.Session.Dispatch(() =>
    {
        var rule = new ConditionalRuleCatalog().Default(ConditionalRuleKind.CellValue, CellRange.Single(CellAddress.First)) with
        {
            Style = new CellStyle { NumberFormatId = 4, Font = CellFont.Default with { Italic = true, ColorHex = "#123456" } },
        };
        var view = Editor(rule);
        var bold = view.GetLogicalDescendants().OfType<CheckBox>().Single(box => Equals(box.Content, _texts["condition.bold"]));

        bold.IsChecked = true;

        Assert.True(view.Rule.Style.Font.Bold);
        Assert.True(view.Rule.Style.Font.Italic);
        Assert.Equal(4, view.Rule.Style.NumberFormatId);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Typed_values_survive_switching_to_another_kind_and_back() => headless.Session.Dispatch(() =>
    {
        var view = Editor(new ConditionalRuleCatalog().Default(ConditionalRuleKind.CellValue, CellRange.Single(CellAddress.First)));
        var first = view.GetLogicalDescendants().OfType<TextBox>().First(box => AutomationProperties.GetName(box) == _texts["condition.first"]);

        first.Text = "42";
        view.SwitchKind(ConditionalRuleKind.ColorScale);
        view.SwitchKind(ConditionalRuleKind.CellValue);

        Assert.Equal("42", view.Draft.TypedOperands[0]);
        return Task.CompletedTask;
    }, CancellationToken.None);
}
