using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round211ShapeStyleKeyboardTests
{
    private const string TitleKey = "dialog.shapeEdit.title";

    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private readonly StyledOutline _shape = new(new ShapeGeometry().Outline(ShapeKind.RoundedRectangle), ShapeStyle.Default) { Text = "Note" };

    private readonly TextCatalog _texts = new(UiLanguage.English);

    private ShapeStyleView Editor(DialogKeyboardWalk walk, int section) =>
        new(_shape, _texts, new TestFontPickers(_texts, walk.Dialogs).Factory, initialSection: section);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task Every_section_of_the_shape_style_dialog_walks_with_tab_and_cancels_with_escape(int section) => Session.Dispatch(() =>
    {
        var walk = new DialogKeyboardWalk(_texts);
        try
        {
            walk.WalkAndCancel(Editor(walk, section), TitleKey);
        }
        finally
        {
            walk.Close();
        }
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task The_font_picker_of_the_text_section_is_a_tab_stop() => Session.Dispatch(() =>
    {
        var walk = new DialogKeyboardWalk(_texts);
        try
        {
            var editor = Editor(walk, 3);
            _ = walk.Open(editor, TitleKey);
            var stops = walk.Cycle(backwards: false);
            Assert.Contains(stops, stop => stop is Visual visual && visual.FindAncestorOfType<FontPicker>() is not null);
        }
        finally
        {
            walk.Close();
        }
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Enter_on_an_effect_check_box_confirms_the_style() => Session.Dispatch(() =>
    {
        var walk = new DialogKeyboardWalk(_texts);
        try
        {
            walk.ConfirmWithEnter(Editor(walk, 2), TitleKey, focused => focused is CheckBox);
        }
        finally
        {
            walk.Close();
        }
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task A_colour_palette_is_one_tab_stop_and_arrows_move_between_its_swatches() => Session.Dispatch(() =>
    {
        var walk = new DialogKeyboardWalk(_texts);
        try
        {
            var picker = new ColorPickerView("#2563EB", 1, _texts);
            _ = walk.Open(picker, TitleKey);
            var stops = walk.Cycle(backwards: false);
            var swatches = stops.OfType<Visual>().Where(stop => stop.FindAncestorOfType<ColorPickerView>() is not null && stop is Button).ToList();
            var swatch = Assert.Single(swatches);
            ((Button)swatch).Focus();
            walk.Press(Key.Right, PhysicalKey.ArrowRight);
            Assert.NotSame(swatch, walk.Focused);
            Assert.IsType<Button>(walk.Focused);
            walk.Press(Key.Left, PhysicalKey.ArrowLeft);
            Assert.Same(swatch, walk.Focused);
        }
        finally
        {
            walk.Close();
        }
        return Task.CompletedTask;
    }, CancellationToken.None);
}
