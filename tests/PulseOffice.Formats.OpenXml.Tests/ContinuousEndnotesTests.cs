using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ContinuousEndnotesTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument WithEndnote(bool continuous)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([new ParagraphBlock("p1", "Testo") { Marks = [new NoteMark(5, "e1")] }]);
        document.Notes.Add(new Note("e1", NoteKind.Endnote, new BlockContainer([new ParagraphBlock("e1p", "Chiusura", DefaultStyleNames.Endnote)])));
        document.Notes.ContinuousEndnotes = continuous;
        return document;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Le_note_di_chiusura_continue_o_su_pagine_proprie_attraversano_ODT(bool continuous)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(WithEndnote(continuous), stream).Succeeded);
        stream.Position = 0;
        Assert.Equal(continuous, new OdtReader().Read(stream).Value!.Notes.ContinuousEndnotes);
    }

    [Fact]
    public void In_DOCX_le_note_di_chiusura_sono_continue_e_la_perdita_si_dichiara()
    {
        var writer = _composer.Writer(DocxFlavour.Document);
        Assert.Contains(writer.CheckFidelity(WithEndnote(false)).Losses, diagnostic => diagnostic.Code == "DOCX115");
        Assert.DoesNotContain(writer.CheckFidelity(WithEndnote(true)).Losses, diagnostic => diagnostic.Code == "DOCX115");

        using var stream = new MemoryStream();
        Assert.True(writer.Write(WithEndnote(false), stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
        Assert.True(read.Notes.ContinuousEndnotes);
        Assert.Single(read.Notes.OfKind(NoteKind.Endnote));
    }
}
