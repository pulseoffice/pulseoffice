using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round176OpenThroughLinkTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pulseoffice-r176-" + Guid.NewGuid().ToString("N"));

    public Round176OpenThroughLinkTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_file_open_through_its_target_is_found_through_a_symbolic_link()
    {
        var target = Path.Combine(_folder, "letter.odt");
        File.WriteAllBytes(target, [1]);
        var link = Path.Combine(_folder, "link.odt");
        try { File.CreateSymbolicLink(link, target); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { Assert.Skip("Symbolic links are not available here."); }
        var workspace = new DocumentWorkspace();
        var open = new OpenDocument(PulseOffice.Foundation.Modules.ModuleKind.Documents, "letter", new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());
        open.RestoreLocation(target);
        workspace.Open(open);
        var router = new DocumentOpenRouter([], null!, new FakeTextCatalog(), workspace, null!, new PulseOffice.Platform.RuntimeHostSystem());

        Assert.Same(open, router.FindOpen(link));
    }
}
