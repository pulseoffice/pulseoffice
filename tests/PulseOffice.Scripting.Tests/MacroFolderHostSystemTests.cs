using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class MacroFolderHostSystemTests
{
    [Theory]
    [InlineData(HostSystemKind.MacOS, true)]
    [InlineData(HostSystemKind.Linux, false)]
    public void A_folder_differing_only_in_letter_case_is_inside_only_where_paths_ignore_case(HostSystemKind kind, bool accepted)
    {
        var root = Path.Combine(Path.GetTempPath(), "pulse-macro-case-" + Guid.NewGuid().ToString("N"), "Macros");
        var folder = new MacroFileFolder(root, new FixedHostSystem(kind));
        var other = Path.Combine(Path.GetDirectoryName(root)!, "MACROS", "note.txt");

        try
        {
            var refusal = Record.Exception(() => folder.Resolve(other, MacroFileFolder.TextExtensions));

            Assert.Equal(accepted, refusal is null);
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(root))) Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }
}
