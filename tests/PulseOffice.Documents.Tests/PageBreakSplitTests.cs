using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class PageBreakSplitTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void Invio_dopo_un_interruzione_di_pagina_non_ripete_l_interruzione()
    {
        _f.Type("Appendice");
        _f.Editor.SetParagraph(_f.Document, _f.History, DocumentRange.In(_f.First.Id, 0, 0), new ParagraphProperties { PageBreakBefore = true, ColumnBreakBefore = true });

        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 9));

        Assert.True(_f.First.Content.PageBreakBefore);
        Assert.Null(_f.Paragraph(1).Content.PageBreakBefore);
        Assert.Null(_f.Paragraph(1).Content.ColumnBreakBefore);
    }
}
