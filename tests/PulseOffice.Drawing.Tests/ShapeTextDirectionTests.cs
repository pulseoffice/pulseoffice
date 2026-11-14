using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich.Bidi;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class ShapeTextDirectionTests
{
    private readonly ShapeTextParagraphs _paragraphs = new();
    private readonly ShapeTextLists _lists = new();
    private readonly ShapeRichText _rich = new();

    private static ShapeObject Shape(string text) => new() { Id = "Forma", Shape = ShapeKind.Rectangle, Text = text };

    [Fact]
    public void Setting_the_direction_of_a_paragraph_is_kept_and_read_back()
    {
        var shape = _paragraphs.Apply(Shape("Uno\nDue"), 5, 5, paragraph => paragraph with { Direction = TextDirection.RightToLeft });

        Assert.Equal([null, TextDirection.RightToLeft], shape.Directions!);
        Assert.Null(_rich.ToRich(shape).Paragraphs[0].Direction);
        Assert.Equal(TextDirection.RightToLeft, _rich.ToRich(shape).Paragraphs[1].Direction);
        Assert.Equal(TextDirection.RightToLeft, _paragraphs.First(shape, 5, 5).Direction);
    }

    [Fact]
    public void The_direction_of_a_single_paragraph_shape_is_kept()
    {
        var shape = _paragraphs.Apply(Shape("Uno"), 0, 0, paragraph => paragraph with { Direction = TextDirection.RightToLeft });

        Assert.Equal([TextDirection.RightToLeft], shape.Directions!);
    }

    [Fact]
    public void Splitting_a_paragraph_carries_its_direction_and_rebuilding_from_rich_text_keeps_it()
    {
        var shape = Shape("Uno\nDue") with { Directions = [TextDirection.RightToLeft, TextDirection.LeftToRight] };

        var split = _lists.Spliced(shape with { Text = "Un\no\nDue" }, "Uno\nDue", 2, string.Empty, "\n");
        var rebuilt = _rich.FromRich(shape, _rich.ToRich(shape));

        Assert.Equal([TextDirection.RightToLeft, TextDirection.RightToLeft, TextDirection.LeftToRight], split.Directions!);
        Assert.Equal(shape.Directions, rebuilt.Directions);
    }
}
