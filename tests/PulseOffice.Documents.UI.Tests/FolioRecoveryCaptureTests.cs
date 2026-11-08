using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioRecoveryCaptureTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sw/qa";
    private const int CorpusSample = 120;

    private static async Task<byte[]> Bytes(IFolioSnapshotCodec codec, TextDocument document)
    {
        using var stream = new MemoryStream();
        await codec.WriteAsync(document, stream, CancellationToken.None);
        return stream.ToArray();
    }

    private static async Task AssertFaithful(IFolioSnapshotCodec codec, TextDocument document, string label)
    {
        var original = await Bytes(codec, document);
        var copied = await Bytes(codec, document.Clone());
        Assert.True(original.AsSpan().SequenceEqual(copied), $"{label}: the captured copy differs from the document");

        using var stream = new MemoryStream(original);
        var restored = await codec.ReadAsync(stream, CancellationToken.None);
        Assert.True(restored is not null, label + ": " + System.Text.Encoding.UTF8.GetString(original));
        var rewritten = await Bytes(codec, restored);
        Assert.True(original.AsSpan().SequenceEqual(rewritten), $"{label}: omitted defaults did not read back as written");
    }

    [Fact]
    public Task A_rich_document_captures_and_reads_back_without_loss() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene();
        var codec = scene.Services.GetRequiredService<IFolioSnapshotCodec>();

        await AssertFaithful(codec, FolioFixesTests.Rich(scene.Document), "rich");
    });

    [Fact]
    public Task The_capture_is_frozen_against_later_edits() => FolioScene.RunAsync(async () =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock(document.NextId("p"), "prima")]));
        var provider = scene.Services.GetServices<IDocumentSnapshotProvider>().Single(candidate => candidate.Module == ModuleKind.Documents);
        var open = new OpenDocument(ModuleKind.Documents, "Folio1", scene.View, scene.History) { Model = scene.Document };

        var capture = provider.Capture(open);
        ((ParagraphBlock)scene.Document.Body.Blocks[0]).Content = new PulseOffice.Text.Rich.TextParagraph("dopo");
        scene.Document.Body.Replace([.. scene.Document.Body.Blocks, new ParagraphBlock(scene.Document.NextId("p"), "aggiunto")]);
        using var stream = new MemoryStream();
        await capture.WriteAsync(stream, CancellationToken.None);
        stream.Position = 0;
        var restored = await provider.RestoreAsync(stream, "Folio1", CancellationToken.None);

        var document = Assert.IsType<TextDocument>(restored!.Model);
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(document.Body.Blocks));
        Assert.Equal("prima", paragraph.Text);
    });

    [Fact]
    public Task Corpus_documents_capture_and_read_back_without_loss() => FolioScene.RunAsync(async () =>
    {
        if (!Directory.Exists(Corpus)) return;
        var scene = new FolioScene();
        var codec = scene.Services.GetRequiredService<IFolioSnapshotCodec>();
        var composer = new DocxComposer(new PulseOffice.Documents.Styles.DirectFormatting(), new PulseOffice.Documents.Styles.DefaultStyleSheetFactory());
        var readers = new (string Extension, Formats.ITextDocumentReader Reader)[] { (".odt", new OdtReader()), (".docx", composer.Reader(DocxFlavour.Document)) };

        var checkedFiles = 0;
        foreach (var (extension, reader) in readers)
        {
            var files = Directory.GetFiles(Corpus, "*" + extension, SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
            var step = Math.Max(1, files.Count / CorpusSample);
            for (var index = 0; index < files.Count; index += step)
            {
                var info = new FileInfo(files[index]);
                if (info.Length > 2_000_000) continue;
                PulseOffice.Foundation.Diagnostics.OperationResult<TextDocument> read;
                try
                {
                    await using var source = File.OpenRead(info.FullName);
                    read = reader.Read(source);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    continue;
                }

                if (!read.Succeeded || read.Value is null || !new PulseOffice.Documents.UI.Shell.Snapshot.FolioSnapshotValidator().IsSound(read.Value)) continue;
                await AssertFaithful(codec, read.Value, info.Name);
                checkedFiles++;
            }
        }

        Assert.True(checkedFiles > 50, $"{checkedFiles} corpus documents checked");
    });
}
