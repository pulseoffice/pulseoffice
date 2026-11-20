using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class Round321ShapeTextAlignmentCallerTests
{
    private readonly ShapeRichText _rich = new();
    private readonly ShapeTextParagraphs _paragraphs = new();

    private static ShapeObject Centred(IReadOnlyList<TextHorizontalPlacement?>? alignments = null) => new()
    {
        Id = "Centred",
        Shape = ShapeKind.Rectangle,
        Text = "One\nTwo",
        Style = ShapeStyle.Default with { Text = ShapeStyle.Default.Text with { Horizontal = TextHorizontalPlacement.Centre } },
        Alignments = alignments,
    };

    [Fact]
    public void Paragraphs_without_their_own_alignment_take_the_shape_alignment()
    {
        var text = _rich.ToRich(Centred([TextHorizontalPlacement.Right]));

        Assert.Equal(new[] { TextAlign.Right, TextAlign.Center }, text.Paragraphs.Select(paragraph => paragraph.Align));
    }

    [Fact]
    public void A_round_trip_through_rich_text_keeps_the_shape_alignment_and_adds_no_paragraph_alignments()
    {
        var shape = Centred();

        var back = _rich.FromRich(shape, _rich.ToRich(shape));

        Assert.Equal(TextHorizontalPlacement.Centre, back.Style.Text.Horizontal);
        Assert.Null(back.Alignments);
    }

    [Fact]
    public void A_paragraph_change_that_leaves_alignment_alone_keeps_the_shape_centred()
    {
        var shape = Centred();

        var listed = _paragraphs.Apply(shape, 0, 0, paragraph => paragraph with { Bullet = true });

        Assert.Equal(TextHorizontalPlacement.Centre, listed.Style.Text.Horizontal);
        Assert.Null(listed.Alignments);
        Assert.Equal(TextAlign.Center, _paragraphs.First(listed, 0, 0).Align);
    }

    [Fact]
    public void Aligning_one_paragraph_left_in_a_centred_shape_is_kept_for_that_paragraph_only()
    {
        var aligned = _paragraphs.Apply(Centred(), 0, 0, paragraph => paragraph with { Align = TextAlign.Left });

        Assert.Equal(new TextHorizontalPlacement?[] { TextHorizontalPlacement.Left }, aligned.Alignments);
        Assert.Equal(TextAlign.Left, _paragraphs.First(aligned, 0, 0).Align);
        Assert.Equal(TextAlign.Center, _paragraphs.First(aligned, 5, 5).Align);
    }
}
