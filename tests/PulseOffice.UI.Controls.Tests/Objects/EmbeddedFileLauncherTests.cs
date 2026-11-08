using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.UI.Controls.Tests.Objects;

public sealed class EmbeddedFileLauncherTests : IDisposable
{
    private sealed class RecordingOpener : IShellOpener
    {
        public List<string> Opened { get; } = [];

        public OperationResult<bool> OpenUrl(string url) => OperationResult<bool>.Ok(true);

        public OperationResult<bool> OpenFile(string path)
        {
            Opened.Add(path);
            return OperationResult<bool>.Ok(true);
        }
    }

    private readonly TemporaryFiles _temporary = new();
    private readonly RecordingOpener _opener = new();

    public void Dispose() => _temporary.Dispose();

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("Invoice.pdf.command")]
    [InlineData("run.sh")]
    [InlineData("launcher.desktop")]
    [InlineData("script.vbs")]
    [InlineData("Payload.app")]
    [InlineData("shortcut.lnk")]
    [InlineData("macro.docm")]
    [InlineData("page.html")]
    [InlineData("noextension")]
    [InlineData("../../evil.bat")]
    [InlineData("trailing.exe.")]
    public async Task An_embedded_file_that_can_run_code_is_never_handed_to_the_system(string name)
    {
        var launcher = new EmbeddedFileLauncher(_opener, _temporary, new ExecutableFileTypes());

        var result = await launcher.OpenAsync(new byte[] { 1, 2, 3 }, name, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("OPEN004", result.Diagnostics[0].Code);
        Assert.Empty(_opener.Opened);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("folder/")]
    public async Task An_embedded_file_without_a_name_fails_with_a_diagnostic_instead_of_throwing(string name)
    {
        var launcher = new EmbeddedFileLauncher(_opener, _temporary, new ExecutableFileTypes());

        var result = await launcher.OpenAsync(new byte[] { 1, 2, 3 }, name, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Empty(_opener.Opened);
    }

    [Theory]
    [InlineData("photo.png")]
    [InlineData("clip.mp4")]
    [InlineData("report.pdf")]
    [InlineData("../../../escape.txt")]
    public async Task A_document_or_media_file_opens_inside_its_own_temporary_folder(string name)
    {
        var launcher = new EmbeddedFileLauncher(_opener, _temporary, new ExecutableFileTypes());

        var result = await launcher.OpenAsync(new byte[] { 1, 2, 3 }, name, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var opened = Assert.Single(_opener.Opened);
        Assert.Equal(Path.GetFileName(name), Path.GetFileName(opened));
        Assert.StartsWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(opened), StringComparison.Ordinal);
    }
}
