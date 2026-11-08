using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Commands;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioAutoTextTests
{
    [Fact]
    public Task UnaVoceDelGlossarioSiSalvaESiRilegge() => FolioScene.RunAsync(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "pulse-autotext-" + Guid.NewGuid().ToString("N"));
        try
        {
            var scene = new FolioScene();
            scene.Type("Distinti saluti");
            scene.Editor.SelectAll();
            var services = scene.Services;
            var library = new AutoTextLibrary(new AutoTextFolder(folder), services.GetRequiredService<PulseOffice.Formats.ITextDocumentFormatRegistry>(),
                services.GetRequiredService<ITextDocumentFactory>(), services.GetRequiredService<IDocumentClipboard>(), services.GetRequiredService<ISubdocuments>(), new PulseOffice.Platform.AtomicFileWriter());

            Assert.True(library.Save("saluti", scene.Editor.Copy()!));
            Assert.Equal(["saluti"], library.Names);

            var fragment = library.Load("saluti");
            Assert.NotNull(fragment);
            scene.Editor.Select(scene.Editor.Selection.End, scene.Editor.Selection.End);
            scene.Editor.Paste(fragment!);
            Assert.Equal("Distinti salutiDistinti saluti", scene.Paragraphs[0]);

            Assert.True(library.Delete("saluti"));
            Assert.Empty(library.Names);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        return Task.CompletedTask;
    });
    [Fact]
    public Task UnaScritturaFallitaNonRovinaLaVoceEsistente() => FolioScene.RunAsync(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "pulse-autotext-" + Guid.NewGuid().ToString("N"));
        try
        {
            var scene = new FolioScene();
            scene.Type("Distinti saluti");
            scene.Editor.SelectAll();
            var services = scene.Services;
            var formats = services.GetRequiredService<PulseOffice.Formats.ITextDocumentFormatRegistry>();
            AutoTextLibrary Library(PulseOffice.Formats.ITextDocumentFormatRegistry registry) => new(new AutoTextFolder(folder), registry,
                services.GetRequiredService<ITextDocumentFactory>(), services.GetRequiredService<IDocumentClipboard>(), services.GetRequiredService<ISubdocuments>(), new PulseOffice.Platform.AtomicFileWriter());
            Assert.True(Library(formats).Save("saluti", scene.Editor.Copy()!));

            Assert.False(Library(new FailingWrites(formats)).Save("saluti", scene.Editor.Copy()!));

            Assert.NotNull(Library(formats).Load("saluti"));
            Assert.Equal(["saluti"], Library(formats).Names);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        return Task.CompletedTask;
    });

    private sealed class FailingWrites(PulseOffice.Formats.ITextDocumentFormatRegistry inner) : PulseOffice.Formats.ITextDocumentFormatRegistry
    {
        public IReadOnlyList<PulseOffice.Formats.TextDocumentFormat> ReadableFormats => inner.ReadableFormats;

        public IReadOnlyList<PulseOffice.Formats.TextDocumentFormat> WritableFormats => inner.WritableFormats;

        public IReadOnlyList<PulseOffice.Formats.TextDocumentFormat> ExportFormats => inner.ExportFormats;

        public PulseOffice.Formats.TextDocumentFormat Native => inner.Native;

        public PulseOffice.Formats.ITextDocumentReader? ReaderFor(string path) => inner.ReaderFor(path);

        public PulseOffice.Formats.ITextDocumentWriter? WriterFor(string path) => inner.WriterFor(path) is { } writer ? new Failing(writer) : null;

        public PulseOffice.Formats.ITextDocumentReader? ReaderForContent(Stream stream) => inner.ReaderForContent(stream);

        public PulseOffice.Foundation.Diagnostics.OperationResult<TextDocument> Read(string path, Stream stream) => inner.Read(path, stream);
    }

    private sealed class Failing(PulseOffice.Formats.ITextDocumentWriter inner) : PulseOffice.Formats.ITextDocumentWriter
    {
        public PulseOffice.Formats.TextDocumentFormat Format => inner.Format;

        public PulseOffice.Formats.FidelityReport CheckFidelity(TextDocument document) => inner.CheckFidelity(document);

        public PulseOffice.Foundation.Diagnostics.OperationResult<bool> Write(TextDocument document, Stream stream)
        {
            stream.WriteByte(0);
            return PulseOffice.Foundation.Diagnostics.OperationResult<bool>.Fail(PulseOffice.Foundation.Diagnostics.Diagnostic.Error("test.write", "write failed"));
        }
    }
}
