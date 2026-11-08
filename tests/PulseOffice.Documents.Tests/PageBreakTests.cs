using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class PageBreakTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void UnParagrafoPuoAprireUnaPaginaConUnAltroStileERipartireLaNumerazione()
    {
        _f.Type("Copertina", "Capitolo");
        var before = _f.Snapshot();

        _f.Editor.SetPageStyle(_f.Document, _f.History, _f.Paragraph(1).Id, DefaultStyleNames.Landscape, 1);

        Assert.Equal(DefaultStyleNames.Landscape, _f.Paragraph(1).Content.PageStyleName);
        Assert.Equal(1, _f.Paragraph(1).Content.PageNumberRestart);
        var effective = _f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.Paragraph(1).Content);
        Assert.Equal(DefaultStyleNames.Landscape, effective.PageStyleName);
        _f.History.Undo();
        Assert.Equal(before, _f.Snapshot());
        _f.History.Redo();
        Assert.Equal(1, _f.Paragraph(1).Content.PageNumberRestart);

        _f.Editor.SetPageStyle(_f.Document, _f.History, _f.Paragraph(1).Id, null);
        Assert.Null(_f.Paragraph(1).Content.PageStyleName);
        Assert.Null(_f.Paragraph(1).Content.PageNumberRestart);
    }

    [Fact]
    public void LoStileDiParagrafoPuoPortareLoStileDiPagina()
    {
        _f.Document.Styles.Paragraph.Add(new ParagraphStyle("Apertura") { Paragraph = new ParagraphProperties { PageStyleName = DefaultStyleNames.Landscape } });
        _f.Type("Testo");
        _f.Editor.ApplyParagraphStyle(_f.Document, _f.History, DocumentRange.At(_f.At(0, 0)), "Apertura");

        Assert.Equal(DefaultStyleNames.Landscape, _f.Resolver.EffectiveParagraph(_f.Document.Styles, _f.First.Content).PageStyleName);
    }

    [Fact]
    public void DividereLasciaIlCambioDiPaginaAlPrimoParagrafoEUnireLoConserva()
    {
        _f.Type("Capitolo");
        _f.Editor.SetPageStyle(_f.Document, _f.History, _f.First.Id, DefaultStyleNames.Landscape, 3);

        _f.Editor.SplitParagraph(_f.Document, _f.History, _f.At(0, 4));
        Assert.Equal(DefaultStyleNames.Landscape, _f.First.Content.PageStyleName);
        Assert.Null(_f.Paragraph(1).Content.PageStyleName);
        Assert.Null(_f.Paragraph(1).Content.PageNumberRestart);

        _f.Editor.MergeWithNext(_f.Document, _f.History, _f.First.Id);
        Assert.Equal(3, _f.First.Content.PageNumberRestart);
    }

    [Fact]
    public void StiliInesistentiCelleENumeriNegativiSonoRifiutati()
    {
        _f.Type("Uno", "Due");
        Assert.Throws<KeyNotFoundException>(() => _f.Editor.SetPageStyle(_f.Document, _f.History, _f.First.Id, "Nope"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _f.Editor.SetPageStyle(_f.Document, _f.History, _f.First.Id, DefaultStyleNames.Standard, -1));
        Assert.Throws<ArgumentException>(() => _f.Editor.SetPageStyle(_f.Document, _f.History, _f.First.Id, null, 2));
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(1, 0), 1, 1);
        var cell = (ParagraphBlock)((TableBlock)_f.Document.Body[1]).Cell(0, 0).Content[0];
        Assert.Throws<InvalidOperationException>(() => _f.Editor.SetPageStyle(_f.Document, _f.History, cell.Id, DefaultStyleNames.Standard));
        Assert.Null(_f.First.Content.PageStyleName);
    }

    [Fact]
    public void LaFormattazioneDirettaCancellataNonTogliIlCambioDiPagina()
    {
        var paragraph = new PulseOffice.Text.Rich.TextParagraph("x") { PageStyleName = DefaultStyleNames.Landscape, PageNumberRestart = 2 };
        var cleared = new DirectFormatting().Clear(paragraph);
        Assert.Equal(DefaultStyleNames.Landscape, cleared.PageStyleName);
        Assert.Equal(2, cleared.PageNumberRestart);
    }
}

public sealed class RegistrationTests
{
    [Fact]
    public void IlContenitoreRisolveOgniServizioDelModello()
    {
        var services = new ServiceCollection().AddDocumentsModel();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        foreach (var descriptor in services) Assert.NotNull(provider.GetRequiredService(descriptor.ServiceType));

        var factory = provider.GetRequiredService<ITextDocumentFactory>();
        var document = factory.Create();
        var history = new PulseOffice.Document.Commands.UndoStack();
        var editor = provider.GetRequiredService<IDocumentEditor>();
        editor.InsertText(document, history, new DocumentPosition(((ParagraphBlock)document.Body[0]).Id, 0), "Uno\nDue");
        var clipboard = provider.GetRequiredService<IDocumentClipboard>();
        var second = ((ParagraphBlock)document.Body[1]).Id;
        clipboard.Paste(document, history, new DocumentPosition(second, 3), clipboard.Copy(document, DocumentRange.In(((ParagraphBlock)document.Body[0]).Id, 0, 3)));
        Assert.Equal("DueUno", ((ParagraphBlock)document.Body[1]).Text);
        using var tracker = provider.GetRequiredService<IPositionTrackerFactory>().Create(document);
        Assert.NotNull(tracker.Track(new DocumentPosition(second, 0)));
    }
}
