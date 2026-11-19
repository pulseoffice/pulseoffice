using Avalonia;
using Avalonia.Input;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round272ShapeTextKeyTests
{
    private static PulseOffice.Testing.UiThreadSession Session => MacroEventsViewTests.Session;

    private static ShapeObject Shape(string text, IReadOnlyList<ShapeListItem?>? lists = null) => new()
    {
        Id = "s",
        Shape = ShapeKind.Rectangle,
        Text = text,
        Lists = lists,
    };

    private static (ShapeTextEditor Editor, Avalonia.Controls.TextBox Box) Editing(ShapeObject shape)
    {
        var editor = new ShapeTextEditor();
        editor.Begin(new ShapeTextEditStart(shape.Id, shape, new ShapeTextPlacement(new Rect(0, 0, 200, 100), 1), new ShapeSurroundings()));
        return (editor, ((PulseOffice.UI.Controls.Text.Editing.InlineTextEditor)editor.Child!).Box);
    }

    private static void Press(Avalonia.Controls.TextBox box, Key key, int caret, KeyModifiers modifiers = KeyModifiers.None)
    {
        box.SelectionStart = box.SelectionEnd = box.CaretIndex = caret;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = box });
    }

    [Fact]
    public Task Tab_in_a_plain_paragraph_inserts_a_tab() => Session.Dispatch(() =>
    {
        var (editor, box) = Editing(Shape("Uno"));

        Press(box, Key.Tab, 1);

        Assert.Equal("U\tno", editor.Live!.Text);
    }, CancellationToken.None);

    [Fact]
    public Task The_list_keys_walk_through_the_live_editor() => Session.Dispatch(() =>
    {
        var (editor, box) = Editing(Shape("Uno\n", [new ShapeListItem(), new ShapeListItem()]));

        Press(box, Key.Tab, 1);
        Assert.Equal(1, new ShapeTextLists().Items(editor.Live!)[0]!.Level);
        Assert.Equal("Uno\n", editor.Live!.Text);

        Press(box, Key.Tab, 1, KeyModifiers.Shift);
        Assert.Equal(0, new ShapeTextLists().Items(editor.Live!)[0]!.Level);

        Press(box, Key.Enter, 4);
        Assert.Null(new ShapeTextLists().Items(editor.Live!)[1]);

        Press(box, Key.Back, 0);
        Assert.Null(new ShapeTextLists().Items(editor.Live!)[0]);
        Assert.Equal("Uno\n", editor.Live!.Text);
    }, CancellationToken.None);

    [Fact]
    public void Preset_text_areas_follow_the_inner_rectangle_of_the_shape()
    {
        var area = new ShapeTextArea();

        Assert.Equal(ShapeTextMargins.None, area.Margins(Shape("x"), 100, 100));
        Assert.Equal(25, area.Margins(Shape("x") with { Shape = ShapeKind.Diamond }, 100, 100).Left, 3);
        Assert.Equal(50, area.Margins(Shape("x") with { Shape = ShapeKind.Triangle }, 100, 100).Top, 3);
        Assert.InRange(area.Margins(Shape("x") with { Shape = ShapeKind.Ellipse }, 100, 100).Left, 14, 15);
    }

    [Fact]
    public Task An_ellipse_needs_more_height_than_a_rectangle_for_the_same_text() => Session.Dispatch(() =>
    {
        var fit = new ShapeTextFit();
        var text = Shape("Uno\nDue\nTre") with { Style = ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Autofit = TextAutofit.ResizeShape } } };
        var size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromPoints(200), PulseOffice.Foundation.Units.Twip.FromPoints(20));

        var rectangle = fit.Height(text, size)!.Value;
        var ellipse = fit.Height(text with { Shape = ShapeKind.Ellipse }, size)!.Value;

        Assert.True(ellipse.Value > rectangle.Value);
        Assert.Null(fit.Height(text with { Style = ShapeStyle.Default }, size));
    }, CancellationToken.None);

    [Fact]
    public Task Stacked_text_that_overflows_is_shrunk_by_the_composer() => Session.Dispatch(() =>
    {
        var text = new RichText(PlaceholderRole.Body, [new TextParagraph("Uno due tre quattro cinque sei")])
        {
            Frame = new TextFrame { Autofit = TextAutofit.ShrinkText, Layout = TextLayout.Default with { Flow = TextFlow.Stacked } },
        };
        var composer = new TextFrameComposer(new ListLabels());
        var theme = new ShapeTextTheme(0xFF000000, "Inter");
        var fields = new TextFields(0, DateTime.Now);

        var shrunk = composer.Compose(text, new Rect(0, 0, 60, 60), theme, fields, 0.05);
        var plain = composer.Compose(text with { Frame = text.Frame with { Autofit = TextAutofit.None } }, new Rect(0, 0, 60, 60), theme, fields, 0.05);

        Assert.True(shrunk.FontFactor < 1);
        Assert.Equal(1, plain.FontFactor);
    }, CancellationToken.None);
}
