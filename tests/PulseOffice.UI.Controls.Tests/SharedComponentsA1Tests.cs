using PulseOffice.Testing;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class SharedComponentsA1Tests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    private static readonly DesignTokens.Metrics Metrics = DesignTokens.For(InterfaceDensity.Compact);

    private static MeasureEntry Entry(string unit, double min = 0, double max = 100, double step = 1) =>
        new(new MeasureOptions(unit, min, max, step), new MeasureUnits(), Italian);

    [Fact]
    public void UnitsConvertWithinADimensionOnly()
    {
        var units = new MeasureUnits();
        Assert.Equal(10, units.Convert(1, units.Find("cm")!, units.Find("mm")!), 9);
        Assert.Equal(72, units.Convert(1, units.Find("in")!, units.Find("pt")!), 9);
        Assert.Equal(25.4, units.Convert(1, units.Find("in")!, units.Find("mm")!), 9);
        Assert.Equal(0.75, units.Convert(1, units.Find("px")!, units.Find("pt")!), 9);
        Assert.Equal(1500, units.Convert(1.5, units.Find("s")!, units.Find("ms")!), 9);
        Assert.Throws<InvalidOperationException>(() => units.Convert(1, units.Find("%")!, units.Find("mm")!));
        Assert.Equal(MeasureDimension.Lines, units.Find("Zeilen")!.Dimension);
    }

    [Fact]
    public void LabelReaderMovesTheUnitOutOfTheLabel()
    {
        var reader = new MeasureLabelReader(new MeasureUnits());
        Assert.Equal(new LabelledUnit("Margine sinistro", "mm"), reader.Read("Margine sinistro (mm)"));
        Assert.Equal(new LabelledUnit("Rotazione", "°"), reader.Read("Rotazione (°)"));
        Assert.Equal(new LabelledUnit("Widow control", "lines"), reader.Read("Widow control (lines)"));
        Assert.Null(reader.Read("Nome (facoltativo)"));
        Assert.Null(reader.Read("Larghezza"));
    }

    [Fact]
    public void EntryParsesTypedUnitsAndClamps()
    {
        var entry = Entry("mm", 0, 300, 0.5);

        Assert.Equal(new MeasureParse(MeasureParseOutcome.Accepted, 12.5), entry.Parse("12,5"));
        Assert.Equal(new MeasureParse(MeasureParseOutcome.Accepted, 20), entry.Parse("2 cm"));
        Assert.Equal(25.4, entry.Parse("1in").Value, 9);
        Assert.Equal(MeasureParseOutcome.Invalid, entry.Parse("abc").Outcome);
        Assert.Equal(MeasureParseOutcome.Invalid, entry.Parse("5 °").Outcome);
        Assert.Equal(new MeasureParse(MeasureParseOutcome.Clamped, 300), entry.Parse("400"));
        Assert.Equal(5, entry.LargeStep);
        Assert.Equal(15.5, entry.Step(10.5, 1, large: true));

        entry.ShowIn(entry.Alternatives.Single(unit => unit.Symbol == "cm"));
        Assert.Equal("2,5", entry.Format(25));
        Assert.Equal(25, entry.Parse("2,5").Value, 9);
        Assert.Equal("cm", entry.DisplayLabel);
    }

    [Fact]
    public Task MeasureFieldStepsWithKeysWheelAndScrubAndWarns() => Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.English);
        var field = new MeasureField(Entry("pt", 0, 100, 1), 10, texts, Metrics, accessibleName: "Indent");
        var window = new Window { Content = field };
        window.Show();
        var box = field.GetVisualDescendants().OfType<TextBox>().Single();
        Assert.Equal("Indent", AutomationProperties.GetName(box));
        Assert.Equal("pt", field.UnitLabel);

        box.Focus();
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up });
        Assert.Equal(11, field.Value);
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up, KeyModifiers = KeyModifiers.Shift });
        Assert.Equal(21, field.Value);
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.PageDown });
        Assert.Equal(11, field.Value);

        field.BeginScrub();
        field.Scrub(20, large: false);
        Assert.Equal(16, field.Value);

        box.Text = "xyz";
        Assert.Equal(16, field.Read());
        Assert.Contains("between", field.Message, StringComparison.Ordinal);

        box.Text = "500";
        Assert.Equal(100, field.Read());
        Assert.NotNull(field.Message);

        box.Text = "40";
        Dispatcher.UIThread.RunJobs();
        Assert.Null(field.Message);
        Assert.Equal(40, field.Read());
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task FormTurnsAUnitInTheLabelIntoAMeasureField() => Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.English);
        var form = new FormPromptView(string.Empty,
        [
            new NumberField("margin", "Margin (mm)", 20, 0, 200),
            new NumberField("count", "Count", 3, 0, 10),
            new NumberField("size", "Size", 12, 1, 400) { Unit = "pt", Presets = [8, 10, 12], ShowSlider = true },
        ], texts);
        var window = new Window { Content = form };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var measures = form.GetVisualDescendants().OfType<MeasureField>().ToList();
        Assert.Equal(2, measures.Count);
        Assert.Equal("mm", measures[0].UnitLabel);
        Assert.Contains(form.GetVisualDescendants().OfType<TextBlock>(), label => label.Text == "Margin");
        Assert.DoesNotContain(form.GetVisualDescendants().OfType<TextBlock>(), label => label.Text == "Margin (mm)");
        Assert.Single(form.GetVisualDescendants().OfType<NumberBox>());
        Assert.Single(form.GetVisualDescendants().OfType<Slider>());

        var box = measures[0].GetVisualDescendants().OfType<TextBox>().Single();
        box.Text = "3 cm";
        Assert.Equal(30d, form.Values["margin"]);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public void HelpSplitterKeepsTheFirstSentenceAsSubtitle()
    {
        var splitter = new HelpTextSplitter();
        Assert.False(splitter.Split("Short text.").HasDetails);

        var help = splitter.Split("Choose the range to protect. Locked cells cannot be edited until you remove the protection, and the password is never stored in the document.");
        Assert.Equal("Choose the range to protect.", help.Summary);
        Assert.StartsWith("Locked cells", help.Details, StringComparison.Ordinal);
    }

    [Fact]
    public Task HelpTipShowsTheRestBehindTheQuestionMark() => Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.English);
        var text = "Choose the range to protect. Locked cells cannot be edited until you remove the protection, and the password is never stored in the document.";
        var form = new FormPromptView(text, [new TextField("name", "Name")], texts);
        var window = new Window { Content = form };
        window.Show();

        var tip = form.GetVisualDescendants().OfType<HelpTip>().Single();
        Assert.NotNull(tip.MoreButton);
        Assert.Equal("More information", AutomationProperties.GetName(tip.MoreButton!));
        Assert.Contains(form.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Choose the range to protect.");
        Assert.Equal(tip.Help.Details, AutomationProperties.GetHelpText(tip.MoreButton!));
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task IconChoiceGroupMovesWithArrowsAndNamesEachChoice() => Session.Dispatch(() =>
    {
        var group = new IconChoiceGroup(
        [
            new IconChoice("left", "folio.paragraph.alignLeft", "Left"),
            new IconChoice("center", "folio.paragraph.alignCenter", "Center"),
            new IconChoice("right", "folio.paragraph.alignRight", "Right"),
        ], null, Metrics, "Alignment", selected: 0);
        var window = new Window { Content = group };
        window.Show();
        var changes = new List<int>();
        group.SelectionChanged += (_, index) => changes.Add(index);

        Assert.Equal("Alignment", AutomationProperties.GetName(group));
        Assert.Equal("Center", AutomationProperties.GetName(group.Items[1]));
        Assert.True(group.Items[0].IsTabStop);
        Assert.False(group.Items[1].IsTabStop);

        group.Items[0].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
        Assert.Equal("center", group.SelectedKey);
        group.Items[1].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End });
        Assert.Equal(2, group.SelectedIndex);
        Assert.True(group.Items[2].IsChecked);
        Assert.False(group.Items[0].IsChecked);
        Assert.Equal([1, 2], changes);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task IconChoiceGroupShowsTheLabelWhenAChoiceHasNoIcon() => Session.Dispatch(() =>
    {
        var group = new IconChoiceGroup(
        [
            new IconChoice("100", string.Empty, "1"),
            new IconChoice("150", string.Empty, "1.5"),
        ], new IconCatalog(), Metrics, "Line spacing", selected: 0, showLabels: true);
        var window = new Window { Content = group };
        window.Show();

        Assert.Contains(group.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "1.5");
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task SwatchStripSelectsAndAcceptsACustomColour() => Session.Dispatch(() =>
    {
        var texts = new TextCatalog(UiLanguage.English);
        var strip = new SwatchStrip(
        [
            new Swatch("red", "Red", "#FF0000"),
            new Swatch("bad", "Bad", "#FFC7CE") { Foreground = "#9C0006", Sample = "Ab" },
        ], texts, Metrics, "Style", selectedKey: "red");
        var window = new Window { Content = strip };
        window.Show();
        var changes = 0;
        strip.Changed += (_, _) => changes++;

        Assert.Equal("#FF0000", strip.Value);
        Assert.Equal("More…", AutomationProperties.GetName(strip.MoreButton!));

        strip.Items[0].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
        Assert.Equal("bad", strip.SelectedKey);

        strip.Choose("#123456");
        Assert.Null(strip.SelectedKey);
        Assert.Equal("#123456", strip.Value);

        strip.Choose("#ff0000");
        Assert.Equal("red", strip.SelectedKey);
        Assert.Null(strip.CustomColour);
        Assert.Equal(3, changes);
        window.Close();
    }, CancellationToken.None);
}
