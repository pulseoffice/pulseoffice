using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Foundation.Modules;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class SharedLockReadingTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulse-lock-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_huge_lock_file_is_not_read_whole()
    {
        var document = Path.Combine(_folder, "Report.ods");
        var lockPath = FileDocumentSharing.LockPathOf(document);
        using (var writer = new StreamWriter(lockPath))
        {
            writer.Write(",Ada,studio,03.10.2026 14:05,");
            writer.Write(new string('x', 32 * 1024 * 1024));
            writer.Write(';');
        }

        var sharing = new FileDocumentSharing();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var holder = sharing.Holder(document);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.NotNull(holder);
        Assert.Equal("Ada", holder.User);
        Assert.True(allocated < 1024 * 1024, $"allocated {allocated} bytes");
    }
}

public sealed class TemplatePreviewCacheTests
{
    private sealed class Pages : IPrintableDocument
    {
        public PulseOffice.Foundation.Pages.PageSetup Setup => throw new NotSupportedException();

        public int PageCount => 1;

        public void PaintPage(int index, IPageCanvas canvas)
        {
        }
    }

    private sealed class Operations : IDocumentOperations
    {
        public int Previews { get; private set; }

        public ModuleKind Module => ModuleKind.Sheets;

        public string NativeExtension => "ods";

        public string OfficeExtension => "xlsx";

        public string TemplateExtension => "ots";

        public string? OfficeTemplateExtension => "xltx";

        public IPrintableDocument? PreviewFile(string path)
        {
            Previews++;
            return new Pages();
        }

        public string SuggestedName(OpenDocument document) => string.Empty;

        public IPrintableDocument? BuildPrintable(OpenDocument document) => null;

        public PulseOffice.Document.Metadata.DocumentProperties? Properties(OpenDocument document) => null;

        public bool WriteTo(OpenDocument document, string path) => false;

        public string? RenderHtml(OpenDocument document) => null;

        public PulseOffice.Document.Versions.DocumentVersionCollection? Versions(OpenDocument document) => null;
    }

    private sealed class Paths : PulseOffice.Settings.IUserDataPaths
    {
        public string ConfigurationDirectory => Path.GetTempPath();

        public string CacheDirectory => Path.GetTempPath();

        public string SettingsFile => Path.Combine(Path.GetTempPath(), "settings.json");

        public string RecoveryDirectory => Path.GetTempPath();

        public string Configuration(string name) => Path.Combine(Path.GetTempPath(), name);
    }

    private readonly Operations _operations = new();

    private TemplateLibrary Library() =>
        new(new DocumentOperationsCatalog([_operations]), new Paths(), NullLogger<TemplateLibrary>.Instance);

    [Fact]
    public void A_changed_template_replaces_its_old_preview()
    {
        var library = Library();
        var first = new TemplateEntry("/t/Budget.ots", "Budget", ModuleKind.Sheets, new DateTime(2026, 1, 1));
        var second = first with { Modified = new DateTime(2026, 1, 2) };

        library.FirstPages(first);
        library.FirstPages(second);
        library.FirstPages(first);

        Assert.Equal(3, _operations.Previews);
    }

    [Fact]
    public void The_preview_cache_has_a_ceiling()
    {
        var library = Library();
        var oldest = new TemplateEntry("/t/0.ots", "0", ModuleKind.Sheets, DateTime.MinValue);
        library.FirstPages(oldest);
        for (var index = 1; index <= TemplateLibrary.PreviewCapacity; index++)
            library.FirstPages(oldest with { Path = $"/t/{index}.ots" });

        library.FirstPages(oldest);

        Assert.Equal(TemplateLibrary.PreviewCapacity + 2, _operations.Previews);
    }
}
