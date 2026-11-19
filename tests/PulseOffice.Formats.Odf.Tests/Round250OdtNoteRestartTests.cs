using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round250OdtNoteRestartTests
{
    [Fact]
    public void Note_numbering_restarting_at_each_section_crosses_odt()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Notes.FootnoteSettings = document.Notes.FootnoteSettings with { Restart = NoteNumbering.Section };
        document.Notes.EndnoteSettings = document.Notes.EndnoteSettings with { Restart = NoteNumbering.Chapter };

        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream).Value!;

        Assert.Equal(NoteNumbering.Section, read.Notes.FootnoteSettings.Restart);
        Assert.Equal(NoteNumbering.Chapter, read.Notes.EndnoteSettings.Restart);
    }
}
