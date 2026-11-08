using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class StyleSelectionTests
{
    private readonly Fixture _f = new();
    private readonly StyleFromSelection _selection;
    private readonly StyleEditing _styles;

    public StyleSelectionTests()
    {
        _selection = new StyleFromSelection(_f.Stories, _f.Text, new DirectFormatting());
        _styles = new StyleEditing(new StyleReferences(_f.Stories, _f.Tree));
        _f.Type("Testo in evidenza", "Altro");
        _f.Editor.SetParagraph(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), new ParagraphProperties { Align = TextAlign.Center, SpaceAfter = 9 });
        _f.Editor.SetCharacter(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(0, 17)), new CharacterProperties { Bold = true });
    }

    [Fact]
    public void IlNuovoStileDallaSelezionePrendeLaFormattazioneELaToglieDalParagrafo()
    {
        _selection.NewParagraphStyle(_f.Document, _f.History, _f.Paragraph(0).Id, "Evidenza");

        var style = _f.Document.Styles.Paragraph.Find("Evidenza")!;
        Assert.Equal(TextAlign.Center, style.Paragraph.Align);
        Assert.Equal(9, style.Paragraph.SpaceAfter);
        Assert.True(style.Character.Bold);
        var paragraph = _f.Paragraph(0).Content;
        Assert.Equal("Evidenza", paragraph.StyleName);
        Assert.Equal(ParagraphOverrides.None, paragraph.Explicit);
        Assert.False(paragraph.Runs[0].Bold);
        var effective = _f.Resolver.EffectiveParagraph(_f.Document.Styles, paragraph);
        Assert.Equal(TextAlign.Center, effective.Align);
        Assert.True(effective.Runs[0].Bold);

        _f.History.Undo();
        _f.History.Redo();
        _f.History.Undo();
        Assert.Null(_f.Document.Styles.Paragraph.Find("Evidenza"));
        Assert.True(_f.Paragraph(0).Content.Runs[0].Bold);
    }

    [Fact]
    public void AggiornareLoStileLoRidefinisceConLaSelezione()
    {
        _selection.UpdateParagraphStyle(_f.Document, _f.History, _f.Paragraph(0).Id);

        Assert.Equal(TextAlign.Center, _f.Document.Styles.Paragraph.Find(DefaultStyleNames.Standard)!.Paragraph.Align);
        Assert.Equal(TextAlign.Center, _f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.Paragraph(1).Content).Align);
        _f.History.Undo();
        Assert.NotEqual(TextAlign.Center, _f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.Paragraph(1).Content).Align);
    }

    [Fact]
    public void LoStileDiCarattereNasceDallaSelezione()
    {
        _selection.NewCharacterStyle(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(0, 5)), "Forte");

        Assert.True(_f.Document.Styles.Character.Find("Forte")!.Character.Bold);
        Assert.Equal("Forte", _f.Paragraph(0).Content.Runs[0].StyleName);
        Assert.Equal("Testo", _f.Paragraph(0).Content.Runs[0].Text);
    }

    [Fact]
    public void EliminareUnoStileRiportaIParagrafiAlGenitoreESiAnnulla()
    {
        _selection.NewParagraphStyle(_f.Document, _f.History, _f.Paragraph(0).Id, "Evidenza");
        _f.Document.Styles.Paragraph.Add(new ParagraphStyle("Figlio") { ParentName = "Evidenza" });

        _styles.Delete(_f.Document, _f.History, StyleFamily.Paragraph, "Evidenza");

        Assert.Null(_f.Document.Styles.Paragraph.Find("Evidenza"));
        Assert.Equal(DefaultStyleNames.Standard, _f.Paragraph(0).StyleName);
        Assert.Equal(DefaultStyleNames.Standard, _f.Document.Styles.Paragraph.Find("Figlio")!.ParentName);
        _f.History.Undo();
        Assert.Equal("Evidenza", _f.Paragraph(0).StyleName);
        Assert.Equal("Evidenza", _f.Document.Styles.Paragraph.Find("Figlio")!.ParentName);
    }

    [Fact]
    public void CaricareGliStiliAggiungeSoloQuelliNuoviSenzaSostituire()
    {
        var template = new StyleSheet();
        template.Paragraph.Add(new ParagraphStyle("Modello") { Paragraph = new ParagraphProperties { Align = TextAlign.Right } });
        template.Paragraph.Add(new ParagraphStyle(DefaultStyleNames.Standard) { Paragraph = new ParagraphProperties { Align = TextAlign.Right } });

        _styles.Load(_f.Document, _f.History, template, overwrite: false);

        Assert.NotNull(_f.Document.Styles.Paragraph.Find("Modello"));
        Assert.NotEqual(TextAlign.Right, _f.Document.Styles.Paragraph.Find(DefaultStyleNames.Standard)!.Paragraph.Align);
        _f.History.Undo();
        Assert.Null(_f.Document.Styles.Paragraph.Find("Modello"));
    }
}

public sealed class FormatBrushTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void LaFormattazioneClonataPassaAlTestoEAlParagrafo()
    {
        var brush = new FormatBrush(_f.Stories, _f.Text, new DirectFormatting());
        _f.Type("Sorgente", "Bersaglio");
        _f.Editor.SetCharacter(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(0, 8)), new CharacterProperties { Italic = true, Color = 0xFFFF0000 });
        _f.Editor.SetParagraph(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), new ParagraphProperties { Align = TextAlign.Right });
        _f.Editor.SetCharacter(_f.Document, _f.History, new DocumentRange(_f.At(1, 0), _f.At(1, 9)), new CharacterProperties { Bold = true });

        var sample = brush.Capture(_f.Document, _f.At(0, 2));
        brush.Paint(_f.Document, _f.History, new DocumentRange(_f.At(1, 0), _f.At(1, 9)), sample, false);

        var run = _f.Paragraph(1).Content.Runs[0];
        Assert.True(run.Italic);
        Assert.False(run.Bold);
        Assert.Equal(0xFFFF0000u, run.Color);
        Assert.Equal(TextAlign.Right, _f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.Paragraph(1).Content).Align);
        _f.History.Undo();
        Assert.True(_f.Paragraph(1).Content.Runs[0].Bold);
        _f.History.Redo();
        Assert.True(_f.Paragraph(1).Content.Runs[0].Italic);
        Assert.False(_f.Paragraph(1).Content.Runs[0].Bold);
    }
}

public sealed class SectionEditingTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void LaSezioneAvvolgeIParagrafiSiModificaESiToglie()
    {
        var sections = new SectionEditing(_f.Stories, _f.Tree);
        _f.Type("Uno", "Due", "Tre");

        sections.Wrap(_f.Document, _f.History, new DocumentRange(_f.At(0, 0), _f.At(1, 0)), new SectionSettings("A", new Blocks.SectionColumns(2, Foundation.Units.Twip.FromCentimeters(1)), false, false));
        var section = Assert.IsType<Blocks.SectionBlock>(_f.Document.Body[0]);
        Assert.Equal(2, section.Content.Count);
        Assert.Same(section, sections.Around(_f.Document, ((Blocks.ParagraphBlock)section.Content[1]).Id));

        sections.Change(_f.Document, _f.History, section.Id, new SectionSettings("B", Blocks.SectionColumns.Single, true, false));
        var changed = Assert.IsType<Blocks.SectionBlock>(_f.Document.Body[0]);
        Assert.Equal("B", changed.Name);
        Assert.True(changed.Protected);

        sections.Unwrap(_f.Document, _f.History, section.Id);
        Assert.Equal(3, _f.Document.Body.Count);

        _f.History.Undo();
        _f.History.Undo();
        Assert.Equal("A", Assert.IsType<Blocks.SectionBlock>(_f.Document.Body[0]).Name);
        _f.History.Undo();
        Assert.Equal(3, _f.Document.Body.Count);
    }
}
