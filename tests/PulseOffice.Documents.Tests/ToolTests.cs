using System.Globalization;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Protection;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class ToolTests
{
    private readonly Fixture _f = new();

    private void Lines(params string[] texts) =>
        _f.Document.Body.Replace(texts.Select(text => new ParagraphBlock(_f.Document.NextId("p"), text)));

    private IEnumerable<string> Texts => _f.Document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text);

    [Fact]
    public void IParagrafiSiOrdinanoPerColonnaNumericaESiAnnulla()
    {
        Lines("b;10", "a;9", "c;100");
        var range = new DocumentRange(new DocumentPosition(_f.Paragraph(0).Id, 0), new DocumentPosition(_f.Paragraph(2).Id, 1));
        var sorting = new ParagraphSorting(_f.Stories);

        sorting.Sort(_f.Document, _f.History, range, new ParagraphSortKey(2, ';', true, false, false), CultureInfo.InvariantCulture);
        Assert.Equal(["a;9", "b;10", "c;100"], Texts);

        sorting.Sort(_f.Document, _f.History, range with { Start = new DocumentPosition(_f.Paragraph(0).Id, 0), End = new DocumentPosition(_f.Paragraph(2).Id, 0) },
            new ParagraphSortKey(1, null, false, true, false), CultureInfo.InvariantCulture);
        Assert.Equal(["c;100", "b;10", "a;9"], Texts);

        _f.History.Undo();
        _f.History.Undo();
        Assert.Equal(["b;10", "a;9", "c;100"], Texts);
    }

    [Fact]
    public void CampiESegnalibriProtettiNonSiCancellano()
    {
        Lines("prima");
        var paragraph = _f.Paragraph(0);
        _f.Editor.InsertField(_f.Document, _f.History, new DocumentPosition(paragraph.Id, 5), TextFieldKind.PageNumber, "1");
        _f.Editor.AddBookmark(_f.Document, _f.History, DocumentRange.In(paragraph.Id, 0, 2), "inizio");
        var guard = new EditGuard(new Indexes.GeneratedRegions(_f.Tree), _f.Trails);
        var all = new List<ParagraphBlock> { paragraph };

        Assert.False(guard.Protects(_f.Document, all, DocumentRange.In(paragraph.Id, 0, 6)));

        _f.Document.Marks = new MarkProtection(true, false);
        Assert.True(guard.Protects(_f.Document, all, DocumentRange.In(paragraph.Id, 4, 6)));
        Assert.False(guard.Protects(_f.Document, all, DocumentRange.In(paragraph.Id, 0, 4)));

        _f.Document.Marks = new MarkProtection(false, true);
        Assert.True(guard.Protects(_f.Document, all, DocumentRange.In(paragraph.Id, 0, 3)));
        Assert.False(guard.Protects(_f.Document, all, DocumentRange.In(paragraph.Id, 3, 5)));
    }
}

public sealed class TextThemeTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void IlTemaColoraITitoliCambiaIlCarattereESiAnnullaInUnPasso()
    {
        var palette = new PulseOffice.Document.Themes.ThemePalettes().Find("warm")!;
        var font = _f.Document.Styles.DefaultCharacter.Font;

        var changed = new PulseOffice.Documents.Styles.TextThemeApplier(new StyleEditing(new StyleReferences(_f.Stories, _f.Tree))).Apply(_f.Document, _f.History, palette);

        Assert.True(changed > 1);
        Assert.Equal("Georgia", _f.Document.Styles.DefaultCharacter.Font);
        Assert.Equal(0xFF7C2D12u, _f.Document.Styles.Paragraph.Find(PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(1))!.Character.Color);

        _f.History.Undo();

        Assert.Equal(font, _f.Document.Styles.DefaultCharacter.Font);
        Assert.NotEqual(0xFF7C2D12u, _f.Document.Styles.Paragraph.Find(PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(1))!.Character.Color);
    }
}
