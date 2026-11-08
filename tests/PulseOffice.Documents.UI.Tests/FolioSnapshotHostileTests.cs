using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioSnapshotHostileTests
{
    public static TheoryData<string> Ostili => new()
    {
        "",
        "[]",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":1,\"document\":{}}",
        "nul",
        "{\"format\":5,\"version\":2}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":\"2\"}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2.5}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":99999999999999}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":5}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"body\":{\"blocks\":[{\"id\":\"p1\",\"block\":\"boh\"}]}}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"body\":{\"blocks\":[{\"id\":null,\"block\":\"paragraph\"}]}}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"ids\":[null,\"\"]}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"body\":{\"blocks\":[{\"id\":\"t\",\"block\":\"table\",\"rows\":[{\"cells\":[{\"columnSpan\":-4,\"rowSpan\":0}]}],\"columns\":[]}]}}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"body\":{\"blocks\":[{\"id\":\"t\",\"block\":\"table\",\"rows\":[null],\"columns\":[null]}]}}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"body\":{\"blocks\":[null]}}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"comments\":[null],\"notes\":[null],\"objects\":[null],\"changes\":[null],\"macros\":[null],\"versions\":[null],\"keywords\":[null]}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"comments\":[{\"id\":\"c\"},{\"id\":\"c\"}],\"notes\":[{\"id\":\"n\"},{\"id\":\"n\"}]}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"changes\":[{\"id\":\"x\",\"change\":\"Nope\"}]}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"objects\":[{\"id\":\"o\",\"anchor\":{\"$kind\":\"System.IO.FileInfo\"},\"content\":{\"kind\":\"embedded\",\"value\":{\"$kind\":\"X\"}}}]}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"styles\":null,\"initialPageStyle\":\"inesistente\",\"properties\":{\"custom\":{\"a\":null}}}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"versions\":[{},{}],\"macros\":[{},{}]}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"body\":{\"blocks\":[{\"id\":\"p1\",\"block\":\"paragraph\",\"content\":{\"Runs\":[{\"Text\":null}]}}]}}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":{\"variables\":{\"\":\"x\"},\"protectionKey\":\"!!!\"}}",
        "{\"format\":\"pulseoffice-folio-snapshot\",\"version\":2,\"document\":" + new string('[', 2000) + "}",
    };

    [Theory]
    [MemberData(nameof(Ostili))]
    public Task UnIstantaneaOstileVieneRifiutataSenzaCadere(string json) => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        var provider = scene.Services.GetServices<IDocumentSnapshotProvider>().Single(candidate => candidate.Module == ModuleKind.Documents);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var restored = await provider.RestoreAsync(stream, "Folio1", CancellationToken.None);

        if (restored is null) return;
        var document = Assert.IsType<TextDocument>(restored.Model);
        Assert.DoesNotContain(document.Body.Blocks, block => block is null);
        Assert.DoesNotContain(document.Comments.All, comment => comment is null);
        Assert.DoesNotContain(document.Notes.All, note => note is null);
        Assert.DoesNotContain(document.Objects.All, item => item is null);
        Assert.DoesNotContain(document.Changes.All, change => change is null);
        var view = Assert.IsType<PulseOffice.Documents.UI.FolioView>(restored.View);
        Assert.NotEmpty(view.Editor.Layout.Pages);
    });
}
