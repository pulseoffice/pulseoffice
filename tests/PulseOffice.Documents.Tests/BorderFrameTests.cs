using PulseOffice.Document.Borders;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Borders;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class BorderFrameTests
{
    private readonly BorderStroke _thin = new(BorderDash.Solid, 0.75, "#000000");
    private readonly BorderStroke _red = new(BorderDash.Dashed, 1.5, "#DC2626");
    private readonly BorderFrameScope _range = new(true, true, false);
    private readonly BorderFrameScope _cell = new(false, false, true);
    private readonly BorderFrameResolver _resolver = new();
    private readonly BorderPresetCatalog _presets = new();
    private readonly BorderFrameEditing _editing = new();

    [Fact]
    public void LeCorniciUgualiSonoUguali()
    {
        var a = BorderFrame.Empty.With(BorderPosition.Top, _thin).With(BorderPosition.Left, _red);
        var b = BorderFrame.Empty.With(BorderPosition.Left, _red).With(BorderPosition.Top, _thin);

        Assert.Equal(a, b);
        Assert.NotEqual(a, BorderFrame.Empty);
        Assert.Equal(BorderFrame.Empty, a.With(BorderPosition.Top, null).With(BorderPosition.Left, null));
    }

    [Fact]
    public void IlContornoTocaSoloILatiEsterni()
    {
        var outline = _presets.For(_range).Single(preset => preset.Key == "outline");

        var frame = outline.Apply(BorderFrame.Empty, _range, _thin);

        Assert.Equal([BorderPosition.Top, BorderPosition.Bottom, BorderPosition.Left, BorderPosition.Right], frame.Positions);
    }

    [Fact]
    public void GliInterniEsistonoSoloPerUnIntervallo()
    {
        Assert.Contains(_presets.For(_range), preset => preset.Key == "inside");
        Assert.DoesNotContain(_presets.For(_cell), preset => preset.Key == "inside");
    }

    [Fact]
    public void IlContornoSpessoIngrossaSoloFuori()
    {
        var thick = _presets.For(_range).Single(preset => preset.Key == "thickOutline").Apply(BorderFrame.Empty, _range, _thin);

        Assert.Equal(0.75 * 2.5, thick.Stroke(BorderPosition.Top)!.WidthPoints, 3);
        Assert.Equal(_thin, thick.Stroke(BorderPosition.InsideHorizontal));
    }

    [Fact]
    public void IlClicCommutaIlLatoEIgnoraQuelliFuoriPortata()
    {
        var on = _editing.Toggle(BorderFrame.Empty, _cell, BorderPosition.DiagonalUp, _thin);
        var off = _editing.Toggle(on, _cell, BorderPosition.DiagonalUp, _thin);
        var ignored = _editing.Toggle(BorderFrame.Empty, _cell, BorderPosition.InsideVertical, _thin);

        Assert.True(on.Has(BorderPosition.DiagonalUp));
        Assert.Equal(BorderFrame.Empty, off);
        Assert.Equal(BorderFrame.Empty, ignored);
    }

    [Fact]
    public void UnLatoGiaPresenteConAltroStileVieneRistilizzato()
    {
        var frame = BorderFrame.Empty.With(BorderPosition.Top, _thin);

        Assert.Equal(_red, _editing.Toggle(frame, _cell, BorderPosition.Top, _red).Stroke(BorderPosition.Top));
    }

    [Fact]
    public void LaCellaInMezzoRiceveLeLineeInterne()
    {
        var after = BorderFrame.Empty.With(BorderPosition.InsideHorizontal, _thin).With(BorderPosition.Top, _red);
        var middle = _resolver.Place(1, 1, 0, 0, 2, 2);
        var corner = _resolver.Place(0, 0, 0, 0, 2, 2);

        var inner = _resolver.Resolve(BorderFrame.Empty, after, middle);
        var top = _resolver.Resolve(BorderFrame.Empty, after, corner);

        Assert.Equal(_thin, inner.Top.Stroke);
        Assert.Equal(_thin, inner.Bottom.Stroke);
        Assert.False(inner.Left.Changes);
        Assert.Equal(_red, top.Top.Stroke);
        Assert.Equal(_thin, top.Bottom.Stroke);
    }

    [Fact]
    public void ILatiNonToccatiRestanoComeSono()
    {
        var before = BorderFrame.Empty.With(BorderPosition.Left, _thin);
        var after = before.With(BorderPosition.Right, _red);

        var edits = _resolver.Resolve(before, after, new CellPlacement(true, true, true, true));

        Assert.False(edits.Left.Changes);
        Assert.True(edits.Right.Changes);
        Assert.Equal(_red, edits.Right.Stroke);
    }

    [Fact]
    public void IBordiDiFolioFannoIlGiroCompleto()
    {
        var frames = new BoxBorderFrames();
        var borders = new BoxBorders { Top = new BorderLine(1, 0xFF112233) { Style = BorderLineStyle.Dotted }, Left = new BorderLine(2, 0xFFDC2626) { Style = BorderLineStyle.Double }, PaddingTop = 3, PaddingLeft = 3 };

        var frame = frames.Frame(borders);

        Assert.Equal(new BorderStroke(BorderDash.Dotted, 1, "#112233"), frame.Stroke(BorderPosition.Top));
        Assert.Equal(borders, frames.Apply(null, frame, 3));
    }

    [Fact]
    public void LaTabellaDiFolioRiceveContornoEInterni()
    {
        var fixture = new Fixture();
        var grid = new TableGrid(fixture.Tree);
        var selections = new TableSelections(new TableEditing(fixture.Stories, fixture.Deletion), grid, new PulseOffice.Document.Tables.TableSpans());
        var framing = new TableBorderFraming(grid, new BoxBorderFrames(), _resolver);
        fixture.Editor.InsertTable(fixture.Document, fixture.History, new DocumentPosition(fixture.First.Id, 0), 3, 3);
        var table = fixture.Document.Body.Blocks.OfType<TableBlock>().Single();
        ParagraphBlock At(int row, int column) => table.Cell(row, column).Content.Blocks.OfType<ParagraphBlock>().First();
        var selection = selections.Select(fixture.Document, new DocumentPosition(At(0, 0).Id, 0), new DocumentPosition(At(1, 1).Id, 0))!;
        var before = framing.FrameOf(selection);
        var outline = _presets.For(framing.Scope(selection)).Single(preset => preset.Key == "outline").Apply(before, framing.Scope(selection), _thin);
        var after = outline.With(BorderPosition.InsideVertical, _red);

        framing.Apply(fixture.Document, fixture.History, selection, before, after);

        var line = new BorderLine(0.75, 0xFF000000);
        Assert.Equal(line, table.Cell(0, 0).Borders!.Top);
        Assert.Equal(line, table.Cell(0, 0).Borders!.Left);
        Assert.Equal(BorderLineStyle.Dashed, table.Cell(0, 0).Borders!.Right!.Style);
        Assert.Null(table.Cell(0, 0).Borders!.Bottom);
        Assert.Equal(line, table.Cell(1, 1).Borders!.Bottom);
        Assert.Null(table.Cell(2, 2).Borders);
        Assert.Equal(after.With(BorderPosition.InsideHorizontal, null), framing.FrameOf(selection).With(BorderPosition.InsideHorizontal, null));
    }
}
