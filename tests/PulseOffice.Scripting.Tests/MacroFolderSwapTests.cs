using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Scripting.Tests;

[Collection("scripting")]
public sealed class MacroFolderSwapTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "pulse-macro-swap-" + Guid.NewGuid().ToString("N"));
    private readonly ScriptExecutionPermissions _permissions = new();
    private readonly MacroFileFolder _folder;
    private readonly string _outside;

    public MacroFolderSwapTests()
    {
        _folder = new MacroFileFolder(Path.Combine(_scratch, "Macros"));
        _outside = Path.Combine(_scratch, "fuori");
        Directory.CreateDirectory(_outside);
        Directory.CreateDirectory(Path.Combine(_folder.Root, "report"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_scratch)) Directory.Delete(_scratch, recursive: true);
    }

    [Theory]
    [InlineData(SwapMoment.WhileWriting)]
    [InlineData(SwapMoment.JustBeforeTheMove)]
    public void A_subfolder_swapped_for_a_link_never_leaves_the_file_outside(SwapMoment moment)
    {
        using var scope = _permissions.Enter(ScriptPermissions.Sandboxed with { MacroFolderWrite = true });
        var subfolder = Path.Combine(_folder.Root, "report");
        var links = new SwappingLinks(new FileSystemLinks(), subfolder, _outside, Path.Combine(subfolder, "totali.csv"), moment);
        var files = new ScriptFilesFacade(_permissions, _folder, new ConfinedFileWriter(links));

        Assert.Throws<MacroFilePathException>(() => files.WriteText("report/totali.csv", "macro"));

        Assert.True(links.Swapped);
        Assert.Empty(Directory.GetFiles(_outside));
    }

    [Fact]
    public void Without_a_swap_the_file_is_written_inside_its_subfolder()
    {
        using var scope = _permissions.Enter(ScriptPermissions.Sandboxed with { MacroFolderWrite = true });
        var files = new ScriptFilesFacade(_permissions, _folder, new ConfinedFileWriter(new FileSystemLinks()));

        var path = files.WriteText("report/nuovo/totali.csv", "a;b");

        Assert.Equal("a;b", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    public enum SwapMoment
    {
        WhileWriting,
        JustBeforeTheMove,
    }

    private sealed class SwappingLinks(IFileSystemLinks inner, string subfolder, string outside, string target, SwapMoment moment) : IFileSystemLinks
    {
        private int _subfolderChecks;

        public bool Swapped { get; private set; }

        public bool IsLink(string path)
        {
            if (!Swapped && moment == SwapMoment.WhileWriting && path == subfolder && ++_subfolderChecks == 2) Swap();
            var answer = inner.IsLink(path);
            if (!Swapped && moment == SwapMoment.JustBeforeTheMove && path == target) Swap();
            return answer;
        }

        private void Swap()
        {
            Directory.Move(subfolder, subfolder + "-vera");
            Directory.CreateSymbolicLink(subfolder, outside);
            foreach (var file in Directory.GetFiles(subfolder + "-vera"))
                File.Move(file, Path.Combine(outside, Path.GetFileName(file)));
            Swapped = true;
        }
    }
}
